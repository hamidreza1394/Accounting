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


class AddColumnOperationTests(unittest.TestCase):
    def _entity(self, columns):
        return {
            "clr_name": "Domain.Company",
            "table": "Companies",
            "primary_key": ["Id"],
            "alternate_keys": [],
            "columns": columns,
            "indexes": [],
        }

    def _column(self, name):
        return {"name": name, "nullable": True, "column_type": "uniqueidentifier"}

    def test_added_column_is_an_operation(self):
        source = self._entity([self._column("Id")])
        target = self._entity([self._column("Id"), self._column("CustomerBoundaryId")])
        operations = migration_model._column_operations(source, target)
        self.assertEqual(["add_column"], [o["kind"] for o in operations])
        self.assertEqual("CustomerBoundaryId", operations[0]["name"])

    def test_dropped_column_still_requires_a_hand_written_migration(self):
        source = self._entity([self._column("Id"), self._column("Old")])
        target = self._entity([self._column("Id")])
        with self.assertRaises(migration_model.MigrationModelError):
            migration_model._column_operations(source, target)


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
