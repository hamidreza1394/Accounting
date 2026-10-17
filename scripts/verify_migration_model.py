#!/usr/bin/env python3
"""Verifies the generated migration and model snapshot against the EF Core model.

The script replaces the checks that normally need the .NET SDK:

* ``dotnet ef migrations has-pending-model-changes`` is approximated by comparing
  the model that the snapshot describes with the model of the code base.
* The C# syntax of the generated files is validated with tree-sitter.

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

from ef_model import ModelExtractionError, derive_model  # noqa: E402

MIGRATIONS_DIRECTORY = os.path.join("Infrastructure", "Persistence", "Migrations")
MIGRATION_FILE = "20251017120000_InitialCreate.cs"
SNAPSHOT_FILE = "DatabaseContextModelSnapshot.cs"
DESIGNER_FILE = "20251017120000_InitialCreate.Designer.cs"

# The CLR types of the snapshot and the store types of the migration.
CLARE_TYPES = {
    "Guid": "System.Guid",
    "DateTime": "System.DateTime",
    "string": "System.String",
    "bool": "System.Boolean",
    "int": "System.Int32",
    "long": "System.Int64",
    "short": "System.Int16",
    "byte": "System.Byte",
    "float": "System.Single",
    "double": "System.Double",
    "decimal": "System.Decimal",
}


class VerificationError(Exception):
    """Raised when the generated files do not match the model."""


def read_text(path: str) -> str:
    with open(path, encoding="utf-8-sig") as file:
        return file.read()


# --------------------------------------------------------------------------------------
# Parsing of the generated files
# --------------------------------------------------------------------------------------


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
    raise VerificationError("Unbalanced parenthesis in the generated code.")


def parse_columns(body: str) -> list[dict[str, Any]]:
    """Reads the columns of a 'CreateTable' operation."""
    columns: list[dict[str, Any]] = []
    for match in re.finditer(r"(?P<property>\w+) = table\.Column<(?P<type>[\w\?]+)>\(", body):
        arguments, _ = balanced_call(body, match.end() - 1)
        columns.append(
            {
                "name": match.group("property"),
                "clr_type": CLARE_TYPES[match.group("type").rstrip("?")],
                "nullable": "nullable: true" in arguments,
                "type": re.search(r'type: "([^"]+)"', arguments).group(1),
                "max_length": _optional_int(re.search(r"maxLength: (\d+)", arguments)),
                "unicode": "unicode: false" not in arguments,
            }
        )
    return columns


def split_arguments(text: str) -> list[str]:
    """Splits the arguments of a call at the commas that are not nested."""
    arguments: list[str] = []
    depth = 0
    current = []
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


def parse_migration(source: str) -> dict[str, Any]:
    """Reads the tables, the indexes and the foreign keys of the migration."""
    tables: dict[str, dict[str, Any]] = {}
    indexes: list[dict[str, Any]] = []
    dropped_tables: list[str] = []

    for match in re.finditer(
        r"migrationBuilder\.CreateTable\(\s*name: \"(?P<name>[^\"]+)\",(?P<body>.*?)\n\s*\}\);",
        source,
        re.DOTALL,
    ):
        name = match.group("name")
        body = match.group("body")
        columns = parse_columns(body)
        primary_key = _parse_lambda_call(body, "table.PrimaryKey")
        unique_constraints = [
            _parse_lambda_call(body, "table.UniqueConstraint")
            for _ in re.finditer(r"table\.UniqueConstraint\(", body)
        ]
        foreign_keys: list[dict[str, Any]] = []
        for foreign_key in re.finditer(r"table\.ForeignKey\(", body):
            arguments, _ = balanced_call(body, foreign_key.end() - 1)
            values = {}
            for argument in split_arguments(arguments):
                key, _, value = argument.partition(":")
                values[key.strip()] = value.strip()
            foreign_keys.append(
                {
                    "name": re.search(r'"([^"]*)"', values.get("name", "")).group(1),
                    "columns": _parse_lambda_columns(values.get("column", values.get("columns", ""))),
                    "principal_table": re.search(
                        r'"([^"]*)"', values.get("principalTable", "")
                    ).group(1),
                    "principal_columns": (
                        _parse_literal_columns(values.get("principalColumn", ""))
                        if "principalColumn" in values
                        else _parse_literal_columns(values.get("principalColumns", ""))
                    ),
                    "on_delete": _parse_enum(arguments, "ReferentialAction."),
                }
            )
        tables[name] = {
            "columns": columns,
            "primary_key": primary_key,
            "unique_constraints": unique_constraints,
            "foreign_keys": foreign_keys,
        }

    for match in re.finditer(r"migrationBuilder\.CreateIndex\(", source):
        arguments, _ = balanced_call(source, match.end() - 1)
        name = re.search(r'name: "([^"]*)"', arguments).group(1)
        table = re.search(r'table: "([^"]*)"', arguments).group(1)
        columns = re.search(r"column(?:s)?: ([^\n]+)", arguments).group(1)
        indexes.append(
            {
                "name": name,
                "table": table,
                "columns": _parse_literal_columns(columns),
                "unique": "unique: true" in arguments,
                "filter": _optional_string(
                    re.search(r'filter: "([^"]*)"', arguments)
                ),
            }
        )

    for match in re.finditer(r"migrationBuilder\.DropTable\(\s*name: \"([^\"]+)\"", source):
        dropped_tables.append(match.group(1))

    return {"tables": tables, "indexes": indexes, "dropped_tables": dropped_tables}


def parse_snapshot(source: str) -> dict[str, Any]:
    """Reads the entity types, the keys, the indexes and the relationships."""
    entities: dict[str, dict[str, Any]] = {}
    relationships: list[dict[str, Any]] = []

    for match in re.finditer(
        r'modelBuilder\.Entity\("(?P<name>[^"]+)", b =>\s*\{(?P<body>.*?)\n\s*\}\);',
        source,
        re.DOTALL,
    ):
        name = match.group("name")
        body = match.group("body")
        properties: list[dict[str, Any]] = []
        for prop in re.finditer(
            r'b\.Property<(?P<type>[\w\?]+)>\("(?P<name>[^"]+)"\)(?P<calls>[^;]*);',
            body,
        ):
            calls = prop.group("calls")
            is_nullable_type = prop.group("type").endswith("?")
            is_reference_type = prop.group("type").rstrip("?") == "string"
            properties.append(
                {
                    "name": prop.group("name"),
                    "clr_type": CLARE_TYPES[prop.group("type").rstrip("?")],
                    "nullable": (
                        is_nullable_type
                        or (is_reference_type and ".IsRequired()" not in calls)
                    ),
                    "max_length": _optional_int(re.search(r"HasMaxLength\((\d+)\)", calls)),
                    "unicode": ".IsUnicode(false)" not in calls,
                    "value_generated": ".ValueGeneratedOnAdd()" in calls,
                    "column_type": _optional_string(
                        re.search(r'HasColumnType\("([^"]*)"\)', calls)
                    ),
                }
            )
        keys = [
            _parse_literal_list(match.group(1))
            for match in re.finditer(r"b\.HasKey\(([^)]*)\);", body)
        ]
        alternate_keys = [
            _parse_literal_list(match.group(1))
            for match in re.finditer(r"b\.HasAlternateKey\(([^)]*)\);", body)
        ]
        indexes = []
        for index in re.finditer(
            r"b\.HasIndex\((?P<columns>[^)]*)\)(?P<calls>[^;]*);", body
        ):
            calls = index.group("calls")
            indexes.append(
                {
                    "columns": _parse_literal_list(index.group("columns")),
                    "unique": ".IsUnique()" in calls,
                    "filter": _optional_string(
                        re.search(r'HasFilter\("([^"]*)"\)', calls)
                    ),
                }
            )
        table = _optional_string(re.search(r'b\.ToTable\("([^"]*)"\)', body))
        described = entities.setdefault(
            name,
            {
                "properties": [],
                "keys": [],
                "alternate_keys": [],
                "indexes": [],
                "table": None,
            },
        )
        described["properties"].extend(properties)
        described["keys"].extend(keys)
        described["alternate_keys"].extend(alternate_keys)
        described["indexes"].extend(indexes)
        if table is not None:
            described["table"] = table

    for match in re.finditer(
        r'b\.HasOne\("(?P<principal>[^"]+)"(?:, "(?P<navigation>[^"]+)")?\)\s*'
        r"(?P<calls>.*?);",
        source,
        re.DOTALL,
    ):
        calls = match.group("calls")
        foreign_key = re.search(r"\.HasForeignKey\((?P<columns>[^)]*)\)", calls)
        if foreign_key is None:
            continue
        principal_key = re.search(r"\.HasPrincipalKey\((?P<columns>[^)]*)\)", calls)
        relationships.append(
            {
                "principal": match.group("principal"),
                "navigation": match.group("navigation"),
                "columns": _parse_literal_list(foreign_key.group("columns")),
                "principal_columns": (
                    _parse_literal_list(principal_key.group("columns"))
                    if principal_key
                    else None
                ),
                "on_delete": _parse_enum(calls, "DeleteBehavior."),
                "required": ".IsRequired()" in calls,
            }
        )

    return {"entities": entities, "relationships": relationships}


def _optional_int(match: re.Match[str] | None) -> int | None:
    return int(match.group(1)) if match else None


def _optional_string(match: re.Match[str] | None) -> str | None:
    return match.group(1) if match else None


def _parse_enum(text: str, prefix: str) -> str | None:
    match = re.search(re.escape(prefix) + r"(\w+)", text)
    return match.group(1) if match else None


def _parse_literal_list(text: str) -> list[str]:
    return re.findall(r'"([^"]*)"', text)


def _expects_value_generation(entity: dict[str, Any], column: dict[str, Any]) -> bool:
    """Reports whether EF Core generates the value of the column when the row is added.

    EF Core applies the 'ValueGenerationConvention': a key of a single property that is a
    'Guid' or an integer (but not a 'byte') and that is not a foreign key property gets
    'ValueGenerated.OnAdd'.
    """
    primary_key = entity.get("primary_key") or []
    if len(primary_key) != 1 or primary_key[0] != column["name"]:
        return False

    for foreign_key in entity.get("foreign_keys", []):
        if column["name"] in foreign_key["columns"]:
            return False

    clr_type = (column.get("clr_type") or column.get("provider_clr_type") or "").split(".")[-1]
    return clr_type == "Guid" or clr_type in {"int", "Int32", "long", "Int64", "short", "Int16"}


def _parse_literal_columns(text: str) -> list[str]:
    text = text.strip().rstrip(",")
    if "new[]" in text:
        return re.findall(r'"([^"]*)"', text)
    return [
        value.strip().strip('"') for value in text.split(",") if value.strip()
    ]


def _parse_lambda_columns(text: str) -> list[str]:
    text = text.strip()
    if "new {" in text:
        return re.findall(r"x\.(\w+)", text)
    match = re.search(r"x\.(\w+)", text)
    return [match.group(1)] if match else []


def _parse_lambda_call(body: str, call: str) -> list[str]:
    match = re.search(re.escape(call) + r"\(\"([^\"]*)\",\s*([^)]*)\)", body)
    if match is None:
        return []
    return _parse_lambda_columns(match.group(2))


# --------------------------------------------------------------------------------------
# Comparison with the model
# --------------------------------------------------------------------------------------


def verify(repository_root: str) -> list[str]:
    """Compares the generated files with the model, returns the list of problems."""
    model = derive_model(repository_root)
    problems: list[str] = []

    directory = os.path.join(repository_root, MIGRATIONS_DIRECTORY)
    migration_source = read_text(os.path.join(directory, MIGRATION_FILE))
    snapshot_source = read_text(os.path.join(directory, SNAPSHOT_FILE))
    designer_source = read_text(os.path.join(directory, DESIGNER_FILE))

    migration = parse_migration(migration_source)
    snapshot = parse_snapshot(snapshot_source)
    designer = parse_snapshot(designer_source)

    entities = model["entities"]
    by_clr = {entity["clr_name"]: entity for entity in entities}

    # ---- the tables of the migration ------------------------------------------------
    migration_tables = migration["tables"]
    model_tables = {entity["table"] for entity in entities}
    for table in sorted(model_tables - set(migration_tables)):
        problems.append(f"The table '{table}' is missing in the migration.")
    for table in sorted(set(migration_tables) - model_tables):
        problems.append(f"The table '{table}' of the migration is not part of the model.")

    for entity in entities:
        table = entity["table"]
        if table not in migration_tables:
            continue
        migrated = migration_tables[table]

        migrated_columns = {column["name"]: column for column in migrated["columns"]}
        model_columns = {column["name"]: column for column in entity["columns"]}
        for name in sorted(set(model_columns) - set(migrated_columns)):
            problems.append(f"The column '{table}.{name}' is missing in the migration.")
        for name in sorted(set(migrated_columns) - set(model_columns)):
            problems.append(f"The column '{table}.{name}' of the migration is unknown.")

        for name in sorted(set(model_columns) & set(migrated_columns)):
            expected = model_columns[name]
            actual = migrated_columns[name]
            if expected["store_type"] != actual["type"]:
                problems.append(
                    f"The type of '{table}.{name}' is '{actual['type']}' in the migration "
                    f"but '{expected['store_type']}' in the model."
                )
            if bool(expected["nullable"]) != bool(actual["nullable"]):
                problems.append(
                    f"The nullability of '{table}.{name}' is 'nullable: {actual['nullable']}' "
                    f"in the migration but '{expected['nullable']}' in the model."
                )
            if expected["max_length"] != actual["max_length"]:
                problems.append(
                    f"The maximum length of '{table}.{name}' is '{actual['max_length']}' in the "
                    f"migration but '{expected['max_length']}' in the model."
                )
            if (expected["unicode"] is False) != (actual["unicode"] is False):
                problems.append(
                    f"The unicode flag of '{table}.{name}' differs between the migration "
                    "and the model."
                )
            if expected["provider_clr_type"] != actual["clr_type"]:
                problems.append(
                    f"The provider type of '{table}.{name}' is '{actual['clr_type']}' in the "
                    f"migration but '{expected['provider_clr_type']}' in the model."
                )

        if migrated["primary_key"] != entity["primary_key"]:
            problems.append(
                f"The primary key of '{table}' is {migrated['primary_key']} in the migration "
                f"but {entity['primary_key']} in the model."
            )

        for alternate_key in entity["alternate_keys"]:
            if alternate_key not in migrated["unique_constraints"]:
                problems.append(
                    f"The alternate key {alternate_key} of '{table}' is missing in the migration."
                )

        migrated_foreign_keys = {
            (foreign_key["name"], tuple(foreign_key["columns"]))
            for foreign_key in migrated["foreign_keys"]
        }
        for foreign_key in entity["foreign_keys"]:
            name = "FK_{}_{}_{}".format(
                foreign_key["dependent"],
                foreign_key["principal_table"],
                "_".join(foreign_key["columns"]),
            )
            if (name, tuple(foreign_key["columns"])) not in migrated_foreign_keys:
                problems.append(
                    f"The foreign key '{name}' of '{table}' is missing in the migration."
                )

    # ---- the indexes of the migration ------------------------------------------------
    migrated_indexes = {
        (index["table"], tuple(index["columns"])): index for index in migration["indexes"]
    }
    model_indexes = {
        (entity["table"], tuple(index["columns"])): (entity, index)
        for entity in entities
        for index in entity["indexes"]
    }
    for key in sorted(set(model_indexes) - set(migrated_indexes)):
        problems.append(f"The index '{key[0]}.{key[1]}' is missing in the migration.")
    for key in sorted(set(migrated_indexes) - set(model_indexes)):
        problems.append(f"The index '{key[0]}.{key[1]}' of the migration is unknown.")
    for key in sorted(set(model_indexes) & set(migrated_indexes)):
        entity, index = model_indexes[key]
        actual = migrated_indexes[key]
        if bool(index["unique"]) != bool(actual["unique"]):
            problems.append(f"The uniqueness of the index '{key[0]}.{key[1]}' differs.")
        if (index["filter"] or None) != (actual["filter"] or None):
            problems.append(
                f"The filter of the index '{key[0]}.{key[1]}' is '{actual['filter']}' in the "
                f"migration but '{index['filter']}' in the model."
            )

    # ---- the 'Down' operations -------------------------------------------------------
    dropped = set(migration["dropped_tables"])
    if dropped != model_tables:
        problems.append("The 'Down' operations do not drop every table of the model.")

    # ---- the model snapshot ----------------------------------------------------------
    snapshot_entities = snapshot["entities"]
    for clr_name, entity in by_clr.items():
        if clr_name not in snapshot_entities:
            problems.append(f"The entity '{clr_name}' is missing in the snapshot.")
            continue
        described = snapshot_entities[clr_name]
        if described["table"] != entity["table"]:
            problems.append(
                f"The table of '{clr_name}' is '{described['table']}' in the snapshot but "
                f"'{entity['table']}' in the model."
            )
        described_columns = {column["name"]: column for column in described["properties"]}
        for column in entity["columns"]:
            if column["name"] not in described_columns:
                problems.append(
                    f"The property '{clr_name}.{column['name']}' is missing in the snapshot."
                )
                continue
            actual = described_columns[column["name"]]
            if actual["clr_type"] != column["snapshot_clr_type"]:
                problems.append(
                    f"The type of '{clr_name}.{column['name']}' is '{actual['clr_type']}' in "
                    f"the snapshot but '{column['snapshot_clr_type']}' in the model."
                )
            if bool(actual["nullable"]) != bool(column["nullable"]):
                problems.append(
                    f"The nullability of '{clr_name}.{column['name']}' differs between the "
                    "snapshot and the model."
                )
            if actual["max_length"] != column["max_length"]:
                problems.append(
                    f"The maximum length of '{clr_name}.{column['name']}' differs between the "
                    "snapshot and the model."
                )
            if (actual["unicode"] is False) != (column["unicode"] is False):
                problems.append(
                    f"The unicode flag of '{clr_name}.{column['name']}' differs between the "
                    "snapshot and the model."
                )
            expected_column_type = column["snapshot_column_type"] or column["store_type"]
            if actual["column_type"] != expected_column_type:
                problems.append(
                    f"The column type of '{clr_name}.{column['name']}' is "
                    f"'{actual['column_type']}' in the snapshot but '{expected_column_type}' "
                    "in the model."
                )
            expected_value_generated = _expects_value_generation(entity, column)
            if bool(actual["value_generated"]) != expected_value_generated:
                problems.append(
                    f"The value generation of '{clr_name}.{column['name']}' differs between "
                    "the snapshot and the model."
                )
        if entity["primary_key"] not in described["keys"]:
            problems.append(f"The primary key of '{clr_name}' is missing in the snapshot.")
        for alternate_key in entity["alternate_keys"]:
            if alternate_key not in described["alternate_keys"]:
                problems.append(
                    f"The alternate key {alternate_key} of '{clr_name}' is missing in the "
                    "snapshot."
                )
        snapshot_index_columns = {
            tuple(index["columns"]): index for index in described["indexes"]
        }
        for index in entity["indexes"]:
            key = tuple(index["columns"])
            if key not in snapshot_index_columns:
                problems.append(
                    f"The index {key} of '{clr_name}' is missing in the snapshot."
                )
                continue
            actual = snapshot_index_columns[key]
            if bool(actual["unique"]) != bool(index["unique"]):
                problems.append(f"The uniqueness of the index {key} of '{clr_name}' differs.")
            if (actual["filter"] or None) != (index["filter"] or None):
                problems.append(f"The filter of the index {key} of '{clr_name}' differs.")

    # ---- the relationships of the snapshot ---------------------------------------------
    snapshot_relationships = {
        (relationship["principal"], tuple(relationship["columns"])): relationship
        for relationship in snapshot["relationships"]
    }
    for entity in entities:
        for foreign_key in entity["foreign_keys"]:
            key = (foreign_key["principal_entity"], tuple(foreign_key["columns"]))
            if key not in snapshot_relationships:
                problems.append(
                    f"The relationship '{foreign_key['principal_entity']}' -> "
                    f"'{foreign_key['dependent']}' ({foreign_key['columns']}) is missing in the "
                    "snapshot."
                )
                continue
            relationship = snapshot_relationships[key]
            expected_on_delete = foreign_key["on_delete"]
            actual_on_delete = relationship["on_delete"] or "ClientSetNull"
            if expected_on_delete != actual_on_delete:
                problems.append(
                    f"The delete behavior of the relationship '{key[0]}' ({key[1]}) is "
                    f"'{actual_on_delete}' in the snapshot but '{expected_on_delete}' in the "
                    "model."
                )
            if bool(foreign_key["required"]) != bool(relationship["required"]):
                problems.append(
                    f"The required flag of the relationship '{key[0]}' ({key[1]}) differs "
                    "between the snapshot and the model."
                )
            if relationship["principal_columns"] is not None:
                if list(relationship["principal_columns"]) != list(
                    foreign_key["principal_columns"]
                ):
                    problems.append(
                        f"The principal key of the relationship '{key[0]}' ({key[1]}) differs "
                        "between the snapshot and the model."
                    )

    # ---- the designer file must describe the same model as the snapshot ---------------
    if snapshot["entities"] != designer["entities"]:
        problems.append("The designer file and the snapshot describe a different model.")

    return problems


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


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    arguments = parser.parse_args(argv)

    directory = os.path.join(arguments.repo_root, MIGRATIONS_DIRECTORY)
    syntax_problems = verify_syntax(
        [
            os.path.join(directory, MIGRATION_FILE),
            os.path.join(directory, DESIGNER_FILE),
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
    print("The migration and the model snapshot match the EF Core model of the code base.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except (ModelExtractionError, VerificationError) as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(2)
