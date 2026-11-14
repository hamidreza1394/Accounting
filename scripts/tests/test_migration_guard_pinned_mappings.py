#!/usr/bin/env python3
"""Tests of the pinned mappings of the migration guards (S3-0h PR-2a).

The guards derive the EF Core model from the C# sources (``ef_model.py``) and compare it
with the snapshot, the designer files and the migrations. These tests prove, against the
REAL parser and the REAL guards (nothing is mocked), that

* ``HasConstraintName``, ``HasDatabaseName``, ``HasName`` and ``HasColumnName`` are
  understood: a pin that repeats the name EF Core derives anyway is NOT a model change, a
  pin that changes a name is one, and the model, the snapshot and the designer carry it;
* every other fluent API call of a configuration statement is rejected
  (``ModelExtractionError``, naming the method and the file) instead of being ignored;
* a renamed constraint or column is still refused: ``RenameColumn``/``RenameIndex`` stay
  outside what the guards accept (that is PR-2b).

Each test copies the real sources into a temporary directory and changes ONE
configuration there; nothing in the repository is modified.

Run: python3 scripts/tests/test_migration_guard_pinned_mappings.py   (needs tree-sitter)
"""
import copy
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
import generate_migrations as gm  # noqa: E402
import migration_model as mm  # noqa: E402

CONFIGURATION = os.path.join(
    "Infrastructure", "Persistence", "Configurations", "CompanyRoleConfiguration.cs"
)
MIGRATIONS = os.path.join("Infrastructure", "Persistence", "Migrations")

# The names that EF Core derives for CompanyRoleConfiguration.cs.
DEFAULT_PRIMARY_KEY = "PK_CompanyRoles"
DEFAULT_ALTERNATE_KEY = "AK_CompanyRoles_Id_TenantId"
DEFAULT_INDEX = "IX_CompanyRoles_TenantId_Code"
DEFAULT_FOREIGN_KEY = "FK_CompanyRoles_Companies_TenantId"

KEY_LINE = "builder.HasKey(x => x.Id);"
INDEX_LINE = "builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();"
ALTERNATE_KEY_LINE = "builder.HasAlternateKey(x => new { x.Id, x.TenantId });"
CODE_LINE = "builder.Property(x => x.Code).HasMaxLength(80).IsUnicode(false).IsRequired();"
RELATIONSHIP_END = ".HasForeignKey(x => x.TenantId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);"


class PinnedMappingBase(unittest.TestCase):
    root = None
    baseline = None

    @classmethod
    def setUpClass(cls):
        cls.root = tempfile.mkdtemp(prefix="faranou-pins-")
        for part in ("Domain", "Infrastructure", "Constants"):
            source = os.path.join(REPO, part)
            if os.path.isdir(source):
                shutil.copytree(
                    source,
                    os.path.join(cls.root, part),
                    ignore=shutil.ignore_patterns("bin", "obj"),
                )
        cls.baseline = ef_model.derive_model(cls.root)
        with open(os.path.join(cls.root, CONFIGURATION), encoding="utf-8") as file:
            cls.original = file.read()

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.root, ignore_errors=True)

    def tearDown(self):
        self.write_configuration(self.original)

    def write_configuration(self, text):
        with open(os.path.join(self.root, CONFIGURATION), "w", encoding="utf-8", newline="\n") as file:
            file.write(text)

    def mutate_configuration(self, old, new):
        self.assertIn(old, self.original, f"fixture text not found: {old!r}")
        self.write_configuration(self.original.replace(old, new, 1))

    def derive(self):
        return ef_model.derive_model(self.root)

    def run_guard(self, *arguments):
        result = subprocess.run(
            [sys.executable, os.path.join(SCRIPTS, arguments[0]), *arguments[1:], "--repo-root", self.root],
            capture_output=True,
            text=True,
        )
        return result.returncode, result.stderr + result.stdout

    def entity(self, model, table="CompanyRoles"):
        return next(entity for entity in model["entities"] if entity["table"] == table)


