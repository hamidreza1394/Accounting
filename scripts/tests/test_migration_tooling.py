#!/usr/bin/env python3
"""Regression tests of the migration guard tooling (ef_model.py, migration_model.py).

Run: python3 scripts/tests/test_migration_tooling.py
Covers the relationship form that PR #68 introduced for 'Users.DefaultCompanyId':
    builder.HasOne<Domain.Company>().WithMany().HasForeignKey(...).OnDelete(SetNull)
The CI step 'Check the generated migrations' failed on it ("the configuration statement is
not understood"). Nothing here weakens a guard: the negative tests prove that an incomplete
navigation-less relationship is still rejected.
"""
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

SCRIPTS = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
REPO = os.path.abspath(os.path.join(SCRIPTS, ".."))
sys.path.insert(0, SCRIPTS)

import ef_model  # noqa: E402
import migration_model  # noqa: E402
import verify_migration_model  # noqa: E402

STATEMENT = (
    "builder .HasOne<Domain.Company>() .WithMany() "
    ".HasForeignKey(current => current.DefaultCompanyId) "
    ".OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.SetNull)"
)
USER_CONFIGURATION = os.path.join(
    "Infrastructure", "Persistence", "Configurations", "UserConfiguration.cs"
)


class ParseRelationshipTests(unittest.TestCase):
    def test_generic_navigationless_has_one_with_many(self):
        relationship = ef_model.parse_relationship(STATEMENT, "UserConfiguration.cs")
        self.assertEqual("one", relationship["has_kind"])
        self.assertEqual("many", relationship["with_kind"])
        self.assertEqual("Domain.Company", relationship["has_type"])
        self.assertEqual([], relationship["has_navigation"])
        self.assertEqual([], relationship["with_navigation"])
        self.assertEqual(["DefaultCompanyId"], relationship["foreign_key"])
        self.assertEqual("SetNull", relationship["on_delete"])

    def test_navigation_form_is_unchanged(self):
        relationship = ef_model.parse_relationship(
            "builder.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId)",
            "UserConfiguration.cs",
        )
        self.assertEqual(["Role"], relationship["has_navigation"])
        self.assertEqual(["Users"], relationship["with_navigation"])
        self.assertIsNone(relationship["has_type"])


class DerivedModelTests(unittest.TestCase):
    def test_default_company_foreign_key_is_in_the_model(self):
        model = ef_model.derive_model(REPO)
        user = next(e for e in model["entities"] if e["clr_name"] == "Domain.User")
        matches = [f for f in user["foreign_keys"] if f["columns"] == ["DefaultCompanyId"]]
        self.assertEqual(1, len(matches))
        foreign_key = matches[0]
        self.assertEqual("Domain.Company", foreign_key["principal_entity"])
        self.assertEqual("SetNull", foreign_key["on_delete"])
        self.assertFalse(foreign_key["required"])
        self.assertFalse(foreign_key["unique"])
        self.assertIsNone(foreign_key["dependent_navigation"])
        self.assertIsNone(foreign_key["principal_navigation"])

    def _model_with_user_configuration(self, transform):
        with tempfile.TemporaryDirectory() as root:
            for part in ("Domain", "Infrastructure", "Constants"):
                source = os.path.join(REPO, part)
                if os.path.isdir(source):
                    shutil.copytree(
                        source,
                        os.path.join(root, part),
                        ignore=shutil.ignore_patterns("bin", "obj", "Migrations"),
                    )
            path = os.path.join(root, USER_CONFIGURATION)
            with open(path, encoding="utf-8") as file:
                text = file.read()
            changed = transform(text)
            self.assertNotEqual(text, changed, "the test fixture did not change")
            with open(path, "w", encoding="utf-8") as file:
                file.write(changed)
            return ef_model.derive_model(root)

    def test_navigationless_relationship_without_foreign_key_is_rejected(self):
        def drop_foreign_key(text):
            return text.replace(
                ".HasForeignKey(current => current.DefaultCompanyId)\n\t\t\t", "", 1
            )

        with self.assertRaises(ef_model.ModelExtractionError):
            self._model_with_user_configuration(drop_foreign_key)

    def test_unknown_generic_entity_type_is_rejected(self):
        with self.assertRaises(ef_model.ModelExtractionError):
            self._model_with_user_configuration(
                lambda text: text.replace("HasOne<Domain.Company>", "HasOne<Domain.NoSuchType>", 1)
            )


def _column(name, clr="System.Int32", nullable=False, column_type="int", max_length=None, unicode=None):
    return {
        "name": name,
        "clr_type": clr,
        "nullable": nullable,
        "max_length": max_length,
        "unicode": unicode,
        "value_generated": False,
        "column_type": column_type,
    }


