using System.Globalization;
using Faranou.Vendor.Persistence.Auditing;

if (args.Length != 4 ||
    !string.Equals(args[0], "append", StringComparison.Ordinal) ||
    !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
{
    Console.Error.WriteLine("Invalid checkpoint helper arguments.");
    return 64;
}

try
{
    var store = new DevelopmentFileVendorAuditCheckpointStore(args[1]);
    var checkpoint = VendorAuditCheckpoint.Create(sequence, args[3], DateTimeOffset.UtcNow);
    await store.AppendAsync(checkpoint);
    return 0;
}
catch (Exception exception)
{
    // The helper emits only the exception type; no path, payload or environment data is logged.
    Console.Error.WriteLine(exception.GetType().Name);
    return 3;
}
