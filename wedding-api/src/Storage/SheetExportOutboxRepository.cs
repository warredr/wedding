using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace WeddingApi.Storage;

public enum SheetExportOperation
{
    Upsert = 0,
    Delete = 1,
}

public enum SheetExportStatus
{
    Pending = 0,
    Succeeded = 1,
}

public sealed class SheetExportOutboxEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // groupId
    public string RowKey { get; set; } = "latest";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Status { get; set; } = SheetExportStatus.Pending.ToString();
    public string Operation { get; set; } = SheetExportOperation.Upsert.ToString();
    public int AttemptCount { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? LastError { get; set; }
}

public sealed record SheetExportWorkItem(
    string GroupId,
    SheetExportOperation Operation,
    int AttemptCount,
    ETag ETag);

public interface ISheetExportOutbox
{
    Task EnqueueUpsertAsync(string groupId, CancellationToken cancellationToken);
    Task EnqueueDeleteAsync(string groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SheetExportWorkItem>> GetPendingAsync(int maxItems, CancellationToken cancellationToken);
    Task MarkSucceededAsync(string groupId, ETag etag, CancellationToken cancellationToken);
    Task MarkFailedAsync(string groupId, ETag etag, string error, CancellationToken cancellationToken);
}

public sealed class SheetExportOutboxRepository : ISheetExportOutbox
{
    private readonly TableServiceClient _serviceClient;
    private readonly StorageOptions _options;

    public SheetExportOutboxRepository(TableServiceClient serviceClient, IOptions<StorageOptions> options)
    {
        _serviceClient = serviceClient;
        _options = options.Value;
    }

    private TableClient Table => _serviceClient.GetTableClient(_options.SheetExportsTableName);

    public async Task EnqueueUpsertAsync(string groupId, CancellationToken cancellationToken)
    {
        await Table.CreateIfNotExistsAsync(cancellationToken);
        var entity = new SheetExportOutboxEntity
        {
            PartitionKey = groupId,
            RowKey = "latest",
            Status = SheetExportStatus.Pending.ToString(),
            Operation = SheetExportOperation.Upsert.ToString(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        await Table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task EnqueueDeleteAsync(string groupId, CancellationToken cancellationToken)
    {
        await Table.CreateIfNotExistsAsync(cancellationToken);
        var entity = new SheetExportOutboxEntity
        {
            PartitionKey = groupId,
            RowKey = "latest",
            Status = SheetExportStatus.Pending.ToString(),
            Operation = SheetExportOperation.Delete.ToString(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        await Table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<SheetExportWorkItem>> GetPendingAsync(int maxItems, CancellationToken cancellationToken)
    {
        await Table.CreateIfNotExistsAsync(cancellationToken);

        var items = new List<SheetExportWorkItem>(Math.Max(1, maxItems));

        // Wedding-scale: full scan is acceptable.
        await foreach (var entity in Table.QueryAsync<SheetExportOutboxEntity>(cancellationToken: cancellationToken))
        {
            if (!string.Equals(entity.RowKey, "latest", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(entity.Status, SheetExportStatus.Pending.ToString(), StringComparison.Ordinal))
            {
                continue;
            }

            var op = Enum.TryParse<SheetExportOperation>(entity.Operation, out var parsed)
                ? parsed
                : SheetExportOperation.Upsert;

            items.Add(new SheetExportWorkItem(entity.PartitionKey, op, entity.AttemptCount, entity.ETag));

            if (items.Count >= maxItems)
            {
                break;
            }
        }

        return items;
    }

    public async Task MarkSucceededAsync(string groupId, ETag etag, CancellationToken cancellationToken)
    {
        await Table.CreateIfNotExistsAsync(cancellationToken);

        var entity = await Table.GetEntityAsync<SheetExportOutboxEntity>(groupId, "latest", cancellationToken: cancellationToken);
        var current = entity.Value;
        current.ETag = etag;
        current.Status = SheetExportStatus.Succeeded.ToString();
        current.LastError = null;
        current.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await Table.UpdateEntityAsync(current, current.ETag, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task MarkFailedAsync(string groupId, ETag etag, string error, CancellationToken cancellationToken)
    {
        await Table.CreateIfNotExistsAsync(cancellationToken);

        var entity = await Table.GetEntityAsync<SheetExportOutboxEntity>(groupId, "latest", cancellationToken: cancellationToken);
        var current = entity.Value;
        current.ETag = etag;
        current.Status = SheetExportStatus.Pending.ToString();
        current.AttemptCount = Math.Min(current.AttemptCount + 1, 1000);
        current.LastError = error.Length > 1000 ? error[..1000] : error;
        current.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await Table.UpdateEntityAsync(current, current.ETag, TableUpdateMode.Replace, cancellationToken);
    }
}