class PinWithoutModelChangeTests(PinnedMappingBase):
    """A pin that repeats the name EF Core derives anyway is not a model change."""

    def assert_unchanged(self):
        self.assertEqual(self.baseline, self.derive())
        code, output = self.run_guard("generate_migrations.py", "--check")
        self.assertEqual(0, code, output)

    def test_has_constraint_name_with_the_derived_name(self):
        self.mutate_configuration(
            RELATIONSHIP_END,
            f'.HasForeignKey(x => x.TenantId).HasConstraintName("{DEFAULT_FOREIGN_KEY}")'
            ".OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);",
        )
        self.assert_unchanged()

    def test_has_database_name_and_has_name_with_the_derived_names(self):
        self.mutate_configuration(KEY_LINE, f'builder.HasKey(x => x.Id).HasName("{DEFAULT_PRIMARY_KEY}");')
        self.mutate_configuration_again(
            INDEX_LINE,
            f'builder.HasIndex(x => new {{ x.TenantId, x.Code }}).IsUnique().HasDatabaseName("{DEFAULT_INDEX}");',
        )
        self.mutate_configuration_again(
            ALTERNATE_KEY_LINE,
            f'builder.HasAlternateKey(x => new {{ x.Id, x.TenantId }}).HasName("{DEFAULT_ALTERNATE_KEY}");',
        )
        self.assert_unchanged()

    def test_has_column_name_keeping_the_physical_column_name(self):
        self.mutate_configuration(
            CODE_LINE,
            'builder.Property(x => x.Code).HasColumnName("Code").HasMaxLength(80).IsUnicode(false).IsRequired();',
        )
        self.assert_unchanged()

    def mutate_configuration_again(self, old, new):
        with open(os.path.join(self.root, CONFIGURATION), encoding="utf-8") as file:
            current = file.read()
        self.assertIn(old, current, f"fixture text not found: {old!r}")
        self.write_configuration(current.replace(old, new, 1))


class PinnedNamesInTheModelTests(PinnedMappingBase):
    """A pin that changes a name is a model change that all generated files must carry."""

    def pin_everything(self):
        text = self.original
        text = text.replace(KEY_LINE, 'builder.HasKey(x => x.Id).HasName("PK_Pinned");', 1)
        text = text.replace(
            INDEX_LINE,
            'builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("IX_Pinned");',
            1,
        )
        text = text.replace(
            ALTERNATE_KEY_LINE,
            'builder.HasAlternateKey(x => new { x.Id, x.TenantId }).HasName("AK_Pinned");',
            1,
        )
        text = text.replace(
            RELATIONSHIP_END,
            '.HasForeignKey(x => x.TenantId).HasConstraintName("FK_Pinned")'
            ".OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);",
            1,
        )
        self.assertNotEqual(self.original, text)
        self.write_configuration(text)

    def test_the_model_records_the_explicit_names(self):
        self.pin_everything()
        model = self.derive()
        entity = self.entity(model)
        self.assertEqual("PK_Pinned", entity["primary_key_name"])
        self.assertEqual(["AK_Pinned"], entity["alternate_key_names"])
        index = next(item for item in entity["indexes"] if item["columns"] == ["TenantId", "Code"])
        self.assertEqual("IX_Pinned", index["name"])
        foreign_key = next(item for item in entity["foreign_keys"] if item["columns"] == ["TenantId"])
        self.assertEqual("FK_Pinned", foreign_key["name"])
        # The names that were not pinned keep the derived defaults.
        other = self.entity(model, "UserCompanyMemberships")
        self.assertEqual("PK_UserCompanyMemberships", other["primary_key_name"])

    def test_the_snapshot_and_the_designer_carry_the_names_and_read_back(self):
        self.pin_everything()
        model = self.derive()
        entities = gm.sorted_entities(model) if hasattr(gm, "sorted_entities") else model["entities"]
        for text in (gm.generate_snapshot(entities), gm.generate_designer(entities)):
            for fragment in (
                '.HasName("PK_Pinned")',
                '.HasName("AK_Pinned")',
                '.HasDatabaseName("IX_Pinned")',
                '.HasConstraintName("FK_Pinned")',
            ):
                self.assertIn(fragment, text)
            parsed = mm.parse_generated_model(text)
            role = parsed["entities"]["Domain.CompanyRole"]
            self.assertEqual("PK_Pinned", role["primary_key_name"])
            self.assertIn("AK_Pinned", role["alternate_key_names"])
            self.assertIn("IX_Pinned", [index["name"] for index in role["indexes"]])
            self.assertIn("FK_Pinned", [item.get("name") for item in parsed["relationships"]])

    def test_the_guards_notice_an_unintended_name_change(self):
        self.pin_everything()
        # The snapshot and the designer of the repository still carry the default names,
        # so the pin is a model change that no migration describes.
        # ('generate_migrations.py --check' leaves the EF-written snapshot and designer of
        # the newest migration to verify_migration_model.py, so this is the guard.)
        code, output = self.run_guard("verify_migration_model.py")
        self.assertNotEqual(0, code, output)
        for name in ("PK_Pinned", "AK_Pinned", "IX_Pinned", "FK_Pinned"):
            self.assertIn(name, output)

    def test_a_name_difference_between_two_models_is_a_rename_and_is_refused(self):
        before = mm.normalize_derived_model(self.baseline)
        self.pin_everything()
        after = mm.normalize_derived_model(self.derive())
        with self.assertRaises(mm.MigrationModelError) as context:
            mm.compute_operations(before, after)
        message = str(context.exception)
        self.assertIn("rename", message.lower())


