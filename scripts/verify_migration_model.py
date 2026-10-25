#!/usr/bin/env python3
"""Verifies the generated migrations and the model snapshot against the EF Core model.

The script replaces the checks that normally need the .NET SDK:

* ``dotnet ef migrations has-pending-model-changes`` is approximated by comparing the
  model that the snapshot describes with the model of the code base. It is the check
  whose failure produces the ``PendingModelChangesWarning`` of
  ``Migrator.MigrateAsync`` at runtime, and it compares exactly the facets that
  ``MigrationsModelDiffer`` compares.
* The published ``InitialCreate`` migration is verified against its own designer file: a
  published migration is historical, it must describe the schema as it was when it was
  written and not the schema of today.
* The newest migration is verified against the difference between the migration before
  it and the snapshot: it has to apply exactly that difference, no more and no less.
* The C# syntax of every generated file is validated with tree-sitter.

Usage:
    python3 scripts/verify_migration_model.py --repo-root .
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from typing import Any

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import migration_model as mm  # noqa: E402
from ef_model import ModelExtractionError, derive_model  # noqa: E402
from migration_model import MigrationModelError  # noqa: E402

MIGRATIONS_DIRECTORY = os.path.join("Infrastructure", "Persistence", "Migrations")

INITIAL_MIGRATION = "20251017120000_InitialCreate"
PREVIOUS_MIGRATION = "20261023190000_AddUserDefaultCompany"
NEWEST_MIGRATION = "20261023191000_AddCompanyCustomerBoundary"
SNAPSHOT_FILE = "DatabaseContextModelSnapshot.cs"


class VerificationError(Exception):
    """Raised when the generated files do not match the model."""


# --------------------------------------------------------------------------------------
# Parsing of the migration files
# --------------------------------------------------------------------------------------


def parse_columns(body: str) -> list[dict[str, Any]]:
    """Reads the columns of a 'CreateTable' operation."""
    columns: list[dict[str, Any]] = []
    for match in re.finditer(r"(?P<property>\w+) = table\.Column<(?P<type>[\w\?]+)>\(", body):
        arguments, _ = mm.balanced_call(body, match.end() - 1)
        columns.append(
            {
                "name": match.group("property"),
                "clr_type": mm.CLR_TYPE_NAMES[match.group("type").rstrip("?")],
                "nullable": "nullable: true" in arguments,
                "type": re.search(r'type: "([^"]+)"', arguments).group(1),
                "max_length": mm.optional_int(re.search(r"maxLength: (\d+)", arguments)),
                "unicode": "unicode: false" not in arguments,
            }
        )
    return columns


def parse_create_tables(source: str) -> dict[str, dict[str, Any]]:
    """Reads the tables that a migration creates."""
    tables: dict[str, dict[str, Any]] = {}
    for match in re.finditer(
        r"migrationBuilder\.CreateTable\(\s*name: \"(?P<name>[^\"]+)\",(?P<body>.*?)\n\s*\}\);",
        source,
        re.DOTALL,
    ):
        body = match.group("body")
        foreign_keys = []
        for foreign_key in re.finditer(r"table\.ForeignKey\(", body):
            arguments, _ = mm.balanced_call(body, foreign_key.end() - 1)
            values = {}
            for argument in mm.split_arguments(arguments):
                key, _, value = argument.partition(":")
                values[key.strip()] = value.strip()
            foreign_keys.append(
                {
                    "name": re.search(r'"([^"]*)"', values.get("name", "")).group(1),
                    "columns": mm.parse_lambda_columns(
                        values.get("column", values.get("columns", ""))
                    ),
                }
            )
        tables[match.group("name")] = {
            "columns": parse_columns(body),
            "primary_key": mm.parse_lambda_call(body, "table.PrimaryKey"),
            "unique_constraints": [
                mm.parse_lambda_call(body, "table.UniqueConstraint", position)
                for position, _ in enumerate(
                    re.finditer(r"table\.UniqueConstraint\(", body)
                )
            ],
            "foreign_keys": foreign_keys,
        }
    return tables


def parse_operations(source: str) -> list[tuple[Any, ...]]:
    """Reads the operations of the 'Up' method in the order in which they are applied."""
    # The 'Down' method applies the operations in the opposite direction; it is verified
    # by 'signatures_of(reverse_operations(...))' and not scanned here.
    down = source.find("protected override void Down(")
    if down >= 0:
        source = source[:down]

    operations: list[tuple[int, tuple[Any, ...]]] = []

    for position, match in enumerate(
        re.finditer(r"migrationBuilder\.(?P<kind>\w+)(?:<[^>]*>)?\(", source)
    ):
        kind = match.group("kind")
        arguments, _ = mm.balanced_call(source, match.end() - 1)

        def argument(name: str) -> str | None:
            found = re.search(rf"(?<![\w]){name}: (?P<value>[^\n]+)", arguments)
            return found.group("value").strip().rstrip(",") if found else None

        if kind == "AddUniqueConstraint":
            columns = argument("column") or argument("columns") or ""
            operations.append(
                (
                    position,
                    (
                        "add_unique_constraint",
                        _string(argument("table")),
                        _string(argument("name")),
                        tuple(mm.parse_literal_list(columns)),
                    ),
                )
            )
        elif kind == "DropIndex":
            operations.append(
                (
                    position,
                    (
                        "drop_index",
                        _string(argument("table")),
                        _string(argument("name")),
                    ),
                )
            )
        elif kind == "CreateIndex":
            columns = argument("column") or argument("columns") or ""
            operations.append(
                (
                    position,
                    (
                        "create_index",
                        _string(argument("table")),
                        _string(argument("name")),
                        tuple(mm.parse_literal_list(columns)),
                        argument("unique") == "true",
                        _string(argument("filter")),
                    ),
                )
            )
        elif kind == "AddColumn":
            operations.append(
                (
                    position,
                    (
                        "add_column",
                        _string(argument("table")),
                        _string(argument("name")),
                        _string(argument("type")),
                        argument("nullable") == "true",
                    ),
                )
            )
        elif kind == "AlterColumn":
            operations.append(
                (
                    position,
                    (
                        "alter_column",
                        _string(argument("table")),
                        _string(argument("name")),
                        _string(argument("type")),
                        argument("nullable") == "true",
                        _string(argument("oldType")),
                        argument("oldNullable") == "true",
                    ),
                )
            )
        elif kind in {
            "CreateTable",
            "DropTable",
            "AddForeignKey",
            "DropForeignKey",
            "Sql",
        }:
            # 'CreateTable'/'DropTable' are verified by 'verify_initial_migration'; the
            # 'Sql' statements of the prelude are written down by the generator and are
            # verified byte by byte by 'generate_migrations.py --check'.
            continue
        else:
            raise VerificationError(
                f"The migration contains an operation that this script does not "
                f"understand: '{kind}'."
            )

    return [signature for _, signature in sorted(operations, key=lambda item: item[0])]


def _string(value: str | None) -> str | None:
    if value is None:
        return None
    match = re.match(r'"(.*)"$', value)
    return match.group(1) if match else value


def signatures_of(operations: list[dict[str, Any]]) -> list[tuple[Any, ...]]:
    """Renders the computed operations as the signatures that 'parse_operations' reads."""
    signatures: list[tuple[Any, ...]] = []
    for operation in operations:
        if operation["kind"] == "drop_index":
            signatures.append(
                (
                    "drop_index",
                    operation["table"],
                    mm.index_name(operation["table"], operation["index"]),
                )
            )
        elif operation["kind"] == "add_unique_constraint":
            signatures.append(
                (
                    "add_unique_constraint",
                    operation["table"],
                    "AK_{}_{}".format(operation["table"], "_".join(operation["columns"])),
                    tuple(operation["columns"]),
                )
            )
        elif operation["kind"] == "create_index":
            index = operation["index"]
            signatures.append(
                (
                    "create_index",
                    operation["table"],
                    mm.index_name(operation["table"], index),
                    tuple(index["columns"]),
                    bool(index["unique"]),
                    index["filter"] or None,
                )
            )
        elif operation["kind"] == "add_column":
            column = operation["column"]
            signatures.append(
                (
                    "add_column",
                    operation["table"],
                    operation["name"],
                    column["column_type"],
                    bool(column["nullable"]),
                )
            )
        else:
            column = operation["column"]
            old_column = operation["old_column"]
            signatures.append(
                (
                    "alter_column",
                    operation["table"],
                    operation["name"],
                    column["column_type"],
                    bool(column["nullable"]),
                    old_column["column_type"],
                    bool(old_column["nullable"]),
                )
            )
    return signatures


# --------------------------------------------------------------------------------------
# The checks
# --------------------------------------------------------------------------------------


def verify_model_against_generated(
    model: dict[str, Any], generated: dict[str, Any], description: str
) -> list[str]:
    """Compares the model of the code base with a generated snapshot or designer file."""
    problems: list[str] = []
    expected = mm.normalize_derived_model(model)

    if set(expected["entities"]) != set(generated["entities"]):
        problems.append(
            f"The entity types of the {description} differ from the model: "
            f"missing {sorted(set(expected['entities']) - set(generated['entities'])) or 'none'}, "
            f"unknown {sorted(set(generated['entities']) - set(expected['entities'])) or 'none'}."
        )
        return problems

    for clr_name in sorted(expected["entities"]):
        wanted = expected["entities"][clr_name]
        described = generated["entities"][clr_name]

        if described["table"] != wanted["table"]:
            problems.append(
                f"The table of '{clr_name}' is '{described['table']}' in the {description} "
                f"but '{wanted['table']}' in the model."
            )
        if described["primary_key"] != wanted["primary_key"]:
            problems.append(
                f"The primary key of '{clr_name}' is {described['primary_key']} in the "
                f"{description} but {wanted['primary_key']} in the model."
            )
        if described["alternate_keys"] != wanted["alternate_keys"]:
            problems.append(
                f"The alternate keys of '{clr_name}' differ between the {description} and "
                "the model."
            )

        wanted_columns = {column["name"]: column for column in wanted["columns"]}
        described_columns = {column["name"]: column for column in described["columns"]}
        for name in sorted(set(wanted_columns) - set(described_columns)):
            problems.append(f"The property '{clr_name}.{name}' is missing in the {description}.")
        for name in sorted(set(described_columns) - set(wanted_columns)):
            problems.append(f"The property '{clr_name}.{name}' of the {description} is unknown.")
        for name in sorted(set(wanted_columns) & set(described_columns)):
            column = wanted_columns[name]
            actual = described_columns[name]
            for facet in (
                "clr_type",
                "nullable",
                "max_length",
                "unicode",
                "value_generated",
                "column_type",
            ):
                if column[facet] != actual[facet]:
                    problems.append(
                        f"The {facet} of '{clr_name}.{name}' is '{actual[facet]}' in the "
                        f"{description} but '{column[facet]}' in the model."
                    )

        wanted_indexes = {
            tuple(index["columns"]): index for index in wanted["indexes"]
        }
        described_indexes = {
            tuple(index["columns"]): index for index in described["indexes"]
        }
        for key in sorted(set(wanted_indexes) - set(described_indexes)):
            problems.append(f"The index {key} of '{clr_name}' is missing in the {description}.")
        for key in sorted(set(described_indexes) - set(wanted_indexes)):
            problems.append(f"The index {key} of '{clr_name}' of the {description} is unknown.")
        for key in sorted(set(wanted_indexes) & set(described_indexes)):
            index = wanted_indexes[key]
            actual = described_indexes[key]
            for facet in ("unique", "filter", "clustered"):
                if (index[facet] or None) != (actual[facet] or None):
                    problems.append(
                        f"The {facet} of the index {key} of '{clr_name}' is "
                        f"'{actual[facet]}' in the {description} but '{index[facet]}' in "
                        "the model."
                    )

    if mm._normalize_relationships(expected["relationships"]) != mm._normalize_relationships(
        generated["relationships"]
    ):
        problems.append(
            f"The relationships of the {description} differ from the model."
        )

    return problems


def verify_initial_migration(migration_source: str, designer: dict[str, Any]) -> list[str]:
    """The published migration must create the schema of its own designer file."""
    problems: list[str] = []
    migration = parse_create_tables(migration_source)
    tables = {
        entity["table"]: entity for entity in designer["entities"].values()
    }

    for table in sorted(set(tables) - set(migration)):
        problems.append(f"The table '{table}' of the designer file is not created.")
    for table in sorted(set(migration) - set(tables)):
        problems.append(f"The migration creates the unknown table '{table}'.")

    for table in sorted(set(tables) & set(migration)):
        entity = tables[table]
        created = migration[table]

        wanted_columns = {column["name"]: column for column in entity["columns"]}
        actual_columns = {column["name"]: column for column in created["columns"]}
        for name in sorted(set(wanted_columns) - set(actual_columns)):
            problems.append(f"The column '{table}.{name}' is not created by the migration.")
        for name in sorted(set(actual_columns) - set(wanted_columns)):
            problems.append(f"The migration creates the unknown column '{table}.{name}'.")
        for name in sorted(set(wanted_columns) & set(actual_columns)):
            column = wanted_columns[name]
            actual = actual_columns[name]
            if column["column_type"] != actual["type"]:
                problems.append(
                    f"The type of '{table}.{name}' is '{actual['type']}' in the migration "
                    f"but '{column['column_type']}' in the designer file."
                )
            if bool(column["nullable"]) != bool(actual["nullable"]):
                problems.append(
                    f"The nullability of '{table}.{name}' differs between the migration "
                    "and the designer file."
                )
            if column["max_length"] != actual["max_length"]:
                problems.append(
                    f"The maximum length of '{table}.{name}' differs between the migration "
                    "and the designer file."
                )
            if column["clr_type"] != actual["clr_type"]:
                problems.append(
                    f"The provider type of '{table}.{name}' differs between the migration "
                    "and the designer file."
                )

        if created["primary_key"] != entity["primary_key"]:
            problems.append(
                f"The primary key of '{table}' is {created['primary_key']} in the "
                f"migration but {entity['primary_key']} in the designer file."
            )
        for alternate_key in entity["alternate_keys"]:
            if alternate_key not in created["unique_constraints"]:
                problems.append(
                    f"The alternate key {alternate_key} of '{table}' is missing in the "
                    "migration."
                )

    created_indexes = {
        (index["table"], tuple(index["columns"])): index
        for index in parse_create_indexes(migration_source)
    }
    wanted_indexes = {
        (entity["table"], tuple(index["columns"])): index
        for entity in designer["entities"].values()
        for index in entity["indexes"]
    }
    for key in sorted(set(wanted_indexes) - set(created_indexes)):
        problems.append(f"The index '{key[0]}.{key[1]}' is missing in the migration.")
    for key in sorted(set(created_indexes) - set(wanted_indexes)):
        problems.append(f"The index '{key[0]}.{key[1]}' of the migration is unknown.")
    for key in sorted(set(wanted_indexes) & set(created_indexes)):
        index = wanted_indexes[key]
        actual = created_indexes[key]
        if bool(index["unique"]) != bool(actual["unique"]):
            problems.append(f"The uniqueness of the index '{key[0]}.{key[1]}' differs.")
        if (index["filter"] or None) != (actual["filter"] or None):
            problems.append(
                f"The filter of the index '{key[0]}.{key[1]}' is '{actual['filter']}' in "
                f"the migration but '{index['filter']}' in the designer file."
            )

    return problems


def verify_migration_with_added_tables(
    model: dict[str, Any],
    previous_designer: dict[str, Any],
    snapshot: dict[str, Any],
    newest_designer: dict[str, Any],
    migration_source: str,
) -> list[str]:
    """Verifies a migration that adds tables; such a migration is written with 'dotnet ef'.

    * The new tables (columns, keys, alternate keys, indexes) must be created exactly as the
      designer file describes them, and with exactly the foreign keys of the model.
    * Every other operation must be the difference of the tables that already existed
      (column/index changes and added alternate keys), no more and no less.
    """
    problems: list[str] = []
    operations, added_entities, added_relationships = (
        mm.compute_operations_with_added_tables(previous_designer, snapshot)
    )
    added_tables = {entity["table"] for entity in added_entities}

    # The new tables against the designer file of the migration.
    new_tables_designer = {
        "entities": {
            name: entity
            for name, entity in newest_designer["entities"].items()
            if entity["table"] in added_tables
        }
    }
    created = parse_create_tables(migration_source)
    unexpected = sorted(set(created) - added_tables)
    for table in unexpected:
        problems.append(f"The migration creates the table '{table}' that already existed.")
    # The indexes of the tables that already existed are verified as operations below.
    chunks = re.split(r"(?=migrationBuilder\.)", migration_source)
    filtered_source = "".join(
        chunk
        for chunk in chunks
        if not chunk.startswith("migrationBuilder.CreateIndex(")
        or re.search(r'table: "([^"]+)"', chunk).group(1) in added_tables
    )
    problems.extend(verify_initial_migration(filtered_source, new_tables_designer))

    # The foreign keys of the new tables against the model of the code base.
    wanted_foreign_keys = sorted(
        "FK_{}_{}_{}".format(
            foreign_key["dependent"],
            foreign_key["principal_table"],
            "_".join(foreign_key["columns"]),
        )
        for foreign_key in model["foreign_keys"]
        if foreign_key["dependent"] in added_tables
    )
    actual_foreign_keys = sorted(
        foreign_key["name"]
        for table in added_tables & set(created)
        for foreign_key in created[table]["foreign_keys"]
    )
    if wanted_foreign_keys != actual_foreign_keys:
        problems.append(
            "The foreign keys of the new tables differ from the model.\n"
            f"  created: {actual_foreign_keys}\n  wanted:  {wanted_foreign_keys}"
        )
    if added_relationships != len(wanted_foreign_keys):
        problems.append(
            f"{added_relationships} relationship(s) were added to the model but the new "
            f"tables declare {len(wanted_foreign_keys)} foreign key(s); a relationship of "
            "an existing table changed."
        )

    # Down undoes Up: it drops every new table and every alternate key that Up added to an
    # existing table (the raw SQL of Down is checked by 'generate_migrations.py --check').
    down_start = migration_source.find("protected override void Down(")
    down_source = migration_source[down_start:] if down_start >= 0 else ""
    dropped_tables = set(re.findall(r'migrationBuilder\.DropTable\(\s*name: "([^"]+)"', down_source))
    if dropped_tables != added_tables:
        problems.append(
            "Down does not drop exactly the new tables.\n"
            f"  dropped: {sorted(dropped_tables)}\n  wanted:  {sorted(added_tables)}"
        )
    for operation in operations:
        if operation["kind"] == "add_unique_constraint":
            name = "AK_{}_{}".format(operation["table"], "_".join(operation["columns"]))
            if f'name: "{name}"' not in down_source or "DropUniqueConstraint(" not in down_source:
                problems.append(f"Down does not drop the unique constraint '{name}'.")

    # The operations on the tables that already existed.
    expected = sorted(signatures_of(operations))
    actual = sorted(
        signature
        for signature in parse_operations(migration_source)
        if not (signature[0] == "create_index" and signature[1] in added_tables)
    )
    if actual != expected:
        problems.append(
            "The operations of the newest migration on the existing tables differ from "
            f"the difference of the models.\n  applied:   {actual}\n  expected:  {expected}"
        )
    return problems


def parse_create_indexes(source: str) -> list[dict[str, Any]]:
    """Reads the 'CreateIndex' operations of a migration."""
    indexes: list[dict[str, Any]] = []
    for match in re.finditer(r"migrationBuilder\.CreateIndex\(", source):
        arguments, _ = mm.balanced_call(source, match.end() - 1)
        columns = re.search(r"column(?:s)?: ([^\n]+)", arguments).group(1)
        indexes.append(
            {
                "name": re.search(r'name: "([^"]*)"', arguments).group(1),
                "table": re.search(r'table: "([^"]*)"', arguments).group(1),
                "columns": mm.parse_literal_list(columns),
                "unique": "unique: true" in arguments,
                "filter": mm.optional_string(re.search(r'filter: "([^"]*)"', arguments)),
            }
        )
    return indexes


def verify_syntax(paths: list[str]) -> list[str]:
    """Validates the C# syntax of the given files with tree-sitter."""
    try:
        import tree_sitter
        import tree_sitter_c_sharp
    except ImportError:  # pragma: no cover - the parser is optional
        return ["tree-sitter is not installed, the C# syntax was not validated."]

    language = tree_sitter.Language(tree_sitter_c_sharp.language())
    parser = tree_sitter.Parser(language)
    problems: list[str] = []
    for path in paths:
        with open(path, "rb") as file:
            tree = parser.parse(file.read())
        errors = []
        stack = [tree.root_node]
        while stack:
            node = stack.pop()
            if node.type == "ERROR" or node.is_missing:
                errors.append((node.start_point, node.end_point, node.type))
            stack.extend(node.children)
        for start, end, node_type in errors:
            problems.append(
                f"{path}:{start.row + 1}:{start.column + 1}: a C# syntax error "
                f"({node_type}) was found."
            )
    return problems