def _entity(clr_name, table, columns):
    return {
        "clr_name": clr_name,
        "table": table,
        "columns": columns,
        "primary_key": ["Id"],
        "alternate_keys": [],
        "indexes": [],
    }


def _designer(items_columns, relationships=()):
    id_column = _column("Id", "System.Guid", False, "uniqueidentifier")
    return {
        "entities": {
            "Domain.Item": _entity("Domain.Item", "Items", [id_column, *items_columns]),
            "Domain.Owner": _entity("Domain.Owner", "Owners", [id_column]),
        },
        "relationships": list(relationships),
    }


def _migration(up, down):
    return (
        "partial class Synthetic : Migration\n{\n"
        f"protected override void Up(MigrationBuilder migrationBuilder)\n{{\n{up}\n}}\n"
        f"protected override void Down(MigrationBuilder migrationBuilder)\n{{\n{down}\n}}\n}}\n"
    )


DROP_QTY = 'migrationBuilder.DropColumn(name: "Qty", table: "Items");'


class AddColumnOperationTests(unittest.TestCase):
    """AddColumn is supported for a fixed set of facets; anything else fails closed."""

    def step(self, column, up, down=DROP_QTY):
        return verify_migration_model.verify_step(
            "Synthetic", _designer([]), _designer([column]), _migration(up, down)
        )

    def add(self, generic, type_, nullable, extra=""):
        return (
            f'migrationBuilder.AddColumn<{generic}>(name: "Qty", table: "Items", '
            f'type: "{type_}", nullable: {str(nullable).lower()}{extra});'
        )

    def test_added_column_is_an_operation(self):
        source = _entity("Domain.Item", "Items", [_column("Id")])
        target = _entity("Domain.Item", "Items", [_column("Id"), _column("Qty", nullable=True)])
        operations = migration_model._column_operations(source, target)
        self.assertEqual(["add_column"], [o["kind"] for o in operations])

    def test_dropped_column_still_requires_a_hand_written_migration(self):
        source = _entity("Domain.Item", "Items", [_column("Id"), _column("Old")])
        target = _entity("Domain.Item", "Items", [_column("Id")])
        with self.assertRaises(migration_model.MigrationModelError):
            migration_model._column_operations(source, target)

    # ---- positive -----------------------------------------------------------------------
    def test_nullable_column_without_default_is_accepted(self):
        column = _column("Qty", nullable=True)
        self.assertEqual([], self.step(column, self.add("int", "int", True)))

    def test_not_null_columns_need_the_clr_default(self):
        cases = [
            (_column("Qty", "System.Int32", False, "int"), "int", "int", "0"),
            (_column("Qty", "System.Int64", False, "bigint"), "long", "bigint", "0L"),
            (_column("Qty", "System.Boolean", False, "bit"), "bool", "bit", "false"),
            (_column("Qty", "System.Guid", False, "uniqueidentifier"), "Guid", "uniqueidentifier",
             'new Guid("00000000-0000-0000-0000-000000000000")'),
            (_column("Qty", "System.DateTime", False, "datetime2"), "DateTime", "datetime2",
             "new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)"),
        ]
        for column, generic, type_, default in cases:
            with self.subTest(type=type_):
                self.assertEqual([], self.step(column, self.add(generic, type_, False, f", defaultValue: {default}")))

    def test_string_column_with_length(self):
        column = _column("Qty", "System.String", False, "nvarchar(50)", max_length=50)
        up = self.add("string", "nvarchar(50)", False, ', maxLength: 50, defaultValue: ""')
        self.assertEqual([], self.step(column, up))

    # ---- negative -----------------------------------------------------------------------
    def assertRejected(self, column, up, down=DROP_QTY):
        try:
            problems = self.step(column, up, down)
        except (verify_migration_model.VerificationError, migration_model.MigrationModelError):
            return
        self.assertNotEqual([], problems, "the invalid AddColumn was accepted")

    def test_not_null_column_without_default_is_rejected(self):
        self.assertRejected(_column("Qty"), self.add("int", "int", False))

    def test_wrong_default_is_rejected(self):
        self.assertRejected(_column("Qty"), self.add("int", "int", False, ", defaultValue: 1"))

    def test_default_on_a_nullable_column_is_rejected(self):
        self.assertRejected(_column("Qty", nullable=True), self.add("int", "int", True, ", defaultValue: 0"))

    def test_wrong_type_nullable_clr_and_length_are_rejected(self):
        column = _column("Qty", nullable=True)
        self.assertRejected(column, self.add("int", "bigint", True))
        self.assertRejected(column, self.add("long", "int", True))
        self.assertRejected(column, self.add("int", "int", False, ", defaultValue: 0"))
        self.assertRejected(column, self.add("int", "int", True, ", maxLength: 5"))

    def test_missing_max_length_is_rejected(self):
        column = _column("Qty", "System.String", True, "nvarchar(50)", max_length=50)
        self.assertRejected(column, self.add("string", "nvarchar(50)", True))

    def test_unknown_arguments_are_rejected(self):
        column = _column("Qty", nullable=True)
        for extra in (', comment: "x"', ", precision: 5", ", computedColumnSql: \"1\"", ", rowVersion: true"):
            with self.subTest(extra=extra):
                self.assertRejected(column, self.add("int", "int", True, extra))

    def test_not_null_column_of_an_unknown_type_is_refused(self):
        column = _column("Qty", "System.TimeSpan", False, "time")
        with self.assertRaises(migration_model.MigrationModelError):
            self.step(column, self.add("TimeSpan", "time", False, ", defaultValue: 0"))

    def test_wrong_or_missing_down_is_rejected(self):
        column = _column("Qty", nullable=True)
        up = self.add("int", "int", True)
        self.assertRejected(column, up, "")
        self.assertRejected(column, up, 'migrationBuilder.DropColumn(name: "Other", table: "Items");')
        self.assertRejected(column, up, DROP_QTY + '\nmigrationBuilder.DropColumn(name: "X", table: "Items");')


