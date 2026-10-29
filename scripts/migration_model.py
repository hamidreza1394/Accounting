#!/usr/bin/env python3
"""Reads the generated EF Core files back and compares two of them.

The scripts of this repository generate the migrations without a .NET SDK, therefore
they also have to be able to read the files that they generated. This module holds the
shared pieces:

* ``parse_generated_model`` reads a model snapshot or the designer file of a migration
  back into the same shape that ``ef_model.derive_model`` produces, and
* ``compute_operations`` compares two such models and returns the migration operations
  that turn the first one into the second one.

``compute_operations`` is deliberately narrow. It understands the operations that the
model of this repository needs and it *fails* for everything else, so that a difference
it does not understand can never silently disappear from a migration.

The comparison is intentionally limited to the facets that the EF Core
``MigrationsModelDiffer`` compares (see ``ColumnStructureEquals`` and
``IndexStructureEquals`` of ``MigrationsModelDiffer`` in ``dotnet/efcore``), because a
difference in a facet that the differ ignores does not produce a migration operation and
must not produce one here either.
"""

from __future__ import annotations

import re
from collections import Counter
from typing import Any

# The CLR types that the generated files use, mapped to their full name.
CLR_TYPE_NAMES = {
    "Guid": "System.Guid",
    "DateTime": "System.DateTime",
    "string": "System.String",
    "bool": "System.Boolean",
    "int": "System.Int32",
    "long": "System.Int64",
    "short": "System.Int16",
    "byte": "System.Byte",
    "byte[]": "System.Byte[]",
    "float": "System.Single",
    "double": "System.Double",
    "decimal": "System.Decimal",
}


class MigrationModelError(Exception):
    """Raised when a generated file contains something this module does not understand."""


# --------------------------------------------------------------------------------------
# Small helpers
# --------------------------------------------------------------------------------------


def read_text(path: str) -> str:
    with open(path, encoding="utf-8-sig") as file:
        return file.read()


def balanced_call(source: str, start: int) -> tuple[str, int]:
    """Returns the text of a call and the position after its closing parenthesis."""
    depth = 0
    for position in range(start, len(source)):
        character = source[position]
        if character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
            if depth == 0:
                return source[start + 1 : position], position + 1
    raise MigrationModelError("Unbalanced parenthesis in the generated code.")


def split_arguments(text: str) -> list[str]:
    """Splits the arguments of a call at the commas that are not nested."""
    arguments: list[str] = []
    depth = 0
    current: list[str] = []
    in_string = False
    for character in text:
        if character == '"' and not in_string:
            in_string = True
        elif character == '"' and in_string:
            in_string = False
        if not in_string:
            if character in "({[":
                depth += 1
            elif character in ")}]":
                depth -= 1
            elif character == "," and depth == 0:
                arguments.append("".join(current).strip())
                current = []
                continue
        current.append(character)
    if current:
        arguments.append("".join(current).strip())
    return arguments


def optional_int(match: re.Match[str] | None) -> int | None:
    return int(match.group(1)) if match else None


def optional_string(match: re.Match[str] | None) -> str | None:
    return match.group(1) if match else None


def parse_enum(text: str, prefix: str) -> str | None:
    match = re.search(re.escape(prefix) + r"(\w+)", text)
    return match.group(1) if match else None


def parse_literal_list(text: str) -> list[str]:
    return re.findall(r'"([^"]*)"', text)


def parse_lambda_call(body: str, name: str, occurrence: int = 0) -> list[str]:
    """Reads the columns of a call such as 'table.PrimaryKey("PK_x", x => new { x.A, x.B })'."""
    for position, match in enumerate(re.finditer(re.escape(name) + r"\(", body)):
        if position != occurrence:
            continue
        arguments, _ = balanced_call(body, match.end() - 1)
        parts = split_arguments(arguments)
        return parse_lambda_columns(parts[-1] if parts else "")
    return []


def parse_lambda_columns(text: str) -> list[str]:
    text = text.strip()
    if "new {" in text:
        return re.findall(r"(\w+)\s*(?:,|\}|$)", text.split("new {", 1)[1])
    match = re.search(r"(\w+)\s*$", text)
    return [match.group(1)] if match else []


