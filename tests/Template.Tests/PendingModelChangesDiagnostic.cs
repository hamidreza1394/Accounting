using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text;
using Xunit;

namespace Template.Tests;

/// <summary>
/// TEMPORARY DIAGNOSTIC. Reports the exact differences that the EF Core
/// "MigrationsModelDiffer" sees between the model snapshot and the current model. The
/// build logs of a run are not always downloadable, so the runner publishes
/// "/tmp/pending-model-changes.txt" as annotations instead. This file is deleted again
/// once the snapshot describes the model.
/// </summary>
public sealed class PendingModelChangesDiagnostic
{
	private static DatabaseContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<DatabaseContext>()
			.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TarazAva_ModelTests;Trusted_Connection=True;\")
			.Options;

		return new DatabaseContext(options);
	}

	[Fact]
	public void ReportThePendingModelChanges()
	{
		using var context = CreateContext();

		var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;

		Assert.NotNull(snapshot);

		var source = snapshot!.Model.GetRelationalModel();
		var target = context.Model.GetRelationalModel();

		var operations = context.GetService<IMigrationsModelDiffer>()
			.GetDifferences(source, target);

		var lines = new List<string> { $"PENDING OPERATIONS: {operations.Count}" };

		lines.AddRange(operations.Select(Describe));
		lines.Add("--- TABLES ---");
		lines.AddRange(Compare(Tables(source), Tables(target), "TABLE"));
		lines.Add("--- COLUMNS ---");
		lines.AddRange(Compare(Columns(source), Columns(target), "COLUMN"));
		lines.Add("--- INDEXES ---");
		lines.AddRange(Compare(Indexes(source), Indexes(target), "INDEX"));
		lines.Add("--- CONSTRAINTS ---");
		lines.AddRange(Compare(Constraints(source), Constraints(target), "CONSTRAINT"));

		var text = string.Join(Environment.NewLine, lines);

		File.WriteAllText("/tmp/pending-model-changes.txt", text);

		Assert.True(operations.Count == 0, text);
	}

	private static string Describe(MigrationOperation operation)
	{
		var builder = new StringBuilder();

		builder.Append("OP ").Append(operation.GetType().Name);

		foreach (var name in new[]
		{
			"Table", "Name", "Schema", "Columns", "Comment", "NewName", "NewSchema",
			"IsUnique", "IsClustered", "IsSparse", "Filter",
			"ColumnType", "ClrType", "IsNullable", "MaxLength", "IsUnicode", "IsFixedLength",
			"Collation", "Precision", "Scale", "DefaultValueSql", "ComputedColumnSql", "IsStored",
			"OldTable", "OldName", "OldSchema", "OldColumns", "ReferencedTable", "ReferencedColumns",
		})
		{
			var property = operation.GetType().GetProperty(name);

			if (property is null)
			{
				continue;
			}

			var value = property.GetValue(operation);

			if (value is null)
			{
				continue;
			}

			if (value is System.Collections.IEnumerable enumerable and not string)
			{
				value = $"[{string.Join(",", enumerable.Cast<object?>().Select(Format))}]";
			}

			builder.Append($" {name}={value}");
		}

		if (operation is AlterColumnOperation alter)
		{
			builder.Append(" OLD(")
				.Append($"ColumnType={Format(alter.OldColumn.ColumnType)}")
				.Append($" ClrType={Format(alter.OldColumn.ClrType)}")
				.Append($" IsNullable={alter.OldColumn.IsNullable}")
				.Append($" MaxLength={Format(alter.OldColumn.MaxLength)}")
				.Append($" IsUnicode={Format(alter.OldColumn.IsUnicode)}")
				.Append($" IsFixedLength={Format(alter.OldColumn.IsFixedLength)})");
		}

		if (operation is CreateTableOperation create)
		{
			builder.Append(" COLUMNS(")
				.AppendJoin(
					" | ",
					create.Columns.Select(column =>
						$"{column.Name}:{column.ColumnType}{(column.IsNullable ? "?" : string.Empty)}"))
				.Append(')');
		}

		return builder.ToString();
	}

	private static IEnumerable<string> Compare(
		SortedDictionary<string, string> left,
		SortedDictionary<string, string> right,
		string kind)
	{
		foreach (var key in left.Keys.Except(right.Keys))
		{
			yield return $"ONLY-IN-SNAPSHOT {kind} {key} -> {left[key]}";
		}

		foreach (var key in right.Keys.Except(left.Keys))
		{
			yield return $"ONLY-IN-MODEL {kind} {key} -> {right[key]}";
		}

		foreach (var key in left.Keys.Intersect(right.Keys))
		{
			if (left[key] != right[key])
			{
				yield return $"{kind} {key} snapshot({left[key]}) model({right[key]})";
			}
		}
	}

	private static SortedDictionary<string, string> New()
		=> new(StringComparer.Ordinal);

	private static string Annotations(IAnnotatable annotatable)
		=> $"ann=[{string.Join(",", annotatable.GetAnnotations().Select(a => $"{a.Name}={a.Value}"))}]";

	private static SortedDictionary<string, string> Tables(IRelationalModel? model)
	{
		var result = New();

		if (model is null)
		{
			return result;
		}

		foreach (var table in model.Tables)
		{
			var key = $"{table.Schema}.{table.Name}";

			result[key] = $"excluded={table.IsExcludedFromMigrations}"
				+ $",pk=[{string.Join(",", table.PrimaryKey?.Columns.Select(column => column.Name) ?? Array.Empty<string>())}]"
				+ $",triggers={table.Triggers.Count()}"
				+ $",{Annotations(table)}";
		}

		return result;
	}

	private static SortedDictionary<string, string> Columns(IRelationalModel? model)
	{
		var result = New();

		if (model is null)
		{
			return result;
		}

		foreach (var table in model.Tables)
		{
			foreach (var column in table.Columns)
			{
				var property = column.PropertyMappings.First().Property;

				result[$"{table.Schema}.{table.Name}.{column.Name}"] =
					$"storeType={column.StoreType},providerClr={property.ClrType.Name}"
					+ $",nullable={column.IsNullable},max={Format(column.MaxLength)}"
					+ $",precision={Format(column.Precision)},scale={Format(column.Scale)}"
					+ $",unicode={Format(column.IsUnicode)},fixed={Format(column.IsFixedLength)}"
					+ $",rowVersion={column.IsRowVersion},collation={Format(column.Collation)}"
					+ $",comment={Format(column.Comment)},default={Format(column.DefaultValueSql)}"
					+ $",computed={Format(column.ComputedColumnSql)},stored={Format(column.IsStored)}"
					+ $",defaultValue={Format(column.DefaultValue)}"
					+ $",vg={property.ValueGenerated},vgSqlServer={property.GetValueGenerationStrategy()}"
					+ $",{Annotations(column)}";
			}
		}

		return result;
	}

	private static SortedDictionary<string, string> Indexes(IRelationalModel? model)
	{
		var result = New();

		if (model is null)
		{
			return result;
		}

		foreach (var table in model.Tables)
		{
			foreach (var index in table.Indexes)
			{
				result[$"{table.Schema}.{table.Name}.{index.Name}"] =
					$"columns=[{string.Join(",", index.Columns.Select(column => column.Name))}]"
					+ $",unique={index.IsUnique}"
					+ $",descending={Format(index.IsDescending)}"
					+ $",filter={Format(index.Filter)}"
					+ $",clustered={Format(index.MappedIndexes.FirstOrDefault()?.IsClustered())}"
					+ $",{Annotations(index)}";
			}
		}

		return result;
	}

	private static SortedDictionary<string, string> Constraints(IRelationalModel? model)
	{
		var result = New();

		if (model is null)
		{
			return result;
		}

		foreach (var table in model.Tables)
		{
			foreach (var unique in table.UniqueConstraints)
			{
				result[$"UNIQUE {table.Schema}.{table.Name}.{unique.Name}"] =
					$"columns=[{string.Join(",", unique.Columns.Select(column => column.Name))}]";
			}

			foreach (var check in table.CheckConstraints)
			{
				result[$"CHECK {table.Schema}.{table.Name}.{check.Name}"] = check.Sql;
			}

			foreach (var foreign in table.ForeignKeyConstraints)
			{
				result[$"FK {table.Schema}.{table.Name}.{foreign.Name}"] =
					$"columns=[{string.Join(",", foreign.Columns.Select(column => column.Name))}]"
					+ $",principal={foreign.PrincipalTable.Schema}.{foreign.PrincipalTable.Name}"
					+ $",principalColumns=[{string.Join(",", foreign.PrincipalUniqueConstraint.Columns.Select(column => column.Name))}]"
					+ $",onDelete={foreign.OnDeleteAction}";
			}
		}

		return result;
	}

	private static string Format(object? value)
		=> value switch
		{
			null => "null",
			Type type => type.Name,
			System.Collections.IEnumerable enumerable and not string =>
				$"[{string.Join(",", enumerable.Cast<object?>().Select(Format))}]",
			_ => value.ToString() ?? "null",
		};
}
