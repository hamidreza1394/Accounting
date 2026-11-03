namespace Data.Conventions;

internal static class DateTimeConventions : object
{
	static DateTimeConventions()
	{
	}

	/// <summary>
	/// Converts <see cref="System.DDateOnly" /> to <see cref="System.DDateTime"/> and vice versa.
	/// </summary>
	internal class DateOnlyConverter :
		Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.DateOnly, System.DateTime>
	{
		/// <summary>
		/// Creates a new instance of this converter.
		/// </summary>
		public DateOnlyConverter() : base
			(current => current.ToDateTime(System.TimeOnly.MinValue),
			current => System.DateOnly.FromDateTime(current))
		{
		}
	}

	/// <summary>
	/// Converts <see cref="System.DateOnly?" /> to <see cref="System.DDateTime?"/> and vice versa.
	/// </summary>
	internal class NullableDateOnlyConverter :
		Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.DateOnly?, System.DateTime?>
	{
		/// <summary>
		/// Creates a new instance of this converter.
		/// </summary>
		public NullableDateOnlyConverter() : base
			(current => current == null ? null : new System.DateTime?(current.Value.ToDateTime(System.TimeOnly.MinValue)),
			current => current == null ? null : new System.DateOnly?(System.DateOnly.FromDateTime(current.Value)))
		{
		}
	}

	/// <summary>
	/// Preserves the stored instant while marking UTC-suffixed DateTime values as UTC.
	/// SQL Server datetime2 has no offset or DateTimeKind metadata.
	/// </summary>
	internal sealed class UtcDateTimeConverter :
		Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.DateTime, System.DateTime>
	{
		public UtcDateTimeConverter() : base(
			current => System.DateTime.SpecifyKind(current, System.DateTimeKind.Utc),
			current => System.DateTime.SpecifyKind(current, System.DateTimeKind.Utc))
		{
		}
	}

	/// <summary>
	/// Nullable counterpart of <see cref="UtcDateTimeConverter"/>.
	/// </summary>
	internal sealed class NullableUtcDateTimeConverter :
		Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.DateTime?, System.DateTime?>
	{
		public NullableUtcDateTimeConverter() : base(
			current => current == null ? null : System.DateTime.SpecifyKind(current.Value, System.DateTimeKind.Utc),
			current => current == null ? null : System.DateTime.SpecifyKind(current.Value, System.DateTimeKind.Utc))
		{
		}
	}
}