# --------------------------------------------------------------------------------------
# The generated model
# --------------------------------------------------------------------------------------


def _parse_unicode(calls: str) -> bool | None:
    """Reads the unicode flag of a property.

    'CSharpSnapshotGenerator.GenerateFluentApiForIsUnicode' writes the annotation whenever
    it is present, so the three states 'not configured', 'true' and 'false' are all
    visible in the generated file.
    """
    match = re.search(r"\.IsUnicode\((?P<value>true|false)\)", calls)
    if match is None:
        return None
    return match.group("value") == "true"


def parse_generated_model(source: str) -> dict[str, Any]:
    """Reads the entity types, the keys, the indexes and the relationships.

    The result has the shape of ``ef_model.derive_model`` so that a generated file and
    the model of the code base can be compared directly.
    """
    entities: dict[str, dict[str, Any]] = {}

    for match in re.finditer(
        r'modelBuilder\.Entity\("(?P<name>[^"]+)", b =>\s*\{(?P<body>.*?)\n\s*\}\);',
        source,
        re.DOTALL,
    ):
        name = match.group("name")
        body = match.group("body")

        properties: list[dict[str, Any]] = []
        for prop in re.finditer(
            r'b\.Property<(?P<type>[\w\[\]\?]+)>\("(?P<name>[^"]+)"\)(?P<calls>[^;]*);',
            body,
        ):
            calls = prop.group("calls")
            is_nullable_type = prop.group("type").endswith("?")
            is_reference_type = prop.group("type").rstrip("?") == "string"
            properties.append(
                {
                    "name": prop.group("name"),
                    "clr_type": CLR_TYPE_NAMES[prop.group("type").rstrip("?")],
                    "nullable": (
                        is_nullable_type
                        or (is_reference_type and ".IsRequired()" not in calls)
                    ),
                    "max_length": optional_int(re.search(r"HasMaxLength\((\d+)\)", calls)),
                    "unicode": _parse_unicode(calls),
                    "value_generated": bool(re.search(r"\.ValueGeneratedOnAdd\(\)", calls)),
                    "column_type": optional_string(
                        re.search(r'HasColumnType\("([^"]*)"\)', calls)
                    ),
                }
            )

        keys = [
            parse_literal_list(match.group(1))
            for match in re.finditer(r"b\.HasKey\(([^)]*)\);", body)
        ]
        alternate_keys = [
            parse_literal_list(match.group(1))
            for match in re.finditer(r"b\.HasAlternateKey\(([^)]*)\);", body)
        ]

        indexes = []
        for index in re.finditer(
            r"b\.HasIndex\((?P<columns>[^)]*)\)(?P<calls>[^;]*);", body
        ):
            calls = index.group("calls")
            indexes.append(
                {
                    "columns": parse_literal_list(index.group("columns")),
                    "unique": ".IsUnique()" in calls,
                    "filter": optional_string(
                        re.search(r'HasFilter\("([^"]*)"\)', calls)
                    ),
                    "clustered": (
                        False
                        if '"SqlServer:Clustered", false' in calls
                        else None
                    ),
                }
            )

        table = optional_string(re.search(r'b\.ToTable\("([^"]*)"\)', body))
        described = entities.setdefault(
            name,
            {
                "clr_name": name,
                "table": None,
                "columns": [],
                "primary_key": [],
                "alternate_keys": [],
                "indexes": [],
            },
        )
        described["columns"].extend(properties)
        if keys:
            described["primary_key"] = keys[0]
        described["alternate_keys"].extend(alternate_keys)
        described["indexes"].extend(indexes)
        if table is not None:
            described["table"] = table

    relationships: list[dict[str, Any]] = []
    for entity_match in re.finditer(
        r'modelBuilder\.Entity\("(?P<name>[^"]+)", b =>\s*\{(?P<body>.*?)\n\s*\}\);',
        source,
        re.DOTALL,
    ):
        for match in re.finditer(
            r'b\.HasOne\("(?P<principal>[^"]+)"(?:, (?:"(?P<navigation>[^"]+)"|null))?\)\s*'
            r"(?P<calls>.*?);",
            entity_match.group("body"),
            re.DOTALL,
        ):
            calls = match.group("calls")
            foreign_key = re.search(r"\.HasForeignKey\((?P<columns>[^)]*)\)", calls)
            if foreign_key is None:
                continue
            principal_key = re.search(r"\.HasPrincipalKey\((?P<columns>[^)]*)\)", calls)
            relationships.append(
                {
                    "dependent": entity_match.group("name"),
                    "principal": match.group("principal"),
                    "navigation": match.group("navigation"),
                    "columns": parse_literal_list(foreign_key.group("columns")),
                    "principal_columns": (
                        parse_literal_list(principal_key.group("columns"))
                        if principal_key
                        else None
                    ),
                    "on_delete": parse_enum(calls, "DeleteBehavior."),
                    "required": ".IsRequired()" in calls,
                }
            )

    for entity in entities.values():
        if entity["table"] is None:
            raise MigrationModelError(
                f"The table of the entity '{entity['clr_name']}' is unknown."
            )

    return {"entities": entities, "relationships": relationships}


