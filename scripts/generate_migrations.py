#!/usr/bin/env python3
"""Generates the EF Core migration, its designer file and the model snapshot.

The generator works on the model that ``scripts/ef_model.py`` extracts from the
domain model, the entity configurations and the ``DatabaseContext``. The output
follows the code shape of the EF Core 10 generators
(``CSharpMigrationsGenerator``, ``CSharpSnapshotGenerator`` and
``CSharpMigrationOperationGenerator``).

It writes the model snapshot and the files of the newest migration. The files of the
published ``InitialCreate`` migration are historical and are never rewritten, exactly
like EF Core leaves an applied migration alone.

Usage:
    python3 scripts/generate_migrations.py --repo-root . [--check]

``--check`` only verifies that the committed files are up to date. When it reports that
the model snapshot is out of date, the model has changed and a new migration has to be
added (with ``dotnet ef migrations add`` as soon as a .NET SDK is available).
"""

from __future__ import annotations

import argparse
import os
import sys
from typing import Any

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import migration_model as mm  # noqa: E402
from ef_model import ModelExtractionError, derive_model  # noqa: E402
from migration_model import MigrationModelError, value_generated_on_add  # noqa: E402

MIGRATION_ID = "20251017120000"
MIGRATION_NAME = "InitialCreate"
MIGRATION_CLASS = "InitialCreate"
MIGRATION_NAMESPACE = "Data.Migrations"
CONTEXT_TYPE = "Data.DatabaseContext"
SNAPSHOT_CLASS = "DatabaseContextModelSnapshot"
MIGRATIONS_DIRECTORY = os.path.join("Infrastructure", "Persistence", "Migrations")

# The migration that brings the schema of 'InitialCreate' in line with the model of the
# code base. 'InitialCreate' itself is a published migration and stays untouched, exactly
# as the rules of 'docs/database-migrations.md' require. Its operations are not written
# down here: they are computed from the difference between the model of the
# 'InitialCreate' designer file and the model of the code base, so that they can not drift
# apart from the snapshot.
DELTA_MIGRATION_ID = "20261017090000"
DELTA_MIGRATION_NAME = "AlignSchemaWithModel"
DELTA_MIGRATION_CLASS = "AlignSchemaWithModel"

# The statements that have to run before the structural operations of a migration. They
# are a human decision and are therefore written down here instead of being computed:
# 'PostCategory.Title' becomes NOT NULL, and SQL Server refuses the 'ALTER COLUMN' while
# a row still holds a NULL. 'PostCategory' is legacy template code that the application
# does not write to, so an empty title is the only sensible replacement.
DELTA_MIGRATION_PRELUDE = [
    "UPDATE [PostCategory] SET [Title] = N'' WHERE [Title] IS NULL;",
]

# The version of the Entity Framework Core packages of the solution. It is written into the
# "ProductVersion" annotation of the model snapshot, exactly like the EF Core tools do.
PRODUCT_VERSION = "10.0.13"

INDENT = "    "

# The delete behaviors of the model and the referential actions of the migration.
DELETE_BEHAVIOR_TO_REFERENTIAL_ACTION = {
    "Cascade": "Cascade",
    "Restrict": "Restrict",
    "NoAction": "NoAction",
    "SetNull": "SetNull",
    "SetDefault": "SetDefault",
    "ClientSetNull": None,
    "ClientCascade": None,
    "ClientNoAction": None,
}


class GenerationError(Exception):
    """Raised when the model cannot be turned into a migration."""


# --------------------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------------------


def literal(value: str) -> str:
    """Renders a C# string literal."""
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


def lambda_expression(columns: list[str]) -> str:
    """Renders the lambda of the migration operations, for example 'x => x.Id'."""
    if len(columns) == 1:
        return f"x => x.{columns[0]}"
    return "x => new { " + ", ".join(f"x.{column}" for column in columns) + " }"


def array_literal(values: list[str]) -> str:
    """Renders a C# array literal, for example 'new[] { "Id", "TenantId" }'."""
    return "new[] { " + ", ".join(literal(value) for value in values) + " }"