class FailClosedTests(PinnedMappingBase):
    """Every fluent API call that the parser does not understand is an error."""

    def assert_rejected(self, old, new, method):
        self.mutate_configuration(old, new)
        with self.assertRaises(ef_model.ModelExtractionError) as context:
            self.derive()
        message = str(context.exception)
        self.assertIn(method, message)
        self.assertIn("CompanyRoleConfiguration.cs", message)

    def test_unknown_property_call(self):
        self.assert_rejected(
            CODE_LINE,
            'builder.Property(x => x.Code).HasDefaultValue("x").HasMaxLength(80).IsUnicode(false).IsRequired();',
            "HasDefaultValue",
        )

    def test_unknown_index_call(self):
        self.assert_rejected(
            INDEX_LINE,
            "builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().IncludeProperties(x => x.Name);",
            "IncludeProperties",
        )

    def test_unknown_key_call(self):
        self.assert_rejected(KEY_LINE, "builder.HasKey(x => x.Id).IsClustered(false);", "IsClustered")

    def test_unknown_alternate_key_call(self):
        self.assert_rejected(
            ALTERNATE_KEY_LINE,
            "builder.HasAlternateKey(x => new { x.Id, x.TenantId }).IsClustered();",
            "IsClustered",
        )

    def test_unknown_relationship_call(self):
        self.assert_rejected(
            RELATIONSHIP_END,
            ".HasForeignKey(x => x.TenantId).HasAnnotation(\"A\", 1)"
            ".OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);",
            "HasAnnotation",
        )

    def test_a_table_callback_or_schema_is_rejected(self):
        self.assert_rejected(
            'builder.ToTable("CompanyRoles");',
            'builder.ToTable("CompanyRoles", t => t.ExcludeFromMigrations());',
            "ToTable",
        )
        self.mutate_configuration('builder.ToTable("CompanyRoles");', 'builder.ToTable("CompanyRoles", "sales");')
        with self.assertRaises(ef_model.ModelExtractionError):
            self.derive()

    def test_index_names_that_differ_only_in_case_are_rejected(self):
        self.mutate_configuration(
            INDEX_LINE,
            INDEX_LINE.replace(".IsUnique();", ".IsUnique().HasDatabaseName(\"IX_Pinned\");")
            + ' builder.HasIndex(x => x.Name).HasDatabaseName("ix_pinned");',
        )
        with self.assertRaises(ef_model.ModelExtractionError) as context:
            self.derive()
        self.assertIn("case", str(context.exception))

    def test_unknown_table_call(self):
        self.assert_rejected(
            'builder.ToTable("CompanyRoles");',
            'builder.ToTable("CompanyRoles").HasComment("x");',
            "HasComment",
        )

    def test_a_pin_without_a_literal_name_is_rejected(self):
        self.assert_rejected(
            KEY_LINE, "builder.HasKey(x => x.Id).HasName(Constants.Name);", "HasName"
        )