def normalize_derived_model(model: dict[str, Any]) -> dict[str, Any]:
    """Brings the model of ``ef_model.derive_model`` into the shape of a generated file."""
    entities: dict[str, dict[str, Any]] = {}
    for entity in model["entities"]:
        columns = []
        for column in entity["columns"]:
            columns.append(
                {
                    "name": column["name"],
                    "clr_type": column["snapshot_clr_type"],
                    "nullable": bool(column["nullable"]),
                    "max_length": column["max_length"],
                    "unicode": column["unicode"],
                    "value_generated": value_generated_on_add(entity, column),
                    "column_type": column["snapshot_column_type"] or column["store_type"],
                }
            )
        indexes = [
            {
                "columns": list(index["columns"]),
                "unique": bool(index["unique"]),
                "filter": index["filter"],
                "clustered": index.get("clustered"),
            }
            for index in entity["indexes"]
        ]
        entities[entity["clr_name"]] = {
            "clr_name": entity["clr_name"],
            "table": entity["table"],
            "columns": columns,
            "primary_key": list(entity["primary_key"]),
            "alternate_keys": [list(key) for key in entity["alternate_keys"]],
            "indexes": indexes,
        }

    relationships = [
        {
            "dependent": foreign_key["dependent_entity"],
            "principal": foreign_key["principal_entity"],
            "navigation": foreign_key["dependent_navigation"],
            "columns": list(foreign_key["columns"]),
            "principal_columns": (
                list(foreign_key["principal_columns"])
                if foreign_key["principal_columns"]
                != _primary_key_of(model, foreign_key["principal_entity"])
                else None
            ),
            "on_delete": (
                foreign_key["on_delete"]
                if foreign_key["on_delete"] != "ClientSetNull"
                else None
            ),
            "required": bool(foreign_key["required"]),
        }
        for foreign_key in model["foreign_keys"]
    ]

    return {"entities": entities, "relationships": relationships}


def _primary_key_of(model: dict[str, Any], clr_name: str) -> list[str]:
    for entity in model["entities"]:
        if entity["clr_name"] == clr_name:
            return list(entity["primary_key"] or ["Id"])
    return ["Id"]


def value_generated_on_add(entity: dict[str, Any], column: dict[str, Any]) -> bool:
    """Reports whether the snapshot writes '.ValueGeneratedOnAdd()' for the column.

    EF Core writes the annotation only for a property whose 'ValueGenerated' is 'OnAdd'.
    '[DatabaseGenerated(DatabaseGeneratedOption.None)]' sets it to 'Never' with
    'ConfigurationSource.DataAnnotation', which the 'ValueGenerationConvention'
    ('ConfigurationSource.Convention') can not override. Without such an attribute the
    'ValueGenerationConvention' applies: a key of a single property that is a 'Guid' or an
    integer (but not a 'byte') and that is not a foreign key property gets
    'ValueGenerated.OnAdd'.
    """
    explicit = column.get("value_generated")
    if explicit is not None:
        return explicit == "OnAdd"

    primary_key = entity.get("primary_key") or []
    if len(primary_key) != 1 or primary_key[0] != column["name"]:
        return False

    for foreign_key in entity.get("foreign_keys", []):
        if column["name"] in foreign_key["columns"]:
            return False

    clr_type = (column.get("clr_type") or column.get("provider_clr_type") or "").split(".")[-1]
    return clr_type == "Guid" or clr_type in {"int", "Int32", "long", "Int64", "short", "Int16"}


