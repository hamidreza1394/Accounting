using System.Text;
using System.Text.Json;

namespace Faranou.Vendor.Persistence.Auditing;

/// <summary>
/// A local append-only checkpoint journal for Development and automated tests only.
/// This type deliberately does not implement <see cref="IProductionVendorAuditCheckpointStore"/>.
/// A local file is not an independent failure domain and must never be presented as a Production checkpoint sink.
/// </summary>
public sealed class DevelopmentFileVendorAuditCheckpointStore : IVendorAuditCheckpointStore
{
    private static readonly SemaphoreSlim ProcessGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly string _lockPath;

    public DevelopmentFileVendorAuditCheckpointStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A fully qualified checkpoint journal path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
        _lockPath = _path + ".lock";
    }

    public async Task<VendorAuditCheckpoint?> ReadLatestAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = new LockLease(await AcquireLockAsync(cancellationToken));
        if (!File.Exists(_path))
        {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(_path, cancellationToken);
        VendorAuditCheckpoint? latest = null;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidDataException("The development audit checkpoint journal contains an empty record.");
            }

            VendorAuditCheckpoint? checkpoint;
            try
            {
                checkpoint = JsonSerializer.Deserialize<VendorAuditCheckpoint>(line, JsonOptions);
            }
            catch (JsonException)
            {
                throw new InvalidDataException("The development audit checkpoint journal contains an invalid record.");
            }

            if (checkpoint is null || !checkpoint.IsValid())
            {
                throw new InvalidDataException("The development audit checkpoint journal contains an invalid checkpoint.");
            }

            if (latest is not null && checkpoint.EventCount <= latest.EventCount)
            {
                throw new InvalidDataException("The development audit checkpoint journal is not strictly monotonic.");
            }

            latest = checkpoint;
        }

        return latest;
    }

    public async Task AppendAsync(
        VendorAuditCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (!checkpoint.IsValid())
        {
            throw new InvalidOperationException("Only valid audit checkpoints may be appended.");
        }

        await using var gate = new LockLease(await AcquireLockAsync(cancellationToken));
        var latest = await ReadLatestWhileLockedAsync(cancellationToken);
        if (latest is not null && checkpoint.EventCount <= latest.EventCount)
        {
            throw new InvalidOperationException(
                "Development audit checkpoints must have strictly increasing event counts.");
        }

        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The development audit checkpoint journal directory is invalid.");
        }

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(checkpoint, JsonOptions) + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(json);
        await using var stream = new FileStream(
            _path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private async Task<VendorAuditCheckpoint?> ReadLatestWhileLockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(_path, cancellationToken);
        VendorAuditCheckpoint? latest = null;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidDataException("The development audit checkpoint journal contains an empty record.");
            }

            VendorAuditCheckpoint? checkpoint;
            try
            {
                checkpoint = JsonSerializer.Deserialize<VendorAuditCheckpoint>(line, JsonOptions);
            }
            catch (JsonException)
            {
                throw new InvalidDataException("The development audit checkpoint journal contains an invalid record.");
            }

            if (checkpoint is null || !checkpoint.IsValid() ||
                (latest is not null && checkpoint.EventCount <= latest.EventCount))
            {
                throw new InvalidDataException("The development audit checkpoint journal is invalid or non-monotonic.");
            }

            latest = checkpoint;
        }

        return latest;
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        await ProcessGate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_lockPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("The development audit checkpoint lock directory is invalid.");
            }

            Directory.CreateDirectory(directory);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
                }
            }
        }
        catch
        {
            ProcessGate.Release();
            throw;
        }
    }

    private sealed class LockLease(FileStream stream) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
            ProcessGate.Release();
        }
    }
}
