#!/usr/bin/env python3
"""Extracts the EF Core model of the Taraz Ava project from the C# sources.

The extractor is intentionally strict: every construct that it does not understand
makes it fail instead of silently producing a wrong model. The extracted model is
used to

  * generate the initial EF Core migration and the model snapshot, and
  * verify that the committed migration still matches the entity model.

Usage:
    python3 scripts/ef_model.py [--repo-root .] [--json]
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from typing import Any

# --------------------------------------------------------------------------------------
# Repository layout
# --------------------------------------------------------------------------------------

DOMAIN_DIR = "Domain"
CONFIGURATIONS_DIR = os.path.join("Infrastructure", "Persistence", "Configurations")
DATABASE_CONTEXT = os.path.join("Infrastructure", "Persistence", "DatabaseContext.cs")

# --------------------------------------------------------------------------------------
# SQL Server store types (Microsoft.EntityFrameworkCore.SqlServer 10.0.13)
# --------------------------------------------------------------------------------------

# The CLR type names of the model, normalized to the full name of the type.
CLR_TYPE_NAMES = {
    "System.Guid": "System.Guid",
    "Guid": "System.Guid",
    "System.DateTime": "System.DateTime",
    "DateTime": "System.DateTime",
    "System.String": "System.String",
    "string": "System.String",
    "String": "System.String",
    "System.Boolean": "System.Boolean",
    "bool": "System.Boolean",
    "Boolean": "System.Boolean",
    "System.Int32": "System.Int32",
    "int": "System.Int32",
    "Int32": "System.Int32",
    "System.Int64": "System.Int64",
    "long": "System.Int64",
    "System.Int16": "System.Int16",
    "short": "System.Int16",
    "System.Byte": "System.Byte",
    "byte": "System.Byte",
    "System.Single": "System.Single",
    "float": "System.Single",
    "System.Double": "System.Double",
    "double": "System.Double",
    "System.Decimal": "System.Decimal",
    "decimal": "System.Decimal",
    "System.DateOnly": "System.DateOnly",
    "DateOnly": "System.DateOnly",
}

STORE_TYPE_GUID = "uniqueidentifier"
STORE_TYPE_BOOL = "bit"
STORE_TYPE_INT = "int"
STORE_TYPE_BIGINT = "bigint"
STORE_TYPE_SMALLINT = "smallint"
STORE_TYPE_TINYINT = "tinyint"
STORE_TYPE_REAL = "real"
STORE_TYPE_FLOAT = "float"
STORE_TYPE_DECIMAL = "decimal(18, 2)"
STORE_TYPE_DATETIME2 = "datetime2"
STORE_TYPE_DATETIME = "datetime"
STORE_TYPE_DATE = "date"

# EF Core limits the size of an indexed/key string column when no explicit maximum
# length is configured (450 characters for Unicode strings, 900 for ANSI strings).
MAX_INDEXED_UNICODE_STRING_LENGTH = 450
MAX_INDEXED_ANSI_STRING_LENGTH = 900

# --------------------------------------------------------------------------------------
# Small helpers
# --------------------------------------------------------------------------------------


class ModelExtractionError(Exception):
    """Raised when the C# sources contain something the extractor does not understand."""


def read_text(path: str) -> str:
    with open(path, "r", encoding="utf-8-sig") as file:
        return file.read()


def walk_files(directory: str) -> list[str]:
    """Returns every C# file of a directory tree, sorted by path."""
    result: list[str] = []
    for root, _, files in os.walk(directory):
        for file_name in files:
            if file_name.endswith(".cs"):
                result.append(os.path.join(root, file_name))
    return sorted(result)


def strip_comments(source: str) -> str:
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.DOTALL)
    source = re.sub(r"//[^\n]*", "", source)
    return source