# --------------------------------------------------------------------------------------
# The difference of two models
# --------------------------------------------------------------------------------------

# The facets of a column that 'MigrationsModelDiffer.ColumnStructureEquals' compares and
# that 'MigrationsModelDiffer.Diff(IColumn, IColumn, ...)' turns into an
# 'AlterColumnOperation'. 'ValueGenerated', 'IsUnicode' and 'MaxLength' are deliberately
# not part of the trigger: EF Core reads them from the store type, so a real change of
# them always changes the column type as well.
ALTER_COLUMN_FACETS = ("nullable", "column_type")

# The facets that 'MigrationsModelDiffer.IndexStructureEquals' compares. An index whose
# facets differ is not matched at all, so it is dropped and created again.
INDEX_FACETS = ("columns", "unique", "filter")


def _column_key(column: dict[str, Any]) -> str:
    return column["name"]


def _index_key(index: dict[str, Any]) -> tuple[str, ...]:
    return tuple(index["columns"])


def compute_operations(
    source: dict[str, Any], target: dict[str, Any]
) -> list[dict[str, Any]]:
    """Returns the operations that turn the ``source`` model into the ``target`` model."""
    source_entities = source["entities"]
    target_entities = target["entities"]

    added = sorted(set(target_entities) - set(source_entities))
    removed = sorted(set(source_entities) - set(target_entities))
    if added or removed:
        raise MigrationModelError(
            "The entity types of the two models differ "
            f"(added: {added or 'none'}, removed: {removed or 'none'}). A table is added "
            "or dropped; the migration has to be written with 'dotnet ef migrations add'."
        )

    source_relationships = Counter(_normalize_relationships(source["relationships"]))
    target_relationships = Counter(_normalize_relationships(target["relationships"]))
    if source_relationships - target_relationships:
        # A relationship that was removed or changed (principal, columns, delete behavior)
        # drops or rebuilds a constraint; such a migration is written and reviewed by hand.
        raise MigrationModelError(
            "A relationship of the source model was removed or changed; the migration has "
            "to be written with 'dotnet ef migrations add'."
        )
    added_relationships = target_relationships - source_relationships

    operations: list[dict[str, Any]] = []
    for clr_name in sorted(target_entities):
        source_entity = source_entities[clr_name]
        target_entity = target_entities[clr_name]
        if source_entity["table"] != target_entity["table"]:
            raise MigrationModelError(
                f"The table of '{clr_name}' was renamed; the migration has to be written "
                "with 'dotnet ef migrations add'."
            )
        if source_entity["primary_key"] != target_entity["primary_key"] or sorted(
            map(tuple, source_entity["alternate_keys"])
        ) != sorted(map(tuple, target_entity["alternate_keys"])):
            raise MigrationModelError(
                f"The keys of '{clr_name}' differ; the migration has to be written with "
                "'dotnet ef migrations add'."
            )
        operations.extend(_column_operations(source_entity, target_entity))
        operations.extend(_index_operations(source_entity, target_entity))

    for relationship in target["relationships"]:
        key = _relationship_key(relationship)
        if added_relationships[key] > 0:
            added_relationships[key] -= 1
            operations.append(_foreign_key_operation(relationship, target_entities))

    # A column that participates in an index can not be altered while the index exists
    # (SQL Server error 5074), therefore every 'DropIndex' of the migration comes first. A
    # foreign key is added last, after the columns and indexes it depends on.
    order = {
        "drop_index": 0,
        "add_column": 1,
        "alter_column": 1,
        "create_index": 2,
        "add_foreign_key": 3,
    }
    return sorted(operations, key=lambda operation: order[operation["kind"]])


