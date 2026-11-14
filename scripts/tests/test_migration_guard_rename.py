#!/usr/bin/env python3
"""Tests of the controlled rename support of the migration guard (S3-0h PR-2b).

A migration that renames a column or an index is accepted only when

* the rename is on the reviewed allowlist (``ALLOWED_RENAMES`` of
  ``verify_migration_model.py``),
* the migration contains nothing but those renames in ``Up`` and exactly their reverse in
  ``Down``,
* the model after the migration is the model before it with exactly those renames
  applied: every key, index and foreign key keeps its name (the explicit pins of PR-2a), so
  the rename is never read as a drop and re-creation of a constraint.

The tests run the REAL ``verify_step`` / ``verify`` and the real generated-file parser on
the model of the newest designer file of the repository; the "after" model is produced by
a rename helper of this file that is independent of the guard. Nothing is mocked.

These tests prove the *guard*. They do not prove that SQL Server executes a rename without
harming the foreign keys, alternate keys and indexes; that needs the SQL Server validation
of S3-0h (gate G3) and is NOT covered here.

Run: python3 scripts/tests/test_migration_guard_rename.py   (needs tree-sitter, like CI)
"""
import copy
import os
import sys
import unittest

SCRIPTS = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
REPO = os.path.abspath(os.path.join(SCRIPTS, ".."))
sys.path.insert(0, SCRIPTS)

import migration_model as mm  # noqa: E402
import verify_migration_model as vm  # noqa: E402

DIRECTORY = os.path.join(REPO, vm.MIGRATIONS_DIRECTORY)
MIGRATION = "20271101000000_RenameCompanyRoleTenantColumn"
MIGRATION_ID = "20271101000000"

TABLE = "CompanyRoles"
OLD_COLUMN = "TenantId"
NEW_COLUMN = "CompanyId"
OLD_INDEX = "IX_CompanyRoles_TenantId_Code"
NEW_INDEX = "IX_CompanyRoles_CompanyId_Code"

COLUMN_ALLOWLIST = {MIGRATION_ID: {"columns": [(TABLE, OLD_COLUMN, NEW_COLUMN)], "indexes": []}}
BOTH_ALLOWLIST = {
    MIGRATION_ID: {
        "columns": [(TABLE, OLD_COLUMN, NEW_COLUMN)],
        "indexes": [(TABLE, OLD_INDEX, NEW_INDEX)],
    }
}


def migration_source(up_operations, down_operations):
    def body(operations):
        return "\n".join(f"            {operation}" for operation in operations)

    return (
        "using Microsoft.EntityFrameworkCore.Migrations;\n\n#nullable disable\n\n"
        "namespace Data.Migrations\n{\n    public partial class RenameCompanyRoleTenantColumn : Migration\n    {\n"
        "        protected override void Up(MigrationBuilder migrationBuilder)\n        {\n"
        f"{body(up_operations)}\n        }}\n\n"
        "        protected override void Down(MigrationBuilder migrationBuilder)\n        {\n"
        f"{body(down_operations)}\n        }}\n    }}\n}}\n"
    )


def rename_column_call(old, new, table=TABLE):
    return f'migrationBuilder.RenameColumn(name: "{old}", table: "{table}", newName: "{new}");'


def rename_index_call(old, new, table=TABLE):
    return f'migrationBuilder.RenameIndex(name: "{old}", table: "{table}", newName: "{new}");'


def entity_of(model, table):
    return next(entity for entity in model["entities"].values() if entity["table"] == table)