def clr_type_name(clr_type: str) -> str:
    """Renders the short name of a CLR type, as the EF Core code generator does."""
    return {
        "System.Guid": "Guid",
        "System.DateTime": "DateTime",
        "System.String": "string",
        "System.Boolean": "bool",
        "System.Int32": "int",
        "System.Int64": "long",
        "System.Int16": "short",
        "System.Byte": "byte",
        "System.Single": "float",
        "System.Double": "double",
        "System.Decimal": "decimal",
    }.get(clr_type, clr_type.split(".")[-1])


def table_order(entities: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Sorts the tables so that a principal table is created before its dependent.

    EF Core uses a breadth first topological sort (Kahn's algorithm) over the
    foreign keys of the tables that are created by the migration. The same
    algorithm is implemented here, the model order is used as the tie breaker.
    """
    by_table = {entity["table"]: entity for entity in entities}
    successors: dict[str, list[str]] = {entity["table"]: [] for entity in entities}
    predecessors: dict[str, int] = {entity["table"]: 0 for entity in entities}

    for entity in entities:
        for foreign_key in entity["foreign_keys"]:
            principal = foreign_key["principal_table"]
            dependent = foreign_key["dependent"]
            if principal == dependent or principal not in by_table:
                continue
            successors[principal].append(dependent)
            predecessors[dependent] += 1

    queue = [entity["table"] for entity in entities if predecessors[entity["table"]] == 0]
    ordered: list[str] = []
    while queue:
        table = queue.pop(0)
        ordered.append(table)
        for successor in successors[table]:
            predecessors[successor] -= 1
            if predecessors[successor] == 0:
                queue.append(successor)

    if len(ordered) != len(entities):
        raise GenerationError("The foreign keys of the model contain a cycle.")

    return [by_table[table] for table in ordered]


def index_name(entity: dict[str, Any], index: dict[str, Any]) -> str:
    """The default name of an index: 'IX_{table}_{columns}'."""
    return "IX_{}_{}".format(entity["table"], "_".join(index["columns"]))


def primary_key_name(entity: dict[str, Any]) -> str:
    """The default name of a primary key: 'PK_{table}'."""
    return f"PK_{entity['table']}"


def alternate_key_name(entity: dict[str, Any], columns: list[str]) -> str:
    """The default name of an alternate key: 'AK_{table}_{columns}'."""
    return "AK_{}_{}".format(entity["table"], "_".join(columns))


def foreign_key_name(foreign_key: dict[str, Any]) -> str:
    """The default name of a foreign key: 'FK_{dependent}_{principal}_{columns}'."""
    return "FK_{}_{}_{}".format(
        foreign_key["dependent"],
        foreign_key["principal_table"],
        "_".join(foreign_key["columns"]),
    )


# --------------------------------------------------------------------------------------
# The migration
# --------------------------------------------------------------------------------------


def generate_create_table(entity: dict[str, Any], lines: list[str]) -> None:
    """Renders the 'CreateTable' operation of a table."""
    lines.append(f'{INDENT * 3}migrationBuilder.CreateTable(')
    lines.append(f'{INDENT * 4}name: {literal(entity["table"])},')
    lines.append(f"{INDENT * 4}columns: table => new")
    lines.append(f"{INDENT * 4}{{")
    for column in entity["columns"]:
        arguments = [f'type: {literal(column["store_type"])}']
        if column["unicode"] is False:
            arguments.append("unicode: false")
        if column["max_length"] is not None:
            arguments.append(f'maxLength: {column["max_length"]}')
        arguments.append(f'nullable: {"true" if column["nullable"] else "false"}')
        lines.append(
            f'{INDENT * 5}{column["name"]} = table.Column<{clr_type_name(column["provider_clr_type"])}>'
            f"({', '.join(arguments)}),"
        )
    lines.append(f"{INDENT * 4}}},")
    lines.append(f"{INDENT * 4}constraints: table =>")
    lines.append(f"{INDENT * 4}{{")
    if entity["primary_key"]:
        lines.append(
            f'{INDENT * 5}table.PrimaryKey({literal(primary_key_name(entity))}, '
            f"{lambda_expression(entity['primary_key'])});"
        )
    for alternate_key in entity["alternate_keys"]:
        lines.append(
            f'{INDENT * 5}table.UniqueConstraint({literal(alternate_key_name(entity, alternate_key))}, '
            f"{lambda_expression(alternate_key)});"
        )
    for foreign_key in entity["foreign_keys"]:
        lines.append(f"{INDENT * 5}table.ForeignKey(")
        column_argument = (
            "column" if len(foreign_key["columns"]) == 1 else "columns"
        )
        arguments = [
            f'name: {literal(foreign_key_name(foreign_key))}',
            f'{column_argument}: {lambda_expression(foreign_key["columns"])}',
            f'principalTable: {literal(foreign_key["principal_table"])}',
        ]
        # EF Core 10 expects the name of the principal column, not a lambda.
        if len(foreign_key["principal_columns"]) == 1:
            arguments.append(
                f'principalColumn: {literal(foreign_key["principal_columns"][0])}'
            )
        else:
            arguments.append(
                f'principalColumns: {array_literal(foreign_key["principal_columns"])}'
            )
        referential_action = DELETE_BEHAVIOR_TO_REFERENTIAL_ACTION.get(
            foreign_key["on_delete"]
        )
        if referential_action is not None:
            arguments.append(f"onDelete: ReferentialAction.{referential_action}")
        lines.append(f"{INDENT * 6}{arguments[0]},")
        for argument in arguments[1:]:
            lines.append(f"{INDENT * 6}{argument},")
        lines[-1] = lines[-1][: -1] + ");"
    lines.append(f"{INDENT * 4}}});")


def generate_create_index(entity: dict[str, Any], index: dict[str, Any], lines: list[str]) -> None:
    """Renders the 'CreateIndex' operation of an index."""
    lines.append(f"{INDENT * 3}migrationBuilder.CreateIndex(")
    arguments = [
        f'name: {literal(index_name(entity, index))}',
        f'table: {literal(entity["table"])}',
    ]
    if len(index["columns"]) == 1:
        arguments.append(f'column: {literal(index["columns"][0])}')
    else:
        arguments.append(f'columns: {array_literal(index["columns"])}')
    if index["unique"]:
        arguments.append("unique: true")
    if index["filter"]:
        arguments.append(f'filter: {literal(index["filter"])}')
    lines.append(f"{INDENT * 4}{arguments[0]},")
    for argument in arguments[1:]:
        lines.append(f"{INDENT * 4}{argument},")
    lines[-1] = lines[-1][: -1] + ")"
    if index.get("clustered") is False:
        lines.append(f'{INDENT * 4}.Annotation("SqlServer:Clustered", false);')
    else:
        lines[-1] = lines[-1] + ";"


def generate_migration(entities: list[dict[str, Any]]) -> str:
    """Renders the migration file with its 'Up' and 'Down' operations.

    Historical: this is the generator that produced the published
    ``20251017120000_InitialCreate`` migration. It is kept so that the published file
    stays reviewable against the model it was generated from; ``build_files`` no longer
    calls it, because a published migration is never rewritten.
    """
    ordered = table_order(entities)

    lines: list[str] = []
    lines.append("using System;")
    lines.append("using Microsoft.EntityFrameworkCore.Migrations;")
    lines.append("")
    lines.append("#nullable disable")
    lines.append("")
    lines.append(f"namespace {MIGRATION_NAMESPACE}")
    lines.append("{")
    lines.append(f"{INDENT}/// <inheritdoc />")
    lines.append(f"{INDENT}public partial class {MIGRATION_CLASS} : Migration")
    lines.append(f"{INDENT}{{")
    lines.append(f"{INDENT * 2}/// <inheritdoc />")
    lines.append(f"{INDENT * 2}protected override void Up(MigrationBuilder migrationBuilder)")
    lines.append(f"{INDENT * 2}{{")
    for entity in ordered:
        generate_create_table(entity, lines)
        lines.append("")
    for entity in ordered:
        for index in entity["indexes"]:
            generate_create_index(entity, index, lines)
            lines.append("")
    if lines[-1] == "":
        lines.pop()
    lines.append(f"{INDENT * 2}}}")
    lines.append("")
    lines.append(f"{INDENT * 2}/// <inheritdoc />")
    lines.append(f"{INDENT * 2}protected override void Down(MigrationBuilder migrationBuilder)")
    lines.append(f"{INDENT * 2}{{")
    for entity in reversed(ordered):
        lines.append(f"{INDENT * 3}migrationBuilder.DropTable(")
        lines.append(f"{INDENT * 4}name: {literal(entity['table'])});")
        if entity is not ordered[0]:
            lines.append("")
    lines.append(f"{INDENT * 2}}}")
    lines.append(f"{INDENT}}}")
    lines.append("}")
    return "\n".join(lines) + "\n"


# --------------------------------------------------------------------------------------
# The model
# --------------------------------------------------------------------------------------


def generate_model(entity_types: list[dict[str, Any]], indent: str) -> list[str]:
    """Renders the model of the snapshot and of the designer file.

    The shape follows the 'CSharpSnapshotGenerator' of EF Core 10.
    """
    lines: list[str] = []

    lines.append(f'{indent}modelBuilder.HasAnnotation("ProductVersion", "{PRODUCT_VERSION}");')

    for entity in entity_types:
        lines.append("")
        lines.append(f'{indent}modelBuilder.Entity({literal(entity["clr_name"])}, b =>')
        lines.append(f"{indent}{{")
        first = True
        for column in entity["columns"]:
            if not first:
                lines.append("")
            first = False
            provider_type = clr_type_name(column["snapshot_clr_type"])
            if column["nullable"] and provider_type not in {"string", "object"}:
                # A nullable value type is rendered with the nullable annotation.
                provider_type = f"{provider_type}?"
            lines.append(f'{indent}{INDENT}b.Property<{provider_type}>({literal(column["name"])})')
            if column["nullable"] is False and provider_type in {
                "string",
                "object",
            }:
                lines.append(f"{indent}{INDENT * 2}.IsRequired()")
            if value_generated_on_add(entity, column):
                lines.append(f"{indent}{INDENT * 2}.ValueGeneratedOnAdd()")
            if column["max_length"] is not None:
                lines.append(f"{indent}{INDENT * 2}.HasMaxLength({column['max_length']})")
            if column["unicode"] is not None:
                # 'CSharpSnapshotGenerator.GenerateFluentApiForIsUnicode' writes the
                # annotation whenever it is present, so '.IsUnicode(true)' belongs into the
                # snapshot as well. Without it the snapshot model reports 'IsUnicode == null'
                # while the model of the code base reports 'true'.
                lines.append(
                    f"{indent}{INDENT * 2}.IsUnicode({'true' if column['unicode'] else 'false'})"
                )
            column_type = column["snapshot_column_type"] or column["store_type"]
            lines.append(f"{indent}{INDENT * 2}.HasColumnType({literal(column_type)})")
            lines[-1] = lines[-1] + ";"

        if entity["primary_key"]:
            lines.append("")
            lines.append(
                f"{indent}{INDENT}b.HasKey("
                + ", ".join(literal(column) for column in entity["primary_key"])
                + ");"
            )
        for alternate_key in entity["alternate_keys"]:
            lines.append("")
            lines.append(
                f"{indent}{INDENT}b.HasAlternateKey("
                + ", ".join(literal(column) for column in alternate_key)
                + ");"
            )
        for index in entity["indexes"]:
            lines.append("")
            lines.append(
                f"{indent}{INDENT}b.HasIndex("
                + ", ".join(literal(column) for column in index["columns"])
                + ")"
            )
            if index["unique"]:
                lines.append(f"{indent}{INDENT * 2}.IsUnique()")
            if index.get("clustered") is False:
                lines.append(
                    f'{indent}{INDENT * 2}.HasAnnotation("SqlServer:Clustered", false)'
                )
            if index["filter"]:
                lines.append(f"{indent}{INDENT * 2}.HasFilter({literal(index['filter'])})")
            lines[-1] = lines[-1] + ";"

        lines.append("")
        lines.append(f'{indent}{INDENT}b.ToTable({literal(entity["table"])});')
        lines.append(f"{indent}}});")

    # The relationships of the dependent entities.
    for entity in entity_types:
        if not entity["foreign_keys"]:
            continue
        lines.append("")
        lines.append(f'{indent}modelBuilder.Entity({literal(entity["clr_name"])}, b =>')
        lines.append(f"{indent}{{")
        for foreign_key in entity["foreign_keys"]:
            principal = f'{literal(foreign_key["principal_entity"])}'
            if foreign_key["dependent_navigation"]:
                principal += f', {literal(foreign_key["dependent_navigation"])}'
            if foreign_key is not entity["foreign_keys"][0]:
                lines.append("")
            lines.append(f"{indent}{INDENT}b.HasOne({principal})")
            if foreign_key["principal_navigation"]:
                lines.append(
                    f"{indent}{INDENT * 2}.WithMany({literal(foreign_key['principal_navigation'])})"
                )
            else:
                lines.append(f"{indent}{INDENT * 2}.WithMany()")
            lines.append(
                f"{indent}{INDENT * 2}.HasForeignKey("
                + ", ".join(literal(column) for column in foreign_key["columns"])
                + ")"
            )
            principal_key = foreign_key["principal_columns"]
            principal_primary_key = next(
                (
                    candidate["primary_key"]
                    for candidate in entity_types
                    if candidate["clr_name"] == foreign_key["principal_entity"]
                ),
                None,
            )
            if principal_key != principal_primary_key:
                lines.append(
                    f"{indent}{INDENT * 2}.HasPrincipalKey("
                    + ", ".join(literal(column) for column in principal_key)
                    + ")"
                )
            if foreign_key["on_delete"] != "ClientSetNull":
                lines.append(
                    f"{indent}{INDENT * 2}.OnDelete(DeleteBehavior.{foreign_key['on_delete']})"
                )
            if foreign_key["required"]:
                lines.append(f"{indent}{INDENT * 2}.IsRequired()")
            lines[-1] = lines[-1] + ";"
        lines.append(f"{indent}}});")

    # The navigations of the principal entities.
    principal_navigations: dict[str, list[str]] = {}
    for entity in entity_types:
        for foreign_key in entity["foreign_keys"]:
            navigation = foreign_key["principal_navigation"]
            if not navigation:
                continue
            principal_navigations.setdefault(foreign_key["principal_entity"], []).append(
                navigation
            )

    for entity in entity_types:
        navigations = principal_navigations.get(entity["clr_name"], [])
        if not navigations:
            continue
        lines.append("")
        lines.append(f'{indent}modelBuilder.Entity({literal(entity["clr_name"])}, b =>')
        lines.append(f"{indent}{{")
        for navigation in navigations:
            lines.append(f'{indent}{INDENT}b.Navigation({literal(navigation)});')
        lines.append(f"{indent}}});")

    return lines


def generate_snapshot(entity_types: list[dict[str, Any]]) -> str:
    """Renders the model snapshot file."""
    lines: list[str] = []
    lines.append("// <auto-generated />")
    lines.append("using System;")
    lines.append("using Data;")
    lines.append("using Microsoft.EntityFrameworkCore;")
    lines.append("using Microsoft.EntityFrameworkCore.Infrastructure;")
    lines.append("using Microsoft.EntityFrameworkCore.Storage.ValueConversion;")
    lines.append("")
    lines.append("#nullable disable")
    lines.append("")
    lines.append(f"namespace {MIGRATION_NAMESPACE}")
    lines.append("{")
    lines.append(f"{INDENT}[DbContext(typeof({CONTEXT_TYPE}))]")
    lines.append(f"{INDENT}partial class {SNAPSHOT_CLASS} : ModelSnapshot")
    lines.append(f"{INDENT}{{")
    lines.append(f"{INDENT * 2}protected override void BuildModel(ModelBuilder modelBuilder)")
    lines.append(f"{INDENT * 2}{{")
    lines.append(f"{INDENT * 3}#pragma warning disable 612, 618")
    lines.extend(generate_model(entity_types, INDENT * 3))
    lines.append(f"{INDENT * 3}#pragma warning restore 612, 618")
    lines.append(f"{INDENT * 2}}}")
    lines.append(f"{INDENT}}}")
    lines.append("}")
    return "\n".join(lines) + "\n"


def generate_designer(entity_types: list[dict[str, Any]]) -> str:
    """Renders the designer file of the migration."""
    lines: list[str] = []
    lines.append("// <auto-generated />")
    lines.append("using System;")
    lines.append("using Data;")
    lines.append("using Microsoft.EntityFrameworkCore;")
    lines.append("using Microsoft.EntityFrameworkCore.Infrastructure;")
    lines.append("using Microsoft.EntityFrameworkCore.Migrations;")
    lines.append("using Microsoft.EntityFrameworkCore.Storage.ValueConversion;")
    lines.append("")
    lines.append("#nullable disable")
    lines.append("")
    lines.append(f"namespace {MIGRATION_NAMESPACE}")
    lines.append("{")
    lines.append(f"{INDENT}[DbContext(typeof({CONTEXT_TYPE}))]")
    lines.append(f"{INDENT}[Migration({literal(MIGRATION_ID + '_' + MIGRATION_NAME)})]")
    lines.append(f"{INDENT}partial class {MIGRATION_CLASS}")
    lines.append(f"{INDENT}{{")
    lines.append(f"{INDENT * 2}/// <inheritdoc />")
    lines.append(f"{INDENT * 2}protected override void BuildTargetModel(ModelBuilder modelBuilder)")
    lines.append(f"{INDENT * 2}{{")
    lines.append(f"{INDENT * 3}#pragma warning disable 612, 618")
    lines.extend(generate_model(entity_types, INDENT * 3))
    lines.append(f"{INDENT * 3}#pragma warning restore 612, 618")
    lines.append(f"{INDENT * 2}}}")
    lines.append(f"{INDENT}}}")
    lines.append("}")
    return "\n".join(lines) + "\n"


# --------------------------------------------------------------------------------------
# Entry point
# --------------------------------------------------------------------------------------


def generate_delta_migration(operations: list[dict[str, Any]]) -> str:
    """Renders the migration that applies the difference of the two models."""
    lines: list[str] = []
    lines.append("using System;")
    lines.append("using Microsoft.EntityFrameworkCore.Migrations;")
    lines.append("")
    lines.append("#nullable disable")
    lines.append("")
    lines.append(f"namespace {MIGRATION_NAMESPACE}")
    lines.append("{")
    lines.append(f"{INDENT}/// <inheritdoc />")
    lines.append(f"{INDENT}public partial class {DELTA_MIGRATION_CLASS} : Migration")
    lines.append(f"{INDENT}{{")
    lines.append(f"{INDENT * 2}/// <inheritdoc />")
    lines.append(f"{INDENT * 2}protected override void Up(MigrationBuilder migrationBuilder)")
    lines.append(f"{INDENT * 2}{{")
    for statement in DELTA_MIGRATION_PRELUDE:
        lines.append(f"{INDENT * 3}migrationBuilder.Sql({literal(statement)});")
        lines.append("")
    lines.extend(mm.render_up(operations, INDENT * 3))
    lines.append(f"{INDENT * 2}}}")
    lines.append("")
    lines.append(f"{INDENT * 2}/// <inheritdoc />")
    lines.append(f"{INDENT * 2}protected override void Down(MigrationBuilder migrationBuilder)")
    lines.append(f"{INDENT * 2}{{")
    lines.extend(mm.render_up(mm.reverse_operations(operations), INDENT * 3))
    lines.append(f"{INDENT * 2}}}")
    lines.append(f"{INDENT}}}")
    lines.append("}")
    return "\n".join(lines) + "\n"


def generate_delta_designer(entity_types: list[dict[str, Any]]) -> str:
    """Renders the designer file of the migration that applies the difference."""
    lines: list[str] = []
    lines.append("// <auto-generated />")
    lines.append("using System;")
    lines.append("using Data;")
    lines.append("using Microsoft.EntityFrameworkCore;")
    lines.append("using Microsoft.EntityFrameworkCore.Infrastructure;")
    lines.append("using Microsoft.EntityFrameworkCore.Migrations;")
    lines.append("using Microsoft.EntityFrameworkCore.Storage.ValueConversion;")
    lines.append("")
    lines.append("#nullable disable")
    lines.append("")
    lines.append(f"namespace {MIGRATION_NAMESPACE}")
    lines.append("{")
    lines.append(f"{INDENT}[DbContext(typeof({CONTEXT_TYPE}))]")
    lines.append(
        f"{INDENT}[Migration({literal(DELTA_MIGRATION_ID + '_' + DELTA_MIGRATION_NAME)})]"
    )
    lines.append(f"{INDENT}partial class {DELTA_MIGRATION_CLASS}")
    lines.append(f"{INDENT}{{")
    lines.append(f"{INDENT * 2}/// <inheritdoc />")
    lines.append(f"{INDENT * 2}protected override void BuildTargetModel(ModelBuilder modelBuilder)")
    lines.append(f"{INDENT * 2}{{")
    lines.append(f"{INDENT * 3}#pragma warning disable 612, 618")
    lines.extend(generate_model(entity_types, INDENT * 3))
    lines.append(f"{INDENT * 3}#pragma warning restore 612, 618")
    lines.append(f"{INDENT * 2}}}")
    lines.append(f"{INDENT}}}")
    lines.append("}")
    return "\n".join(lines) + "\n"


def compute_delta_operations(repository_root: str) -> list[dict[str, Any]]:
    """Computes the operations of the delta migration.

    The 'InitialCreate' migration is a published migration: it describes the schema as it
    was when it was written and it is never regenerated. The difference is therefore
    computed between the model of its designer file and the model of the code base, which
    is exactly the comparison that 'dotnet ef migrations has-pending-model-changes' makes
    between the model snapshot and the model.
    """
    model = derive_model(repository_root)
    current = mm.normalize_derived_model(model)
    designer_path = os.path.join(
        repository_root, MIGRATIONS_DIRECTORY, f"{MIGRATION_ID}_{MIGRATION_NAME}.Designer.cs"
    )
    published = mm.parse_generated_model(mm.read_text(designer_path))
    return mm.compute_operations(published, current)


def build_files(repository_root: str) -> dict[str, str]:
    """Builds the content of every generated file.

    The files of the published 'InitialCreate' migration are *not* part of the result.
    They are historical: like EF Core, this generator only writes the model snapshot and
    the files of the newest migration.
    """
    model = derive_model(repository_root)
    entities = model["entities"]

    # EF Core discovers the entity types of the DbSet properties first and the
    # entity types that are only known by a configuration afterwards.
    db_set_order = model.get("db_set_order", [])
    order = {clr_name: index for index, clr_name in enumerate(db_set_order)}
    entity_types = sorted(
        entities,
        key=lambda entity: (order.get(entity["clr_name"], len(order)), entity["clr_name"]),
    )

    return {
        f"{DELTA_MIGRATION_ID}_{DELTA_MIGRATION_NAME}.cs": generate_delta_migration(
            compute_delta_operations(repository_root)
        ),
        f"{DELTA_MIGRATION_ID}_{DELTA_MIGRATION_NAME}.Designer.cs": generate_delta_designer(
            entity_types
        ),
        f"{SNAPSHOT_CLASS}.cs": generate_snapshot(entity_types),
    }


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    parser.add_argument(
        "--check",
        action="store_true",
        help="Only verify that the committed files are up to date.",
    )
    arguments = parser.parse_args(argv)

    files = build_files(arguments.repo_root)
    directory = os.path.join(arguments.repo_root, MIGRATIONS_DIRECTORY)

    if arguments.check:
        problems = []
        for name, content in files.items():
            path = os.path.join(directory, name)
            if not os.path.exists(path):
                problems.append(f"{name} is missing.")
                continue
            with open(path, encoding="utf-8") as file:
                if file.read() != content:
                    problems.append(f"{name} is out of date.")
        if problems:
            for problem in problems:
                print(f"error: {problem}", file=sys.stderr)
            return 1
        print("The generated migrations are up to date.")
        return 0

    os.makedirs(directory, exist_ok=True)
    for name, content in files.items():
        path = os.path.join(directory, name)
        with open(path, "w", encoding="utf-8", newline="\n") as file:
            file.write(content)
        print(f"wrote {path}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except (ModelExtractionError, GenerationError, MigrationModelError) as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(2)