def match_brace(source: str, open_index: int) -> int:
    """Returns the index of the '}' that closes the '{' at ``open_index``."""
    depth = 0
    for index in range(open_index, len(source)):
        character = source[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return index
    raise ModelExtractionError("Unbalanced braces in the C# source.")


def normalize_type(type_text: str) -> str:
    """Removes the noise (whitespace, global::) from a CLR type name."""
    type_text = type_text.replace("global::", "")
    type_text = re.sub(r"\s+", " ", type_text).strip()
    return type_text


def short_type_name(type_text: str) -> str:
    """'System.Collections.Generic.IList<Domain.Permission>' -> 'IList<Domain.Permission>'."""
    type_text = normalize_type(type_text)
    if "<" in type_text:
        base, rest = type_text.split("<", 1)
        return f"{base.split('.')[-1]}<{rest}"
    return type_text.split(".")[-1] if "." in type_text else type_text


def is_nullable_reference(type_text: str) -> bool:
    type_text = normalize_type(type_text)
    return type_text.endswith("?")


def unwrap_nullable(type_text: str) -> str:
    type_text = normalize_type(type_text)
    return type_text[:-1] if type_text.endswith("?") else type_text


def is_collection_type(type_text: str) -> bool:
    type_text = short_type_name(normalize_type(type_text))
    return bool(
        re.match(r"^(IList|ICollection|IEnumerable|List|Collection|HashSet)<", type_text)
    )


def collection_element_type(type_text: str) -> str | None:
    match = re.search(r"<(.+)>$", normalize_type(type_text))
    return normalize_type(match.group(1)) if match else None


# --------------------------------------------------------------------------------------
# Entity extraction
# --------------------------------------------------------------------------------------

PROPERTY_PATTERN = re.compile(
    r"public\s+(?:virtual\s+|override\s+|new\s+)?"
    r"(?P<type>[A-Za-z_][\w\.\?]*\s*(?:<[^>{}]*>)?)\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*\{\s*get;\s*(?:private\s+|public\s+|protected\s+)?set;\s*\}",
    re.DOTALL,
)

ATTRIBUTE_PATTERN = re.compile(r"\[([^\[\]]+)\]")


def parse_attributes(block: str) -> list[str]:
    return [match.group(1).strip() for match in ATTRIBUTE_PATTERN.finditer(block)]


def attribute_argument(attribute: str, name: str) -> str | None:
    match = re.search(rf"{name}\s*:\s*([^,)]+)", attribute)
    if match:
        return match.group(1).strip()
    return None


def attribute_name(attribute: str) -> str:
    return attribute.split("(")[0].strip().split(".")[-1]


def extract_attribute_block(source: str, start: int) -> str:
    """Returns the attribute block that directly precedes ``start``."""
    index = start
    while True:
        # Skip the whitespace in front of the member.
        while index > 0 and source[index - 1] in " \t\r\n":
            index -= 1
        if index == 0 or source[index - 1] != "]":
            return ""
        # Find the matching '['.
        depth = 0
        cursor = index - 1
        while cursor >= 0:
            if source[cursor] == "]":
                depth += 1
            elif source[cursor] == "[":
                depth -= 1
                if depth == 0:
                    break
            cursor -= 1
        if cursor < 0:
            return ""
        block_start = cursor
        # Continue with the previous attribute, if any.
        index = block_start
        while True:
            probe = index
            while probe > 0 and source[probe - 1] in " \t\r\n":
                probe -= 1
            if probe == 0 or source[probe - 1] != "]":
                break
            depth = 0
            cursor = probe - 1
            while cursor >= 0:
                if source[cursor] == "]":
                    depth += 1
                elif source[cursor] == "[":
                    depth -= 1
                    if depth == 0:
                        break
                cursor -= 1
            if cursor < 0:
                break
            index = cursor
        return source[index:start]


class Entity:
    def __init__(self, clr_name: str, base_type: str | None) -> None:
        self.clr_name = clr_name
        self.base_type = base_type
        self.properties: list[dict[str, Any]] = []
        self.navigations: list[dict[str, Any]] = []
        self.constants: dict[str, int] = {}


def extract_constants(repository_root: str) -> dict[str, dict[str, int]]:
    """Reads the public int constants of the Constants project.

    The domain model uses them in its [MaxLength] data annotations, for example
    "Constants.MaxLength.Name", and EF Core turns them into the maximum length
    of the column.
    """
    constants: dict[str, dict[str, int]] = {}
    constants_path = os.path.join(repository_root, "Constants")
    for file_path in sorted(walk_files(constants_path)):
        source = strip_comments(read_text(file_path))
        class_match = re.search(r"class\s+(?P<name>\w+)", source)
        if class_match is None:
            continue
        class_name = class_match.group("name")
        values: dict[str, int] = {}
        for match in re.finditer(
            r"public\s+const\s+int\s+(?P<name>\w+)\s*=\s*(?P<value>\d[\d_]*)", source
        ):
            values[match.group("name")] = int(match.group("value").replace("_", ""))
        if values:
            constants[class_name] = values
    return constants


def resolve_constant(
    expression: str,
    constants: dict[str, dict[str, int]],
    type_constants: dict[str, dict[str, int]] | None = None,
    own_constants: dict[str, int] | None = None,
) -> int | None:
    """Resolves 'Constants.MaxLength.Name' and 'Domain.Page.NameMaxLength'."""
    expression = expression.strip()
    parts = expression.split(".")
    if len(parts) == 3 and parts[0] == "Constants":
        group = constants.get(parts[1])
        if group is None:
            return None
        return group.get(parts[2])
    if len(parts) == 3 and type_constants is not None:
        owner = type_constants.get(f"{parts[0]}.{parts[1]}")
        if owner is not None:
            return owner.get(parts[2])
    if len(parts) == 1 and own_constants is not None:
        return own_constants.get(parts[0])
    return None


def extract_entities(repository_root: str) -> dict[str, Entity]:
    entities: dict[str, Entity] = {}
    domain_path = os.path.join(repository_root, DOMAIN_DIR)
    for file_path in sorted(walk_files(domain_path)):
        source = strip_comments(read_text(file_path))
        namespace_match = re.search(r"namespace\s+([\w\.]+)", source)
        namespace = namespace_match.group(1) if namespace_match else "Domain"
        for match in re.finditer(
            r"public\s+(?:sealed\s+|abstract\s+)?class\s+(?P<name>\w+)\s*(?::\s*(?P<base>[^{]+?))?\s*\{",
            source,
        ):
            name = match.group("name")
            base_text = match.group("base")
            base_type: str | None = None
            if base_text:
                # Only the first base type is relevant, the rest are interfaces.
                first = base_text.split(",")[0].strip()
                first = re.sub(r"^where\s.*$", "", first, flags=re.DOTALL).strip()
                if first:
                    base_type = normalize_type(first)
            body_start = source.index("{", match.end() - 1)
            body_end = match_brace(source, body_start)
            body = source[body_start + 1 : body_end]
            entity = Entity(clr_name=f"{namespace}.{name}", base_type=base_type)
            for constant in re.finditer(
                r"public\s+const\s+(?:byte|int|short|long)\s+(?P<name>\w+)\s*=\s*(?P<value>\d[\d_]*)",
                source,
            ):
                entity.constants[constant.group("name")] = int(
                    constant.group("value").replace("_", "")
                )
            for property_match in PROPERTY_PATTERN.finditer(body):
                property_type = normalize_type(property_match.group("type"))
                property_name = property_match.group("name")
                attributes = parse_attributes(
                    extract_attribute_block(body, property_match.start())
                )
                if is_collection_type(property_type):
                    entity.navigations.append(
                        {
                            "name": property_name,
                            "type": collection_element_type(property_type) or property_type,
                            "kind": "collection",
                        }
                    )
                    continue
                entity.properties.append(
                    {
                        "name": property_name,
                        "clr_type": property_type,
                        "attributes": attributes,
                    }
                )
            entities[entity.clr_name] = entity
    if not entities:
        raise ModelExtractionError("No entity was found in the Domain project.")
    return entities


# --------------------------------------------------------------------------------------
# Configuration extraction
# --------------------------------------------------------------------------------------


def split_statements(body: str) -> list[str]:
    statements: list[str] = []
    depth = 0
    current: list[str] = []
    for character in body:
        if character in "({[":
            depth += 1
        elif character in ")}]":
            depth -= 1
        if character == ";" and depth == 0:
            statement = "".join(current).strip()
            if statement:
                statements.append(statement)
            current = []
            continue
        current.append(character)
    tail = "".join(current).strip()
    if tail:
        statements.append(tail)
    return statements


def method_call_argument(statement: str, method: str) -> str | None:
    """Returns the text between the parentheses of the first call of ``method``."""
    marker = f".{method}("
    index = statement.find(marker)
    if index < 0:
        return None
    start = index + len(marker)
    depth = 1
    for position in range(start, len(statement)):
        character = statement[position]
        if character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
            if depth == 0:
                return statement[start:position]
    return None


def lambda_properties(lambda_text: str) -> list[str]:
    """Extracts the property names of a lambda such as 'x => new { x.A, x.B }'."""
    lambda_text = lambda_text.strip()
    if "=>" in lambda_text:
        lambda_text = lambda_text.split("=>", 1)[1].strip()
    lambda_text = lambda_text.strip()
    if lambda_text.startswith("new"):
        lambda_text = lambda_text[len("new") :]
        lambda_text = lambda_text.strip()
        if lambda_text.startswith("{"):
            lambda_text = lambda_text[1:]
        if lambda_text.endswith("}"):
            lambda_text = lambda_text[:-1]
    return re.findall(r"\.([A-Za-z_]\w*)", lambda_text)


class Configuration:
    def __init__(self, entity_clr_name: str) -> None:
        self.entity_clr_name = entity_clr_name
        self.table: str | None = None
        self.key: list[str] | None = None
        self.alternate_keys: list[list[str]] = []
        self.indexes: list[dict[str, Any]] = []
        self.properties: dict[str, dict[str, Any]] = {}
        self.foreign_keys: list[dict[str, Any]] = []


def resolve_navigation_types(entities: dict[str, Entity]) -> None:
    """Resolves the namespace relative type names of the navigation properties."""
    for clr_name, entity in entities.items():
        namespace = clr_name.rsplit(".", 1)[0]
        for navigation in entity.navigations:
            navigation_type = navigation["type"]
            candidate = f"{namespace}.{navigation_type}"
            if candidate in entities:
                navigation["type"] = candidate
            elif navigation_type not in entities:
                raise ModelExtractionError(
                    f"{clr_name}.{navigation['name']}: the navigation target "
                    f"'{navigation_type}' is not an entity type."
                )


def extract_configurations(repository_root: str) -> dict[str, Configuration]:
    configurations: dict[str, Configuration] = {}
    configurations_path = os.path.join(repository_root, CONFIGURATIONS_DIR)
    for file_name in sorted(os.listdir(configurations_path)):
        if not file_name.endswith(".cs"):
            continue
        source = strip_comments(read_text(os.path.join(configurations_path, file_name)))
        class_match = re.search(
            r"class\s+(?P<name>\w+)[^{]*?IEntityTypeConfiguration<\s*(?P<entity>[\w\.]+)\s*>",
            source,
            re.DOTALL,
        )
        if class_match is None:
            raise ModelExtractionError(
                f"{file_name}: the entity type configuration could not be identified."
            )
        entity_name = class_match.group("entity")
        entity_clr_name = entity_name if "." in entity_name else f"Domain.{entity_name}"
        configuration = Configuration(entity_clr_name=entity_clr_name)
        configure_match = re.search(r"public\s+void\s+Configure\s*\(", source)
        if configure_match is None:
            raise ModelExtractionError(f"{file_name}: the Configure method was not found.")
        body_start = source.index("{", configure_match.end() - 1)
        body_end = match_brace(source, body_start)
        body = source[body_start + 1 : body_end]
        for statement in split_statements(body):
            apply_configuration_statement(configuration, statement, file_name)
        configurations[configuration.entity_clr_name] = configuration
    return configurations


def apply_configuration_statement(
    configuration: Configuration, statement: str, file_name: str
) -> None:
    normalized = re.sub(r"\s+", " ", statement).strip()

    table_match = re.search(r"\.ToTable\(\s*\"(?P<name>[^\"]+)\"", normalized)
    if table_match:
        configuration.table = table_match.group("name")
        return

    if ".HasAlternateKey(" in normalized:
        lambda_text = method_call_argument(normalized, "HasAlternateKey") or ""
        configuration.alternate_keys.append(lambda_properties(lambda_text))
        return

    if ".HasKey(" in normalized:
        lambda_text = method_call_argument(normalized, "HasKey") or ""
        configuration.key = lambda_properties(lambda_text)
        return

    if ".HasIndex(" in normalized:
        lambda_text = method_call_argument(normalized, "HasIndex") or ""
        columns = lambda_properties(lambda_text)
        index: dict[str, Any] = {
            "columns": columns,
            "unique": False,
            "filter": None,
            "clustered": None,
        }
        unique_match = re.search(
            r"\.IsUnique\(\s*(?:unique\s*:\s*)?(?P<value>true|false)?\s*\)", normalized
        )
        if unique_match is not None:
            # "IsUnique()" means a unique index.
            index["unique"] = unique_match.group("value") != "false"
        filter_match = re.search(r"\.HasFilter\(\s*\"(?P<filter>[^\"]*)\"", normalized)
        if filter_match:
            index["filter"] = filter_match.group("filter")
        clustered_match = re.search(r"\.IsClustered\(\s*(?:clustered\s*:\s*)?(?P<value>true|false)", normalized)
        if clustered_match:
            index["clustered"] = clustered_match.group("value") == "true"
        configuration.indexes.append(index)
        return

    if ".Property(" in normalized:
        lambda_text = method_call_argument(normalized, "Property") or ""
        names = lambda_properties(lambda_text)
        if not names:
            raise ModelExtractionError(f"{file_name}: unsupported Property(...) call.")
        property_name = names[0]
        settings = configuration.properties.setdefault(property_name, {})
        max_length_argument = method_call_argument(normalized, "HasMaxLength")
        if max_length_argument is not None:
            max_length_argument = max_length_argument.strip()
            if ":" in max_length_argument and "=>" not in max_length_argument:
                # A named argument, for example "maxLength: Constants.MaxLength.Name".
                max_length_argument = max_length_argument.split(":", 1)[1].strip()
            if max_length_argument.isdigit():
                settings["max_length"] = int(max_length_argument)
            else:
                settings["max_length_expression"] = max_length_argument
        if ".IsRequired(" in normalized:
            required_match = re.search(
                r"\.IsRequired\(\s*(?:required\s*:\s*)?(?P<value>true|false)?\s*\)",
                normalized,
            )
            if required_match is None:
                raise ModelExtractionError(
                    f"{file_name}: unsupported IsRequired(...) call: {normalized}"
                )
            # "PropertyBuilder.IsRequired(bool required = true)" defaults to 'true', so a
            # call without an argument (".IsRequired()") makes the property required. The
            # nullability of the column follows it, and with it the filter that the
            # 'SqlServerIndexConvention' derives for the unique indexes of the property.
            settings["required"] = required_match.group("value") != "false"
        unicode_match = re.search(r"\.IsUnicode\(\s*(?:unicode\s*:\s*)?(?P<value>true|false)", normalized)
        if unicode_match:
            settings["unicode"] = unicode_match.group("value") == "true"
        column_type_match = re.search(r"\.HasColumnType\(\s*\"(?P<value>[^\"]+)\"", normalized)
        if column_type_match:
            settings["column_type"] = column_type_match.group("value")
        return

    if ".HasOne(" in normalized or ".HasMany(" in normalized:
        configuration.foreign_keys.append(parse_relationship(normalized, file_name))
        return

    raise ModelExtractionError(
        f"{file_name}: the configuration statement is not understood: {normalized}"
    )


def parse_relationship(statement: str, file_name: str) -> dict[str, Any]:
    """Parses a relationship statement of an entity type configuration.

    The navigation names are not enough to identify the two sides of a
    relationship: EF Core resolves them through the generic type arguments of
    the builder, which come from the *type* of the navigation. The parser
    therefore keeps the navigation names and the resolution is done later,
    when the entity types are known.
    """
    relationship: dict[str, Any] = {
        "has_kind": None,
        "has_navigation": None,
        "with_kind": None,
        "with_navigation": None,
        "foreign_key": None,
        "principal_key": None,
        "on_delete": None,
        "required": None,
    }

    has_one_argument = method_call_argument(statement, "HasOne")
    has_many_argument = method_call_argument(statement, "HasMany")
    if has_one_argument is not None:
        relationship["has_kind"] = "one"
        relationship["has_navigation"] = lambda_properties(has_one_argument)
    elif has_many_argument is not None:
        relationship["has_kind"] = "many"
        relationship["has_navigation"] = lambda_properties(has_many_argument)
    else:
        raise ModelExtractionError(f"{file_name}: unsupported relationship: {statement}")

    with_one_argument = method_call_argument(statement, "WithOne")
    with_many_argument = method_call_argument(statement, "WithMany")
    if with_one_argument is not None:
        relationship["with_kind"] = "one"
        relationship["with_navigation"] = lambda_properties(with_one_argument)
    elif with_many_argument is not None:
        relationship["with_kind"] = "many"
        relationship["with_navigation"] = lambda_properties(with_many_argument)
    else:
        raise ModelExtractionError(f"{file_name}: unsupported relationship: {statement}")

    foreign_key_argument = method_call_argument(statement, "HasForeignKey")
    if foreign_key_argument is not None:
        relationship["foreign_key"] = lambda_properties(foreign_key_argument)

    principal_key_argument = method_call_argument(statement, "HasPrincipalKey")
    if principal_key_argument is not None:
        relationship["principal_key"] = lambda_properties(principal_key_argument)

    on_delete = re.search(r"\.OnDelete\([^)]*?DeleteBehavior\.(?P<behavior>\w+)", statement)
    if on_delete:
        relationship["on_delete"] = on_delete.group("behavior")

    required = re.search(r"\.IsRequired\(\s*(?:required\s*:\s*)?(?P<value>true|false)", statement)
    if required:
        relationship["required"] = required.group("value") == "true"

    return relationship


# --------------------------------------------------------------------------------------
# Database context extraction
# --------------------------------------------------------------------------------------


def extract_db_sets(repository_root: str) -> dict[str, str]:
    source = strip_comments(read_text(os.path.join(repository_root, DATABASE_CONTEXT)))
    db_sets: dict[str, str] = {}
    for match in re.finditer(
        r"DbSet<\s*(?P<entity>[\w\.]+)\s*>\s+(?P<name>\w+)\s*\{", source
    ):
        entity_name = match.group("entity")
        clr_name = entity_name if "." in entity_name else f"Domain.{entity_name}"
        db_sets[match.group("name")] = clr_name
    if not db_sets:
        raise ModelExtractionError("No DbSet was found in the DatabaseContext.")
    return db_sets


def extract_date_only_conventions(repository_root: str) -> dict[str, Any]:
    """Reads the DateOnly conventions of the DatabaseContext.

    The convention installs a DateOnly -> DateTime value converter and sets the
    column type. Because the converter changes the provider CLR type, the SQL
    Server store type of such a column is resolved from the *provider* type.
    """
    source = strip_comments(read_text(os.path.join(repository_root, DATABASE_CONTEXT)))
    conventions: dict[str, Any] = {
        "provider_clr_type": None,
        "column_type": None,
        "nullable": False,
    }
    block = re.search(
        r"builder\.Properties<\s*System\.DateOnly\s*>\(\s*\)\s*"
        r"(?P<body>.*?);",
        source,
        re.DOTALL,
    )
    if block is None:
        return conventions
    body = block.group("body")
    converter = re.search(r"HaveConversion<\s*[^>]*?DateOnlyConverter", body)
    if converter:
        conventions["provider_clr_type"] = "DateTime"
    column_type = re.search(r"HaveColumnType\(\s*typeName\s*:\s*nameof\([^)]*\.Date\)\s*\)", body)
    if column_type:
        conventions["column_type"] = "Date"
    return conventions


# --------------------------------------------------------------------------------------
# Model derivation
# --------------------------------------------------------------------------------------

VALUE_TYPES = {
    "bool",
    "byte",
    "sbyte",
    "short",
    "ushort",
    "int",
    "uint",
    "long",
    "ulong",
    "float",
    "double",
    "decimal",
    "char",
    "Guid",
    "DateTime",
    "DateTimeOffset",
    "DateOnly",
    "TimeOnly",
    "TimeSpan",
}


def collect_enums(repository_root: str) -> dict[str, str]:
    """Maps the full name of every enum to its underlying CLR type."""
    enums: dict[str, str] = {}
    domain_path = os.path.join(repository_root, DOMAIN_DIR)
    for file_path in sorted(walk_files(domain_path)):
        source = strip_comments(read_text(file_path))
        namespace_match = re.search(r"namespace\s+([\w\.]+)", source)
        namespace = namespace_match.group(1) if namespace_match else "Domain"
        for match in re.finditer(
            r"public\s+enum\s+(?P<name>\w+)\s*(?::\s*(?P<underlying>[\w]+))?", source
        ):
            underlying = match.group("underlying") or "int"
            enums[f"{namespace}.{match.group('name')}"] = underlying
    return enums


CLR_ALIASES = {
    "bool": "Boolean",
    "byte": "Byte",
    "sbyte": "SByte",
    "char": "Char",
    "decimal": "Decimal",
    "double": "Double",
    "float": "Single",
    "int": "Int32",
    "uint": "UInt32",
    "long": "Int64",
    "ulong": "UInt64",
    "object": "Object",
    "short": "Int16",
    "ushort": "UInt16",
    "string": "String",
}


def resolve_type_name(clr_type: str, namespace: str, enums: dict[str, str]) -> str:
    """Resolves a possibly namespace relative type name against the entity namespace."""
    clr_type = unwrap_nullable(clr_type)
    candidate = f"{namespace}.{clr_type}"
    if candidate in enums:
        return candidate
    return clr_type


def simple_name(clr_type: str) -> str:
    clr_type = unwrap_nullable(clr_type)
    if "<" in clr_type:
        clr_type = clr_type.split("<")[0]
    name = clr_type.split(".")[-1]
    return CLR_ALIASES.get(name, name)


def derive_model(repository_root: str) -> dict[str, Any]:
    entities = extract_entities(repository_root)
    resolve_navigation_types(entities)
    configurations = extract_configurations(repository_root)
    db_sets = extract_db_sets(repository_root)
    date_only = extract_date_only_conventions(repository_root)
    enums = collect_enums(repository_root)
    constants = extract_constants(repository_root)
    type_constants = {
        clr_name: entity.constants for clr_name, entity in entities.items() if entity.constants
    }

    # The abstract base class of every entity carries the primary key and the audit columns.
    base_entity = None
    for entity in entities.values():
        property_names = {property["name"] for property in entity.properties}
        if {"Id", "Ordering", "InsertDateTime"}.issubset(property_names):
            is_used_as_base = any(
                other.base_type
                and other.base_type.split(".")[-1] == entity.clr_name.split(".")[-1]
                for other in entities.values()
            )
            if is_used_as_base:
                base_entity = entity
                break
    if base_entity is None:
        raise ModelExtractionError("The shared base entity (Domain.Seedwork.Entity) was not found.")

    base_property_names = {property["name"] for property in base_entity.properties}

    table_by_entity: dict[str, str] = {}
    for db_set_name, clr_name in db_sets.items():
        table_by_entity[clr_name] = db_set_name
    for clr_name, configuration in configurations.items():
        if configuration.table:
            table_by_entity[clr_name] = configuration.table

    model_entities: list[dict[str, Any]] = []

    for clr_name in sorted(entities):
        entity = entities[clr_name]
        if clr_name == base_entity.clr_name:
            continue
        if entity.base_type and entity.base_type.split(".")[-1] == "Entity":
            properties = list(base_entity.properties) + list(entity.properties)
        else:
            properties = list(entity.properties)
        configuration = configurations.get(clr_name)
        namespace = clr_name.rsplit(".", 1)[0]

        # A property whose type is an entity type is a navigation, not a column.
        scalar_properties: list[dict[str, Any]] = []
        for property_info in properties:
            property_type = property_info["clr_type"]
            if not is_collection_type(property_type) and resolve_entity_type(
                property_type, namespace, entities
            ):
                entity.navigations.append(
                    {
                        "name": property_info["name"],
                        "type": resolve_entity_type(property_type, namespace, entities),
                        "kind": "reference",
                    }
                )
                continue
            scalar_properties.append(property_info)
        properties = scalar_properties

        columns: list[dict[str, Any]] = []
        property_index: dict[str, dict[str, Any]] = {}
        for property_info in properties:
            column = build_column(
                property_info,
                configuration.properties.get(property_info["name"], {})
                if configuration
                else {},
                date_only,
                enums,
                clr_name.rsplit(".", 1)[0],
                constants,
                type_constants,
                entities[clr_name].constants,
            )
            columns.append(column)
            property_index[column["name"]] = column

        primary_key = None
        alternate_keys: list[list[str]] = []
        if configuration is not None and configuration.key:
            primary_key = list(configuration.key)
        elif "Id" in property_index:
            primary_key = ["Id"]
        if configuration is not None:
            alternate_keys = [list(key) for key in configuration.alternate_keys]

        indexes: list[dict[str, Any]] = []
        if configuration is not None:
            for index in configuration.indexes:
                if any(
                    existing["columns"] == list(index["columns"])
                    for existing in indexes
                ):
                    # The same index is declared twice in the configuration.
                    continue
                indexes.append(
                    {
                        "columns": list(index["columns"]),
                        "unique": bool(index["unique"]),
                        "filter": index["filter"],
                        "clustered": index["clustered"],
                    }
                )

        model_entities.append(
            {
                "column_index": {column["name"]: column for column in columns},
                "clr_name": clr_name,
                "table": table_by_entity.get(clr_name, simple_name(clr_name)),
                "columns": columns,
                "primary_key": primary_key,
                "alternate_keys": alternate_keys,
                "indexes": indexes,
                "navigations": entity.navigations,
                "base_property_names": sorted(base_property_names)
                if entity.base_type
                and entity.base_type.split(".")[-1] == "Entity"
                else [],
            }
        )

    entity_by_clr = {entity["clr_name"]: entity for entity in model_entities}
    entity_by_table = {entity["table"]: entity for entity in model_entities}

    # ---- Foreign keys ----------------------------------------------------------------
    configured_relationships: list[tuple[str, dict[str, Any]]] = []
    for clr_name, configuration in configurations.items():
        for relationship in configuration.foreign_keys:
            configured_relationships.append((clr_name, relationship))

    foreign_keys: list[dict[str, Any]] = []
    seen_relationships: set[tuple[str, tuple[str, ...]]] = {}

    for configuration_entity, relationship in configured_relationships:
        has_navigation = (relationship["has_navigation"] or [None])[0]
        with_navigation = (relationship["with_navigation"] or [None])[0]
        if has_navigation is None or with_navigation is None:
            raise ModelExtractionError(
                f"{configuration_entity}: the relationship does not declare its navigations."
            )

        source_entity = entity_by_clr[configuration_entity]

        if relationship["has_kind"] == "one":
            # The source entity is the dependent one, the navigation points to the principal.
            principal_entity = navigation_target(source_entity, has_navigation, entity_by_clr)
            dependent_entity = source_entity
        else:
            # The source entity is the principal one, the collection holds the dependents.
            dependent_entity = navigation_target(source_entity, has_navigation, entity_by_clr)
            principal_entity = source_entity

        # Cross check the inverse navigation.
        inverse_entity = principal_entity if relationship["with_kind"] == "many" else dependent_entity
        inverse_target = navigation_target(inverse_entity, with_navigation, entity_by_clr)
        expected = dependent_entity if relationship["with_kind"] == "many" else principal_entity
        if inverse_target["clr_name"] != expected["clr_name"]:
            raise ModelExtractionError(
                f"{configuration_entity}: the inverse navigation '{with_navigation}' of the "
                f"relationship does not point to '{expected['clr_name']}' but to "
                f"'{inverse_target['clr_name']}'."
            )

        foreign_key_columns = relationship["foreign_key"]
        if not foreign_key_columns:
            # The foreign key follows the naming convention: <Navigation>Id.
            foreign_key_columns = [f"{has_navigation}Id"]

        if relationship["principal_key"]:
            principal_columns = list(relationship["principal_key"])
        else:
            principal_columns = list(principal_entity["primary_key"] or ["Id"])

        required = relationship["required"]
        if required is None:
            required = all(
                not dependent_entity["column_index"][column]["nullable"]
                for column in foreign_key_columns
            )

        on_delete = relationship["on_delete"] or ("Cascade" if required else "ClientSetNull")

        key = (dependent_entity["clr_name"], tuple(foreign_key_columns))
        if key in seen_relationships:
            # The same foreign key is configured from both sides. The configurations are
            # applied in the order of their file names, therefore the last one wins.
            existing = seen_relationships[key]
            if relationship["required"] is not None:
                existing["required"] = bool(relationship["required"])
            if relationship["on_delete"] is not None:
                existing["on_delete"] = relationship["on_delete"]
            if relationship["principal_key"]:
                existing["principal_columns"] = list(relationship["principal_key"])
            continue

        if relationship["has_kind"] == "one":
            dependent_navigation = has_navigation
            principal_navigation = with_navigation if relationship["with_kind"] == "many" else None
        else:
            dependent_navigation = with_navigation
            principal_navigation = has_navigation

        foreign_key = {
            "dependent": dependent_entity["table"],
            "dependent_entity": dependent_entity["clr_name"],
            "columns": list(foreign_key_columns),
            "principal_table": principal_entity["table"],
            "principal_entity": principal_entity["clr_name"],
            "principal_columns": principal_columns,
            "on_delete": on_delete,
            "required": bool(required),
            "configured": True,
            "dependent_navigation": dependent_navigation,
            "principal_navigation": principal_navigation,
        }
        foreign_key["name"] = "FK_{}_{}_{}".format(
            foreign_key["dependent"],
            foreign_key["principal_table"],
            "_".join(foreign_key["columns"]),
        )
        foreign_keys.append(foreign_key)
        seen_relationships[key] = foreign_key

    # Relationships that are only discovered by the EF Core conventions.
    for dependent_clr_name, entity in entity_by_clr.items():
        for navigation in entity["navigations"]:
            if navigation["kind"] != "reference":
                continue
            target = navigation["type"]
            if target not in entity_by_clr:
                continue
            convention_columns = [f"{navigation['name']}Id"]
            if not all(column in entity["column_index"] for column in convention_columns):
                continue
            key = (dependent_clr_name, tuple(convention_columns))
            if key in seen_relationships:
                continue
            if any(
                convention_column in foreign_key["columns"]
                for foreign_key in foreign_keys
                if foreign_key["dependent_entity"] == dependent_clr_name
                for convention_column in convention_columns
            ):
                # The foreign key of this navigation is already configured explicitly.
                continue

            principal_entity = entity_by_clr[target]
            required = all(
                not entity["column_index"][column]["nullable"] for column in convention_columns
            )
            on_delete = "Cascade" if required else "ClientSetNull"
            principal_navigation = None
            for candidate in principal_entity["navigations"]:
                if candidate["kind"] == "collection" and candidate["type"] == dependent_clr_name:
                    principal_navigation = candidate["name"]
                    break
            foreign_key = {
                "dependent": entity["table"],
                "dependent_entity": dependent_clr_name,
                "columns": convention_columns,
                "principal_table": principal_entity["table"],
                "principal_entity": target,
                "principal_columns": list(principal_entity["primary_key"] or ["Id"]),
                "on_delete": on_delete,
                "required": required,
                "configured": False,
                "dependent_navigation": navigation["name"],
                "principal_navigation": principal_navigation,
            }
            foreign_key["name"] = "FK_{}_{}_{}".format(
                foreign_key["dependent"],
                foreign_key["principal_table"],
                "_".join(foreign_key["columns"]),
            )
            foreign_keys.append(foreign_key)
            seen_relationships[key] = foreign_key

    for entity in model_entities:
        entity["foreign_keys"] = [
            foreign_key
            for foreign_key in foreign_keys
            if foreign_key["dependent_entity"] == entity["clr_name"]
        ]

    # ---- Index names -----------------------------------------------------------------
    for entity in model_entities:
        for index in entity["indexes"]:
            index["name"] = "IX_{}_{}".format(
                entity["table"], "_".join(index["columns"])
            )

            # The SQL Server provider adds a filter to every unique, non clustered
            # index that contains a nullable column, so that the null values do not
            # violate the uniqueness. (SqlServerIndexConvention)
            if (
                index["unique"]
                and index["clustered"] is not True
                and index["filter"] is None
            ):
                nullable_columns = [
                    column
                    for column in index["columns"]
                    if entity["column_index"][column]["nullable"]
                ]
                if nullable_columns:
                    index["filter"] = " AND ".join(
                        f"[{column}] IS NOT NULL" for column in nullable_columns
                    )

    return {
        "entities": model_entities,
        "foreign_keys": foreign_keys,
        "enums": enums,
        "db_set_order": list(db_sets.values()),
    }


def resolve_entity_type(
    clr_type: str, namespace: str, entities: dict[str, Entity]
) -> str | None:
    """Returns the CLR name of the entity type of a navigation, or None."""
    clr_type = unwrap_nullable(clr_type)
    candidates = [clr_type]
    if not clr_type.startswith(namespace + "."):
        candidates.insert(0, f"{namespace}.{clr_type}")
    for candidate in candidates:
        if candidate in entities:
            return candidate
    return None


def navigation_target(
    entity: dict[str, Any], navigation_name: str, entity_by_clr: dict[str, dict[str, Any]]
) -> dict[str, Any]:
    """Returns the entity type that a navigation of an entity points to."""
    for navigation in entity["navigations"]:
        if navigation["name"] == navigation_name and navigation["type"] in entity_by_clr:
            return entity_by_clr[navigation["type"]]
    raise ModelExtractionError(
        f"{entity['clr_name']}.{navigation_name}: the navigation target is unknown."
    )


def is_configured_navigation(
    configured_relationships: list[tuple[str, dict[str, Any]]],
    dependent_clr_name: str,
    navigation_name: str,
) -> bool:
    for owner, relationship in configured_relationships:
        dependent_navigation = relationship["dependent_navigation"] or []
        principal_navigation = relationship["principal_navigation"] or []
        if navigation_name in dependent_navigation and owner == dependent_clr_name:
            return True
        if navigation_name in principal_navigation:
            return True
    return False


def resolve_navigation_target(
    dependent_clr_name: str,
    navigation_name: str,
    entity_by_clr: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    """Resolves the entity type of a navigation property."""
    for entity in entity_by_clr.values():
        for navigation in entity["navigations"]:
            if navigation["name"] == navigation_name and navigation["type"] in entity_by_clr:
                if entity["clr_name"] == dependent_clr_name:
                    return entity_by_clr[navigation["type"]]
    # Fall back to a unique match on the navigation name.
    candidates = [
        entity_by_clr[navigation["type"]]
        for entity in entity_by_clr.values()
        for navigation in entity["navigations"]
        if navigation["name"] == navigation_name and navigation["type"] in entity_by_clr
    ]
    if len(candidates) == 1:
        return candidates[0]
    raise ModelExtractionError(
        f"{dependent_clr_name}.{navigation_name}: the navigation target is ambiguous."
    )


def build_column(
    property_info: dict[str, Any],
    settings: dict[str, Any],
    date_only: dict[str, Any],
    enums: dict[str, str],
    namespace: str,
    constants: dict[str, dict[str, int]],
    type_constants: dict[str, dict[str, int]],
    own_constants: dict[str, int],
) -> dict[str, Any]:
    name = property_info["name"]
    clr_type = property_info["clr_type"]
    attributes = property_info["attributes"]

    nullable = is_nullable_reference(clr_type) or unwrap_nullable(clr_type) in {
        "System.Guid?",
        "System.DateTime?",
    }
    unwrapped = unwrap_nullable(clr_type)
    simple = simple_name(unwrapped)

    # Data annotations.
    value_generated = None
    for attribute in attributes:
        name_of_attribute = attribute_name(attribute)
        if name_of_attribute == "Required":
            nullable = False
        elif name_of_attribute == "MaxLength":
            length = attribute_argument(attribute, "length")
            if length:
                value = None
                if length.isdigit():
                    value = int(length)
                else:
                    value = resolve_constant(
                        length, constants, type_constants, own_constants
                    )
                if value is None:
                    raise ModelExtractionError(
                        f"The maximum length '{length}' of the property '{name}' could not be resolved."
                    )
                settings = dict(settings)
                settings.setdefault("max_length", value)
        elif name_of_attribute == "DatabaseGenerated":
            option_match = re.search(
                r"DatabaseGeneratedOption\s*\.\s*(?P<option>\w+)", attribute
            )
            if option_match is None:
                raise ModelExtractionError(
                    f"The [DatabaseGenerated] attribute of the property '{name}' could "
                    "not be read."
                )
            # 'DatabaseGeneratedAttributeConvention' (EF Core) maps the attribute to
            # 'ValueGenerated' and applies it with 'ConfigurationSource.DataAnnotation',
            # so it wins over the 'ValueGenerationConvention', which uses
            # 'ConfigurationSource.Convention'. 'Domain.Seedwork.Entity.Id' therefore
            # keeps 'ValueGenerated.Never' even though it is a Guid primary key, and the
            # snapshot must not claim '.ValueGeneratedOnAdd()' for it.
            try:
                value_generated = {
                    "None": "Never",
                    "Identity": "OnAdd",
                    "Computed": "OnAddOrUpdate",
                }[option_match.group("option")]
            except KeyError:
                raise ModelExtractionError(
                    f"The [DatabaseGenerated] option '{option_match.group('option')}' of "
                    f"the property '{name}' is unknown."
                ) from None

    max_length_expression = settings.pop("max_length_expression", None)
    if max_length_expression is not None:
        value = resolve_constant(max_length_expression, constants, type_constants)
        if value is None:
            raise ModelExtractionError(
                f"The maximum length '{max_length_expression}' of the property '{name}' "
                "could not be resolved."
            )
        settings = dict(settings)
        settings["max_length"] = value

    if "required" in settings:
        nullable = not settings["required"]

    unicode = settings.get("unicode")
    max_length = settings.get("max_length")

    provider_clr_type = CLR_TYPE_NAMES.get(unwrapped, unwrapped)
    store_type = ""
    snapshot_clr_type = provider_clr_type
    snapshot_column_type = None

    if simple == "String":
        is_ansi = unicode is False
        if max_length is not None:
            store_type = f"{'varchar' if is_ansi else 'nvarchar'}({max_length})"
        else:
            store_type = "varchar(max)" if is_ansi else "nvarchar(max)"
    elif simple == "Guid":
        store_type = STORE_TYPE_GUID
    elif simple == "Boolean":
        store_type = STORE_TYPE_BOOL
    elif simple == "Int32":
        store_type = STORE_TYPE_INT
    elif simple == "Int64":
        store_type = STORE_TYPE_BIGINT
    elif simple == "Int16":
        store_type = STORE_TYPE_SMALLINT
    elif simple == "Byte":
        store_type = STORE_TYPE_TINYINT
    elif simple == "Single":
        store_type = STORE_TYPE_REAL
    elif simple == "Double":
        store_type = STORE_TYPE_FLOAT
    elif simple == "Decimal":
        store_type = STORE_TYPE_DECIMAL
    elif simple == "DateTime":
        store_type = STORE_TYPE_DATETIME2
    elif simple == "DateOnly":
        # The DateOnly -> DateTime value converter changes the provider CLR type to
        # DateTime. The "HaveColumnType(\"Date\")" convention is applied on top of it. The
        # store type of a column that has an explicit column type is that column type
        # ("RelationalTypeMapping.WithTypeMappingInfo" keeps the requested name), so the
        # store type is "Date" and not "date".
        snapshot_column_type = date_only.get("column_type")
        store_type = snapshot_column_type or STORE_TYPE_DATE
        provider_clr_type = "System.DateTime"
        snapshot_clr_type = "System.DateTime"
    elif resolve_type_name(unwrapped, namespace, enums) in enums:
        underlying = enums[resolve_type_name(unwrapped, namespace, enums)]
        store_type = {
            "byte": STORE_TYPE_TINYINT,
            "sbyte": STORE_TYPE_TINYINT,
            "short": STORE_TYPE_SMALLINT,
            "ushort": STORE_TYPE_SMALLINT,
            "int": STORE_TYPE_INT,
            "uint": STORE_TYPE_INT,
            "long": STORE_TYPE_BIGINT,
            "ulong": STORE_TYPE_BIGINT,
        }[underlying]
        provider_clr_type = CLR_TYPE_NAMES.get(underlying, underlying)
        snapshot_clr_type = provider_clr_type
    else:
        raise ModelExtractionError(
            f"The CLR type '{clr_type}' of the property '{name}' has no known SQL Server mapping."
        )

    return {
        "name": name,
        "clr_type": clr_type,
        "snapshot_clr_type": snapshot_clr_type,
        "nullable": nullable,
        "store_type": store_type,
        "snapshot_column_type": snapshot_column_type,
        "max_length": max_length,
        "unicode": unicode,
        "provider_clr_type": provider_clr_type,
        "value_generated": value_generated,
    }


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--json", action="store_true", help="Print the model as JSON.")
    arguments = parser.parse_args(argv)

    model = derive_model(arguments.repo_root)
    if arguments.json:
        print(json.dumps(model, indent=2, sort_keys=True, default=str))
    else:
        for entity in model["entities"]:
            print(f"{entity['clr_name']} -> {entity['table']}")
            for column in entity["columns"]:
                print(
                    f"    {column['name']:<32} {column['store_type']:<20}"
                    f" nullable={column['nullable']}"
                )
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1:]))
    except ModelExtractionError as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(2)