def renamed_model(model, table, old, new, keep_names=True):
    """The model after a rename of a column (independent of the guard).

    Every reference to the column follows it. With ``keep_names`` the names of the keys,
    indexes and foreign keys stay as they were (pinned); without it they are the names that
    EF Core derives from the new column.
    """
    after = copy.deepcopy(model)
    entity = entity_of(after, table)
    swap = lambda columns: [new if column == old else column for column in columns]  # noqa: E731
    for column in entity["columns"]:
        if column["name"] == old:
            column["name"] = new
    entity["primary_key"] = swap(entity["primary_key"])
    entity["alternate_keys"] = [swap(key) for key in entity["alternate_keys"]]
    for index in entity["indexes"]:
        index["columns"] = swap(index["columns"])
    for relationship in after["relationships"]:
        if after["entities"][relationship["dependent"]] is entity:
            relationship["columns"] = swap(relationship["columns"])
        if after["entities"][relationship["principal"]] is entity and relationship["principal_columns"]:
            relationship["principal_columns"] = swap(relationship["principal_columns"])
    if not keep_names:
        entity["primary_key_name"] = f"PK_{table}"
        entity["alternate_key_names"] = [
            "AK_{}_{}".format(table, "_".join(key)) for key in entity["alternate_keys"]
        ]
        for index in entity["indexes"]:
            index["name"] = "IX_{}_{}".format(table, "_".join(index["columns"]))
        for relationship in after["relationships"]:
            if after["entities"][relationship["dependent"]] is entity:
                principal = after["entities"][relationship["principal"]]
                relationship["name"] = "FK_{}_{}_{}".format(
                    table, principal["table"], "_".join(relationship["columns"])
                )
    return after


def pin(model, table):
    """Makes the derived names of a table explicit: the pins of PR-2a (a no-op for the model)."""
    return model


class RenameGuardBase(unittest.TestCase):
    before = None

    @classmethod
    def setUpClass(cls):
        chain = vm.migration_chain(DIRECTORY)
        designer = os.path.join(DIRECTORY, f"{chain[-1]}.Designer.cs")
        cls.before = mm.parse_generated_model(mm.read_text(designer))

    def step(self, after, source, allowed=None):
        return vm.verify_step(
            MIGRATION, self.before, after, source,
            allowed_renames=COLUMN_ALLOWLIST if allowed is None else allowed,
        )

    def good_after(self):
        return renamed_model(self.before, TABLE, OLD_COLUMN, NEW_COLUMN)

    def good_source(self):
        return migration_source(
            [rename_column_call(OLD_COLUMN, NEW_COLUMN)],
            [rename_column_call(NEW_COLUMN, OLD_COLUMN)],
        )

    def assert_rejected(self, after, source, allowed=None, text=None):
        try:
            problems = self.step(after, source, allowed)
        except (mm.MigrationModelError, vm.VerificationError) as error:
            problems = [str(error)]
        self.assertTrue(problems, "the guard accepted a migration that it has to refuse")
        if text is not None:
            self.assertIn(text.lower(), " ".join(problems).lower())


class AcceptedRenameTests(RenameGuardBase):
    def test_the_fixture_has_the_names_that_the_tests_rely_on(self):
        entity = entity_of(self.before, TABLE)
        self.assertIn(OLD_COLUMN, [column["name"] for column in entity["columns"]])
        self.assertIn(OLD_INDEX, [index["name"] for index in entity["indexes"]])

    def test_a_reviewed_column_rename_with_pinned_names_is_accepted(self):
        self.assertEqual([], self.step(self.good_after(), self.good_source()))

    def test_a_reviewed_index_rename_is_accepted(self):
        after = self.good_after()
        for index in entity_of(after, TABLE)["indexes"]:
            if index["name"] == OLD_INDEX:
                index["name"] = NEW_INDEX
        source = migration_source(
            [rename_column_call(OLD_COLUMN, NEW_COLUMN), rename_index_call(OLD_INDEX, NEW_INDEX)],
            [rename_index_call(NEW_INDEX, OLD_INDEX), rename_column_call(NEW_COLUMN, OLD_COLUMN)],
        )
        self.assertEqual([], self.step(after, source, BOTH_ALLOWLIST))