class RenameIsStillRefusedTests(PinnedMappingBase):
    """PR-2a accepts no rename: the mapping to another column is an error, too."""

    def test_has_column_name_to_another_column_is_refused(self):
        self.mutate_configuration(
            CODE_LINE,
            'builder.Property(x => x.Code).HasColumnName("RoleCode").HasMaxLength(80).IsUnicode(false).IsRequired();',
        )
        with self.assertRaises(ef_model.ModelExtractionError) as context:
            self.derive()
        message = str(context.exception)
        self.assertIn("HasColumnName", message)
        self.assertIn("RoleCode", message)

    def test_rename_operations_are_parsed_but_never_accepted_without_the_allowlist(self):
        # PR-2b: the parser reads the renames; the guard only accepts them for a migration
        # on the reviewed ALLOWED_RENAMES allowlist (see test_migration_guard_rename.py).
        import verify_migration_model as vm

        for operation, kind in (("RenameColumn", "rename_column"), ("RenameIndex", "rename_index")):
            with self.subTest(operation=operation):
                source = (
                    "protected override void Up(MigrationBuilder migrationBuilder)\n{\n"
                    f'    migrationBuilder.{operation}(name: "A", table: "T", newName: "B");\n'
                    "}\nprotected override void Down(MigrationBuilder migrationBuilder)\n{\n}\n"
                )
                self.assertEqual(vm.parse_operations(source, "up"), [(kind, "T", "A", "B")])
                self.assertEqual(vm.ALLOWED_RENAMES, {})


class GeneratedFileParserFailClosedTests(unittest.TestCase):
    """The snapshot and the designer parser must not ignore a call either."""

    def snapshot_with(self, property_calls):
        return (
            'modelBuilder.Entity("Domain.Thing", b =>\n{\n'
            '    b.Property<Guid>("Id")\n        .HasColumnType("uniqueidentifier")'
            f"{property_calls};\n\n"
            '    b.HasKey("Id");\n\n    b.ToTable("Things");\n});\n'
        )

    def test_a_known_snapshot_parses(self):
        parsed = mm.parse_generated_model(self.snapshot_with(""))
        self.assertEqual("Things", parsed["entities"]["Domain.Thing"]["table"])

    def test_an_unknown_snapshot_property_call_is_an_error(self):
        with self.assertRaises(mm.MigrationModelError) as context:
            mm.parse_generated_model(self.snapshot_with('\n        .HasDefaultValue("x")'))
        self.assertIn("HasDefaultValue", str(context.exception))

    def test_whitespace_in_a_naming_call_does_not_hide_it(self):
        for calls in ('\n        .HasColumnName( "Other" )', '\n        .HasColumnName ("Other")'):
            with self.subTest(calls=calls):
                with self.assertRaises(mm.MigrationModelError) as context:
                    mm.parse_generated_model(self.snapshot_with(calls))
                self.assertIn("Other", str(context.exception))
        parsed = mm.parse_generated_model(
            self.snapshot_with("").replace('b.HasKey("Id");', 'b.HasKey("Id").HasName( "PK_Spaced" );')
        )
        self.assertEqual("PK_Spaced", parsed["entities"]["Domain.Thing"]["primary_key_name"])

    def test_a_column_name_that_differs_from_the_property_is_an_error(self):
        with self.assertRaises(mm.MigrationModelError) as context:
            mm.parse_generated_model(self.snapshot_with('\n        .HasColumnName("Other")'))
        self.assertIn("HasColumnName", str(context.exception))


if __name__ == "__main__":
    unittest.main()
