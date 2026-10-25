#!/usr/bin/env python3
"""Mutation tests of the migration guards (generate_migrations.py --check and
verify_migration_model.py).

Each test copies the real sources and migrations into a temporary directory, applies ONE
unauthorised change to a migration, a designer file or the snapshot, and asserts that the
named guard rejects it with the expected reason. The baseline test proves that the
unmodified copy passes both guards, so a rejection can not be caused by the harness.

Run: python3 scripts/tests/test_migration_guard_mutations.py   (needs tree-sitter, like CI)
Nothing in the repository is modified: every mutation happens in the temporary copy.
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

import migration_model as mm  # noqa: E402

MIGRATIONS = os.path.join("Infrastructure", "Persistence", "Migrations")
ALIGN = "20261017090000_AlignSchemaWithModel"
WIDEN = "20261019071146_WidenUserPasswordHash"
GROUPS = "20261022150943_AddCorporateGroups"
DEFAULT_COMPANY = "20261023190000_AddUserDefaultCompany"
BOUNDARY = "20261023191000_AddCompanyCustomerBoundary"
INITIAL = "20251017120000_InitialCreate"
SNAPSHOT = "DatabaseContextModelSnapshot"


def method_span(text, method):
    start = text.index(f"protected override void {method}(")
    other = "Down" if method == "Up" else None
    end = text.index("protected override void Down(") if method == "Up" else len(text)
    return start, end


def operation_span(text, kind, method="Up", nth=0):
    """Returns (start, end) of the nth migrationBuilder.<kind>(...); statement of a method."""
    low, high = method_span(text, method)
    position = low
    for _ in range(nth + 1):
        position = text.index(f"migrationBuilder.{kind}", position, high)
        found = position
        position += 1
    _, after = mm.balanced_call(text, text.index("(", found))
    while text[after] in " \t":
        after += 1
    assert text[after] == ";"
    return found, after + 1


def remove_operation(text, kind, method="Up", nth=0):
    start, end = operation_span(text, kind, method, nth)
    return text[:start] + text[end:]


def insert_statement(text, method, statement):
    low, _ = method_span(text, method)
    brace = text.index("{", low)
    return text[: brace + 1] + f"\n            {statement}\n" + text[brace + 1 :]


def replace_once(text, old, new):
    assert text.count(old) >= 1, f"fixture text not found: {old!r}"
    return text.replace(old, new, 1)


class MutationBase(unittest.TestCase):
    root = None

    @classmethod
    def setUpClass(cls):
        cls.root = tempfile.mkdtemp(prefix="faranou-guards-")
        for part in ("Domain", "Infrastructure", "Constants"):
            source = os.path.join(REPO, part)
            if os.path.isdir(source):
                shutil.copytree(
                    source,
                    os.path.join(cls.root, part),
                    ignore=shutil.ignore_patterns("bin", "obj"),
                )

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.root, ignore_errors=True)

    def path(self, name):
        return os.path.join(self.root, MIGRATIONS, name)

    def run_guard(self, guard):
        script = {
            "generate": ["generate_migrations.py", "--check"],
            "verify": ["verify_migration_model.py"],
        }[guard]
        result = subprocess.run(
            [sys.executable, os.path.join(SCRIPTS, script[0]), *script[1:], "--repo-root", self.root],
            capture_output=True,
            text=True,
        )
        return result.returncode, result.stderr + result.stdout

    def mutate(self, file_name, transform, guard, reason):
        """Applies one change, asserts the guard rejects it for `reason`, then restores."""
        path = self.path(file_name)
        with open(path, encoding="utf-8") as file:
            original = file.read()
        changed = transform(original)
        self.assertNotEqual(original, changed, "the mutation changed nothing")
        try:
            with open(path, "w", encoding="utf-8", newline="\n") as file:
                file.write(changed)
            code, output = self.run_guard(guard)
        finally:
            with open(path, "w", encoding="utf-8", newline="\n") as file:
                file.write(original)
        self.assertNotEqual(0, code, f"{guard} ACCEPTED a forbidden change in {file_name}")
        self.assertIn(reason, output, f"{guard} rejected the change for another reason:\n{output}")


class BaselineTests(MutationBase):
    def test_unmodified_copy_passes_both_guards(self):
        for guard in ("generate", "verify"):
            code, output = self.run_guard(guard)
            self.assertEqual(0, code, f"{guard} failed on the unmodified copy:\n{output}")


class ReviewedSqlMutationTests(MutationBase):
    REASON = "differ from the reviewed list"

    def test_changed_check_constraint_in_add_corporate_groups(self):
        self.mutate(f"{GROUPS}.cs", lambda t: replace_once(t, "BETWEEN 1 AND 5", "BETWEEN 1 AND 9"),
                    "generate", self.REASON)

    def test_removed_statement_in_add_corporate_groups(self):
        self.mutate(f"{GROUPS}.cs", lambda t: remove_operation(t, "Sql", "Up", 3), "generate", self.REASON)

    def test_extra_statement_in_add_corporate_groups_down(self):
        self.mutate(f"{GROUPS}.cs",
                    lambda t: insert_statement(t, "Down", 'migrationBuilder.Sql("DELETE FROM [Users];");'),
                    "generate", self.REASON)

    def test_changed_permission_catalog_cleanup_in_down(self):
        self.mutate(f"{GROUPS}.cs",
                    lambda t: replace_once(t, "DELETE FROM [PermissionDefinitions]", "DELETE FROM [PermissionDefinition]"),
                    "generate", self.REASON)

    def test_changed_data_repair_in_align_schema(self):
        self.mutate(f"{ALIGN}.cs", lambda t: replace_once(t, "N''", "N'x'"), "generate", self.REASON)

    def test_sql_added_to_a_migration_without_reviewed_sql(self):
        self.mutate(f"{BOUNDARY}.cs",
                    lambda t: insert_statement(t, "Up", 'migrationBuilder.Sql("DELETE FROM [Users];");'),
                    "generate", self.REASON)

    def test_sql_in_a_form_that_cannot_be_compared(self):
        self.mutate(f"{DEFAULT_COMPANY}.cs",
                    lambda t: insert_statement(t, "Up", 'migrationBuilder.Sql(@"DELETE FROM [Users];");'),
                    "generate", "can not be compared")

    def test_newer_migration_than_the_constants(self):
        extra = self.path("20261024000000_Unregistered.cs")
        with open(self.path(f"{BOUNDARY}.cs"), encoding="utf-8") as file:
            text = file.read().replace("AddCompanyCustomerBoundary", "Unregistered")
        try:
            with open(extra, "w", encoding="utf-8") as file:
                file.write(text)
            code, output = self.run_guard("generate")
        finally:
            os.remove(extra)
        self.assertNotEqual(0, code)
        self.assertIn("advance the constants", output)


class ChainMutationTests(MutationBase):
    UP = "The operations of Up differ"
    DOWN = "The operations of Down are not the reverse of Up"

    def test_dropped_create_index_in_a_middle_migration(self):
        self.mutate(f"{DEFAULT_COMPANY}.cs", lambda t: remove_operation(t, "CreateIndex"), "verify", self.UP)

    def test_changed_foreign_key_delete_behavior(self):
        self.mutate(f"{DEFAULT_COMPANY}.cs",
                    lambda t: replace_once(t, "ReferentialAction.SetNull", "ReferentialAction.Cascade"),
                    "verify", self.UP)

    def test_removed_foreign_key(self):
        self.mutate(f"{DEFAULT_COMPANY}.cs", lambda t: remove_operation(t, "AddForeignKey"), "verify", self.UP)

    def test_down_without_drop_foreign_key(self):
        self.mutate(f"{DEFAULT_COMPANY}.cs", lambda t: remove_operation(t, "DropForeignKey", "Down"), "verify", self.DOWN)

    def test_bogus_foreign_key_added(self):
        bogus = ('migrationBuilder.AddForeignKey(name: "FK_x", table: "Users", column: "Id", '
                 'principalTable: "Companies", principalColumn: "Id");')
        self.mutate(f"{BOUNDARY}.cs", lambda t: insert_statement(t, "Up", bogus), "verify", self.UP)

    def test_changed_column_type_of_add_column(self):
        self.mutate(f"{BOUNDARY}.cs", lambda t: replace_once(t, 'type: "uniqueidentifier"', 'type: "nvarchar(max)"'),
                    "verify", self.UP)

    def test_flipped_nullable_of_add_column(self):
        self.mutate(f"{BOUNDARY}.cs", lambda t: replace_once(t, "nullable: true", "nullable: false"), "verify", self.UP)

    def test_default_value_on_a_nullable_add_column(self):
        self.mutate(f"{BOUNDARY}.cs",
                    lambda t: replace_once(t, "nullable: true);",
                                           'nullable: true,\n                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));'),
                    "verify", self.UP)

    def test_max_length_on_a_guid_add_column(self):
        self.mutate(f"{BOUNDARY}.cs", lambda t: replace_once(t, "nullable: true);", "nullable: true,\n                maxLength: 10);"),
                    "verify", self.UP)

    def test_unsupported_add_column_argument_is_refused(self):
        self.mutate(f"{BOUNDARY}.cs", lambda t: replace_once(t, "nullable: true);", 'nullable: true,\n                comment: "x");'),
                    "verify", "that this script does not verify (fail-closed)")

    def test_empty_down(self):
        def empty_down(text):
            start, _ = method_span(text, "Down")
            return text[:start] + "protected override void Down(MigrationBuilder migrationBuilder)\n        {\n        }\n    }\n}\n"
        self.mutate(f"{BOUNDARY}.cs", empty_down, "verify", self.DOWN)

    def test_down_drops_the_wrong_column(self):
        self.mutate(f"{BOUNDARY}.cs", lambda t: replace_once(t, 'name: "CustomerBoundaryId",\n                table: "Companies");', 'name: "CustomerBoundaryId",\n                table: "Users");'),
                    "verify", self.DOWN)

    def test_down_without_drop_index(self):
        self.mutate(f"{BOUNDARY}.cs", lambda t: remove_operation(t, "DropIndex", "Down"), "verify", self.DOWN)

    def test_down_with_an_extra_operation(self):
        self.mutate(f"{BOUNDARY}.cs",
                    lambda t: insert_statement(t, "Down", 'migrationBuilder.DropColumn(name: "Extra", table: "Companies");'),
                    "verify", self.DOWN)

    def test_modified_widen_password_migration(self):
        self.mutate(f"{WIDEN}.cs", lambda t: replace_once(t, "nullable: true", "nullable: false"), "verify", self.UP)

    def test_removed_drop_index_in_align_schema(self):
        self.mutate(f"{ALIGN}.cs", lambda t: remove_operation(t, "DropIndex"), "verify", self.UP)

    def test_removed_create_index_for_a_new_table(self):
        self.mutate(f"{GROUPS}.cs", lambda t: remove_operation(t, "CreateIndex"), "verify", "is missing in the migration")

    def test_add_corporate_groups_down_keeps_a_table(self):
        self.mutate(f"{GROUPS}.cs", lambda t: remove_operation(t, "DropTable", "Down"), "verify",
                    "Down does not drop exactly the new tables")

    def test_changed_type_in_the_initial_migration(self):
        self.mutate(f"{INITIAL}.cs", lambda t: replace_once(t, 'type: "uniqueidentifier"', 'type: "bigint"'), "verify",
                    "The type of 'Roles.Id' is 'bigint' in the migration")

    def test_tampered_historical_designer_breaks_the_chain(self):
        # A semantic (not a syntax) change of an old designer file: the chain of designers no
        # longer explains the operations of the migrations built on it.
        self.mutate(f"{ALIGN}.Designer.cs",
                    lambda t: replace_once(t, '.HasColumnType("nvarchar(50)")', '.HasColumnType("nvarchar(60)")'),
                    "verify", "differ")

    def test_tampered_snapshot_is_detected(self):
        self.mutate(f"{SNAPSHOT}.cs", lambda t: replace_once(t, 'b.HasIndex("RoleId");', "// removed"), "verify",
                    "missing in the model snapshot")

    def test_unsupported_operations_in_up_are_refused(self):
        for statement in (
            'migrationBuilder.DropColumn(name: "A", table: "Companies");',
            'migrationBuilder.RenameColumn(name: "A", table: "Companies", newName: "B");',
            'migrationBuilder.DropTable(name: "Companies");',
            'migrationBuilder.InsertData(table: "Companies", column: "Id", value: 1);',
            'migrationBuilder.DropForeignKey(name: "FK_x", table: "Users");',
        ):
            with self.subTest(statement=statement):
                self.mutate(f"{BOUNDARY}.cs", lambda t, s=statement: insert_statement(t, "Up", s),
                            "verify", "does not verify")


if __name__ == "__main__":
    unittest.main(verbosity=2)