class AddForeignKeyOperationTests(unittest.TestCase):
    RELATIONSHIP = {
        "dependent": "Domain.Item",
        "principal": "Domain.Owner",
        "navigation": None,
        "columns": ["OwnerId"],
        "principal_columns": None,
        "on_delete": "SetNull",
        "required": False,
    }

    def step(self, up, down):
        owner_id = _column("OwnerId", "System.Guid", True, "uniqueidentifier")
        previous = _designer([owner_id])
        current = _designer([owner_id], [self.RELATIONSHIP])
        current["entities"]["Domain.Item"]["indexes"] = []
        return verify_migration_model.verify_step("Synthetic", previous, current, _migration(up, down))

    FK = (
        'migrationBuilder.AddForeignKey(name: "FK_Items_Owners_OwnerId", table: "Items", '
        'column: "OwnerId", principalTable: "Owners", principalColumn: "Id", '
        "onDelete: ReferentialAction.{action});"
    )
    DROP = 'migrationBuilder.DropForeignKey(name: "FK_Items_Owners_OwnerId", table: "Items");'

    def test_matching_foreign_key_is_accepted(self):
        self.assertEqual([], self.step(self.FK.format(action="SetNull"), self.DROP))

    def test_wrong_action_missing_foreign_key_and_missing_drop_are_rejected(self):
        self.assertNotEqual([], self.step(self.FK.format(action="Cascade"), self.DROP))
        self.assertNotEqual([], self.step("", self.DROP))
        self.assertNotEqual([], self.step(self.FK.format(action="SetNull"), ""))
        wrong_table = self.FK.format(action="SetNull").replace('principalTable: "Owners"', 'principalTable: "Items"')
        self.assertNotEqual([], self.step(wrong_table, self.DROP))

    def test_removed_or_changed_relationship_is_refused(self):
        owner_id = _column("OwnerId", "System.Guid", True, "uniqueidentifier")
        with_fk = _designer([owner_id], [self.RELATIONSHIP])
        without_fk = _designer([owner_id])
        with self.assertRaises(migration_model.MigrationModelError):
            migration_model.compute_operations(with_fk, without_fk)
        changed = dict(self.RELATIONSHIP, on_delete="Cascade")
        with self.assertRaises(migration_model.MigrationModelError):
            migration_model.compute_operations(with_fk, _designer([owner_id], [changed]))


class ChainDiscoveryTests(unittest.TestCase):
    def test_the_chain_is_read_from_the_directory_in_order(self):
        directory = os.path.join(REPO, "Infrastructure", "Persistence", "Migrations")
        chain = verify_migration_model.migration_chain(directory)
        self.assertEqual(sorted(chain), chain)
        self.assertEqual(verify_migration_model.INITIAL_MIGRATION, chain[0])
        self.assertTrue(all("." not in name for name in chain))
        self.assertGreaterEqual(len(chain), 6)


class GuardScriptsTests(unittest.TestCase):
    def _run(self, *arguments):
        return subprocess.run(
            [sys.executable, *arguments, "--repo-root", REPO],
            capture_output=True,
            text=True,
        )

    def test_generated_files_are_up_to_date(self):
        result = self._run(os.path.join(SCRIPTS, "generate_migrations.py"), "--check")
        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