# 'DeleteBehavior' -> 'ReferentialAction' as EF Core's migrations code generator writes it.
# Behaviors that this table does not list are refused (fail-closed), not guessed.
REFERENTIAL_ACTIONS = {
    None: "Restrict",
    "ClientSetNull": "Restrict",
    "Restrict": "Restrict",
    "Cascade": "Cascade",
    "SetNull": "SetNull",
    "NoAction": "NoAction",
}


def _foreign_key_operation(
    relationship: dict[str, Any], entities: dict[str, dict[str, Any]]
) -> dict[str, Any]:
    dependent = entities[relationship["dependent"]]
    principal = entities[relationship["principal"]]
    if relationship["on_delete"] not in REFERENTIAL_ACTIONS:
        raise MigrationModelError(
            f"The delete behavior '{relationship['on_delete']}' of a new foreign key is not "
            "supported; the migration has to be written with 'dotnet ef migrations add'."
        )
    columns = list(relationship["columns"])
    return {
        "kind": "add_foreign_key",
        "table": dependent["table"],
        "name": "FK_{}_{}_{}".format(dependent["table"], principal["table"], "_".join(columns)),
        "columns": columns,
        "principal_table": principal["table"],
        "principal_columns": list(relationship["principal_columns"] or principal["primary_key"]),
        "on_delete": REFERENTIAL_ACTIONS[relationship["on_delete"]],
    }


def compute_operations_with_added_tables(
    source: dict[str, Any], target: dict[str, Any]
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], int]:
    """The difference of a migration that adds tables (written with 'dotnet ef').

    'compute_operations' refuses added tables because this module can not render a
    'CreateTable'. A migration that adds tables is therefore written with 'dotnet ef
    migrations add' and only verified here. The method returns

    * the operations on the tables that already existed (column and index changes and the
      alternate keys that were added to them; dropped tables, dropped keys and changed or
      removed relationships are still refused),
    * the entity types of the added tables (verified against the designer file by the
      caller), and
    * the number of relationships that were added (they have to be the foreign keys of
      the added tables).
    """
    source_entities = source["entities"]
    target_entities = target["entities"]

    added = sorted(set(target_entities) - set(source_entities))
    removed = sorted(set(source_entities) - set(target_entities))
    if removed:
        raise MigrationModelError(
            f"The entity types {removed} were removed; dropping a table is not supported."
        )

    source_relationships = Counter(_normalize_relationships(source["relationships"]))
    target_relationships = Counter(_normalize_relationships(target["relationships"]))
    if source_relationships - target_relationships:
        raise MigrationModelError(
            "A relationship of an existing table was changed or removed; this is not "
            "supported by the verification of a migration that adds tables."
        )
    added_relationships = sum((target_relationships - source_relationships).values())

    operations: list[dict[str, Any]] = []
    for clr_name in sorted(source_entities):
        source_entity = source_entities[clr_name]
        target_entity = target_entities[clr_name]
        if source_entity["table"] != target_entity["table"]:
            raise MigrationModelError(f"The table of '{clr_name}' was renamed.")
        if source_entity["primary_key"] != target_entity["primary_key"]:
            raise MigrationModelError(f"The primary key of '{clr_name}' was changed.")
        source_keys = {tuple(key) for key in source_entity["alternate_keys"]}
        target_keys = {tuple(key) for key in target_entity["alternate_keys"]}
        if source_keys - target_keys:
            raise MigrationModelError(f"An alternate key of '{clr_name}' was removed.")
        for key in sorted(target_keys - source_keys):
            operations.append(
                {
                    "kind": "add_unique_constraint",
                    "table": target_entity["table"],
                    "columns": list(key),
                }
            )
        operations.extend(_column_operations(source_entity, target_entity))
        operations.extend(_index_operations(source_entity, target_entity))

    return operations, [target_entities[name] for name in added], added_relationships


def _relationship_key(relationship: dict[str, Any]) -> tuple[Any, ...]:
    return (
        relationship["dependent"],
        relationship["principal"],
        relationship["navigation"] or "",
        tuple(relationship["columns"]),
        tuple(relationship["principal_columns"] or ()),
        relationship["on_delete"] or "",
        relationship["required"],
    )


def _normalize_relationships(relationships: list[dict[str, Any]]) -> list[tuple[Any, ...]]:
    return sorted(_relationship_key(relationship) for relationship in relationships)