def verify(repository_root: str) -> list[str]:
    """Runs every check and returns the problems that were found."""
    directory = os.path.join(repository_root, MIGRATIONS_DIRECTORY)
    initial_migration_path = os.path.join(directory, f"{INITIAL_MIGRATION}.cs")
    initial_designer_path = os.path.join(directory, f"{INITIAL_MIGRATION}.Designer.cs")
    previous_migration_path = os.path.join(directory, f"{PREVIOUS_MIGRATION}.cs")
    previous_designer_path = os.path.join(directory, f"{PREVIOUS_MIGRATION}.Designer.cs")
    newest_migration_path = os.path.join(directory, f"{NEWEST_MIGRATION}.cs")
    newest_designer_path = os.path.join(directory, f"{NEWEST_MIGRATION}.Designer.cs")
    snapshot_path = os.path.join(directory, SNAPSHOT_FILE)

    problems: list[str] = []
    for path in (
        initial_migration_path,
        initial_designer_path,
        previous_migration_path,
        previous_designer_path,
        newest_migration_path,
        newest_designer_path,
        snapshot_path,
    ):
        if not os.path.exists(path):
            problems.append(f"{path} is missing.")
    if problems:
        return problems

    model = derive_model(repository_root)
    snapshot = mm.parse_generated_model(mm.read_text(snapshot_path))
    initial_designer = mm.parse_generated_model(mm.read_text(initial_designer_path))
    previous_designer = mm.parse_generated_model(mm.read_text(previous_designer_path))
    newest_designer = mm.parse_generated_model(mm.read_text(newest_designer_path))

    # 1. The snapshot has to describe the model of the code base. This is the check that
    #    'dotnet ef migrations has-pending-model-changes' performs.
    problems.extend(
        verify_model_against_generated(model, snapshot, "model snapshot")
    )

    # 2. The designer file of the newest migration describes the model after that
    #    migration, which is the model of the snapshot.
    problems.extend(
        verify_model_against_generated(
            model, newest_designer, "designer file of the newest migration"
        )
    )

    # 3. The published migration creates the schema of its own designer file.
    problems.extend(
        verify_initial_migration(mm.read_text(initial_migration_path), initial_designer)
    )

    # 4. The newest migration applies exactly the difference between the schema of the
    #    migration before it and the schema of the snapshot.
    newest_source = mm.read_text(newest_migration_path)
    added_tables = set(snapshot["entities"]) - set(previous_designer["entities"])
    if added_tables:
        problems.extend(
            verify_migration_with_added_tables(
                model, previous_designer, snapshot, newest_designer, newest_source
            )
        )
        return problems

    expected = signatures_of(mm.compute_operations(previous_designer, snapshot))
    actual = parse_operations(newest_source)
    if actual != expected:
        problems.append(
            "The operations of the newest migration differ from the difference between "
            f"the previous migration and the snapshot.\n  applied:   {actual}\n"
            f"  expected:  {expected}"
        )

    return problems


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    arguments = parser.parse_args(argv)

    directory = os.path.join(arguments.repo_root, MIGRATIONS_DIRECTORY)
    syntax_problems = verify_syntax(
        [
            os.path.join(directory, f"{INITIAL_MIGRATION}.cs"),
            os.path.join(directory, f"{INITIAL_MIGRATION}.Designer.cs"),
            os.path.join(directory, f"{PREVIOUS_MIGRATION}.cs"),
            os.path.join(directory, f"{PREVIOUS_MIGRATION}.Designer.cs"),
            os.path.join(directory, f"{NEWEST_MIGRATION}.cs"),
            os.path.join(directory, f"{NEWEST_MIGRATION}.Designer.cs"),
            os.path.join(directory, SNAPSHOT_FILE),
        ]
    )
    problems = verify(arguments.repo_root)

    for problem in syntax_problems + problems:
        print(f"error: {problem}", file=sys.stderr)

    if syntax_problems or problems:
        print(
            f"The verification failed with {len(syntax_problems) + len(problems)} problem(s).",
            file=sys.stderr,
        )
        return 1

    print("The C# syntax of the generated files is valid.")
    print("The model snapshot and the migrations match the EF Core model of the code base.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except (ModelExtractionError, VerificationError, MigrationModelError) as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(2)