class RefusedRenameTests(RenameGuardBase):
    def test_a_rename_without_an_allowlist_entry_is_refused(self):
        self.assert_rejected(self.good_after(), self.good_source(), allowed={}, text="allowlist")

    def test_a_rename_outside_the_allowlist_is_refused(self):
        other = {MIGRATION_ID: {"columns": [(TABLE, "Name", "Title")], "indexes": []}}
        self.assert_rejected(self.good_after(), self.good_source(), allowed=other)

    def test_a_removed_pin_is_refused(self):
        after = renamed_model(self.before, TABLE, OLD_COLUMN, NEW_COLUMN, keep_names=False)
        self.assert_rejected(after, self.good_source(), text="pin")

    def test_an_incomplete_rename_is_refused(self):
        # The column is renamed but an index still names the old column.
        after = self.good_after()
        entity_of(after, TABLE)["indexes"][0]["columns"] = [OLD_COLUMN, "Code"]
        self.assert_rejected(after, self.good_source())

    def test_a_rename_in_the_migration_without_the_model_change_is_refused(self):
        self.assert_rejected(copy.deepcopy(self.before), self.good_source())

    def test_a_missing_down_is_refused(self):
        source = migration_source([rename_column_call(OLD_COLUMN, NEW_COLUMN)], [])
        self.assert_rejected(self.good_after(), source, text="reverse")

    def test_an_asymmetric_down_is_refused(self):
        source = migration_source(
            [rename_column_call(OLD_COLUMN, NEW_COLUMN)],
            [rename_column_call(NEW_COLUMN, "Other")],
        )
        self.assert_rejected(self.good_after(), source, text="reverse")

    def test_a_rename_combined_with_add_column_is_refused(self):
        source = migration_source(
            [
                rename_column_call(OLD_COLUMN, NEW_COLUMN),
                f'migrationBuilder.AddColumn<string>(name: "Extra", table: "{TABLE}", type: "nvarchar(10)", nullable: true);',
            ],
            [rename_column_call(NEW_COLUMN, OLD_COLUMN)],
        )
        self.assert_rejected(self.good_after(), source)

    def test_a_rename_combined_with_an_unrelated_model_change_is_refused(self):
        after = self.good_after()
        for column in entity_of(after, TABLE)["columns"]:
            if column["name"] == "Name":
                column["nullable"] = not column["nullable"]
        self.assert_rejected(after, self.good_source())

    def test_a_rename_to_an_existing_column_is_refused(self):
        existing = {MIGRATION_ID: {"columns": [(TABLE, OLD_COLUMN, "Code")], "indexes": []}}
        source = migration_source(
            [rename_column_call(OLD_COLUMN, "Code")], [rename_column_call("Code", OLD_COLUMN)]
        )
        self.assert_rejected(self.good_after(), source, allowed=existing)

    def test_an_unknown_operation_stays_fail_closed(self):
        for operation in ("RenameTable", "RenameSequence"):
            with self.subTest(operation=operation):
                source = migration_source(
                    [f'migrationBuilder.{operation}(name: "A", newName: "B");'],
                    [f'migrationBuilder.{operation}(name: "B", newName: "A");'],
                )
                self.assert_rejected(self.good_after(), source)

    def test_a_rename_with_a_positional_argument_is_refused(self):
        source = migration_source(
            [f'migrationBuilder.RenameColumn("{OLD_COLUMN}", "{TABLE}", "{NEW_COLUMN}");'],
            [rename_column_call(NEW_COLUMN, OLD_COLUMN)],
        )
        self.assert_rejected(self.good_after(), source)


class AllowlistConsistencyTests(unittest.TestCase):
    def test_the_repository_allowlist_has_no_entry(self):
        # No migration of the repository renames anything; PR-2b adds the capability only.
        self.assertEqual({}, vm.ALLOWED_RENAMES)

    def test_a_stale_allowlist_entry_is_refused_by_the_full_verification(self):
        original = vm.ALLOWED_RENAMES
        vm.ALLOWED_RENAMES = {MIGRATION_ID: {"columns": [(TABLE, OLD_COLUMN, NEW_COLUMN)], "indexes": []}}
        try:
            problems = vm.verify(REPO)
        finally:
            vm.ALLOWED_RENAMES = original
        self.assertTrue(any(MIGRATION_ID in problem and "allowlist" in problem.lower() for problem in problems), problems)

    def test_the_real_repository_still_verifies(self):
        self.assertEqual([], vm.verify(REPO))


if __name__ == "__main__":
    unittest.main()