def _column_operations(
    source_entity: dict[str, Any], target_entity: dict[str, Any]
) -> list[dict[str, Any]]:
    source_columns = {c["name"]: c for c in source_entity["columns"]}
    target_columns = {c["name"]: c for c in target_entity["columns"]}

    if set(source_columns) - set(target_columns):
        # A dropped column can lose data; its migration is written and reviewed by hand.
        raise MigrationModelError(
            f"The columns of '{target_entity['table']}' differ; the migration has to be "
            "written with 'dotnet ef migrations add'."
        )

    operations: list[dict[str, Any]] = []
    # A column that only the target model has is added, in the order of the model.
    for column in target_entity["columns"]:
        if column["name"] not in source_columns:
            operations.append(
                {
                    "kind": "add_column",
                    "table": target_entity["table"],
                    "name": column["name"],
                    "column": column,
                }
            )
    for name in sorted(set(target_columns) & set(source_columns)):
        source_column = source_columns[name]
        target_column = target_columns[name]
        # The facets are compared by value, not by truthiness: a column type keeps its
        # truthiness when only the length changes ('varchar(44)' -> 'varchar(255)'), and
        # that is exactly the kind of change an 'AlterColumn' has to carry.
        changed = [
            facet
            for facet in ALTER_COLUMN_FACETS
            if source_column[facet] != target_column[facet]
        ]
        if changed:
            operations.append(
                {
                    "kind": "alter_column",
                    "table": target_entity["table"],
                    "name": name,
                    "column": target_column,
                    "old_column": source_column,
                    "changed": changed,
                }
            )
    return operations


def _index_operations(
    source_entity: dict[str, Any], target_entity: dict[str, Any]
) -> list[dict[str, Any]]:
    source_indexes = {_index_key(index): index for index in source_entity["indexes"]}
    target_indexes = {_index_key(index): index for index in target_entity["indexes"]}

    operations: list[dict[str, Any]] = []
    for key in sorted(set(source_indexes) - set(target_indexes)):
        operations.append(
            {
                "kind": "drop_index",
                "table": target_entity["table"],
                "index": source_indexes[key],
            }
        )
    for key in sorted(set(target_indexes) - set(source_indexes)):
        operations.append(
            {
                "kind": "create_index",
                "table": target_entity["table"],
                "index": target_indexes[key],
            }
        )
    for key in sorted(set(source_indexes) & set(target_indexes)):
        source_index = source_indexes[key]
        target_index = target_indexes[key]
        differs = any(
            (source_index[facet] or None) != (target_index[facet] or None)
            for facet in ("unique", "filter", "clustered")
        )
        if differs:
            # 'IndexStructureEquals' compares the facets of an index before it matches it
            # by name, so an index whose facets changed is dropped and created again.
            operations.append(
                {
                    "kind": "drop_index",
                    "table": target_entity["table"],
                    "index": source_index,
                }
            )
            operations.append(
                {
                    "kind": "create_index",
                    "table": target_entity["table"],
                    "index": target_index,
                }
            )
    return operations


# --------------------------------------------------------------------------------------
# Rendering
# --------------------------------------------------------------------------------------


def literal(value: str) -> str:
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


def index_name(table: str, index: dict[str, Any]) -> str:
    return "IX_{}_{}".format(table, "_".join(index["columns"]))


def array_literal(values: list[str]) -> str:
    return "new[] { " + ", ".join(literal(value) for value in values) + " }"


def clr_type_name(clr_type: str) -> str:
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


def _render_alter_column(operation: dict[str, Any], indent: str) -> list[str]:
    """Renders an 'AlterColumn' operation in the shape of the EF Core code generator."""
    column = operation["column"]
    old_column = operation["old_column"]
    nullable = operation["kind"] == "alter_column" and not column["nullable"]

    lines = [
        f"{indent}migrationBuilder.AlterColumn<{clr_type_name(column['clr_type'])}>(",
        f"{indent}    name: {literal(operation['name'])},",
        f"{indent}    table: {literal(operation['table'])},",
        f"{indent}    type: {literal(column['column_type'])},",
    ]
    if column["unicode"] is False:
        lines.append(f"{indent}    unicode: false,")
    if column["max_length"] is not None:
        lines.append(f"{indent}    maxLength: {column['max_length']},")
    lines.append(f"{indent}    nullable: {'true' if not nullable else 'false'},")
    lines.append(f"{indent}    oldClrType: typeof({clr_type_name(old_column['clr_type'])}),")
    lines.append(f"{indent}    oldType: {literal(old_column['column_type'])},")
    if old_column["unicode"] is False:
        lines.append(f"{indent}    oldUnicode: false,")
    if old_column["max_length"] is not None:
        lines.append(f"{indent}    oldMaxLength: {old_column['max_length']},")
    if old_column["nullable"]:
        lines.append(f"{indent}    oldNullable: true,")
    lines[-1] = lines[-1][: -len(",")] + ");"
    return lines


def _render_drop_index(operation: dict[str, Any], indent: str) -> list[str]:
    index = operation["index"]
    return [
        f"{indent}migrationBuilder.DropIndex(",
        f"{indent}    name: {literal(index_name(operation['table'], index))},",
        f"{indent}    table: {literal(operation['table'])});",
    ]


def _render_create_index(operation: dict[str, Any], indent: str) -> list[str]:
    index = operation["index"]
    lines = [
        f"{indent}migrationBuilder.CreateIndex(",
        f"{indent}    name: {literal(index_name(operation['table'], index))},",
        f"{indent}    table: {literal(operation['table'])},",
    ]
    if len(index["columns"]) == 1:
        lines.append(f"{indent}    column: {literal(index['columns'][0])},")
    else:
        lines.append(f"{indent}    columns: {array_literal(index['columns'])},")
    if index["unique"]:
        lines.append(f"{indent}    unique: true,")
    if index["filter"]:
        lines.append(f"{indent}    filter: {literal(index['filter'])},")
    if index.get("clustered") is False:
        lines.append(f"{indent}    clustered: false,")
    lines[-1] = lines[-1][: -len(",")] + ");"
    return lines


def render_up(operations: list[dict[str, Any]], indent: str) -> list[str]:
    lines: list[str] = []
    for position, operation in enumerate(operations):
        if position:
            lines.append("")
        if operation["kind"] == "alter_column":
            lines.extend(_render_alter_column(operation, indent))
        elif operation["kind"] == "drop_index":
            lines.extend(_render_drop_index(operation, indent))
        elif operation["kind"] == "create_index":
            lines.extend(_render_create_index(operation, indent))
        else:  # pragma: no cover - compute_operations does not produce anything else
            raise MigrationModelError(
                f"The operation '{operation['kind']}' can not be rendered."
            )
    return lines


def reverse_operations(operations: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Returns the operations of the 'Down' method."""
    reversed_operations: list[dict[str, Any]] = []
    for operation in reversed(operations):
        if operation["kind"] == "drop_index":
            reversed_operations.append(
                {"kind": "create_index", "table": operation["table"], "index": operation["index"]}
            )
        elif operation["kind"] == "create_index":
            reversed_operations.append(
                {"kind": "drop_index", "table": operation["table"], "index": operation["index"]}
            )
        elif operation["kind"] == "add_column":
            reversed_operations.append(
                {"kind": "drop_column", "table": operation["table"], "name": operation["name"]}
            )
        elif operation["kind"] == "add_foreign_key":
            reversed_operations.append(
                {
                    "kind": "drop_foreign_key",
                    "table": operation["table"],
                    "name": operation["name"],
                }
            )
        else:
            reversed_operations.append(
                {
                    "kind": "alter_column",
                    "table": operation["table"],
                    "name": operation["name"],
                    "column": operation["old_column"],
                    "old_column": operation["column"],
                    "changed": operation["changed"],
                }
            )
    # The same rule as in 'compute_operations': an index is dropped before a column that
    # it uses is altered, and created again afterwards.
    order = {
        "drop_foreign_key": 0,
        "drop_index": 1,
        "drop_column": 2,
        "alter_column": 2,
        "create_index": 3,
    }
    return sorted(reversed_operations, key=lambda operation: order[operation["kind"]])
