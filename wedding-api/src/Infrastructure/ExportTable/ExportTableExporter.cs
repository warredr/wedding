using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using WeddingApi.Domain;

namespace WeddingApi.Infrastructure.ExportTable;

public interface IExportTableExporter
{
    Task UpsertRowsAsync(IReadOnlyList<SheetRow> rows, CancellationToken cancellationToken);
    Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken);
}

public sealed class ExportRowEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // groupId
    public string RowKey { get; set; } = default!; // personId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Dinner { get; set; } = string.Empty;
    public string EveningParty { get; set; } = string.Empty;
    public string Allergies { get; set; } = string.Empty;
}

public sealed class ExportTableExporter : IExportTableExporter
{
    private readonly TableServiceClient _serviceClient;
    private readonly ExportTableOptions _options;

    public ExportTableExporter(TableServiceClient serviceClient, IOptions<ExportTableOptions> options)
    {
        _serviceClient = serviceClient;
        _options = options.Value;
    }

    private TableClient Table => _serviceClient.GetTableClient(_options.TableName);

    public async Task UpsertRowsAsync(IReadOnlyList<SheetRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await Table.CreateIfNotExistsAsync(cancellationToken);

        // Table transactions are limited to 100 entities and one partition key.
        // We naturally use PartitionKey=groupId, so per-group batches are safe.
        var byGroup = rows.GroupBy(r => r.GroupId, StringComparer.Ordinal);
        foreach (var group in byGroup)
        {
            var batch = new List<TableTransactionAction>(capacity: 100);

            foreach (var row in group)
            {
                var entity = new ExportRowEntity
                {
                    PartitionKey = row.GroupId,
                    RowKey = row.PersonId,
                    FullName = row.FullName,
                    Dinner = row.Dinner,
                    EveningParty = row.EveningParty,
                    Allergies = row.Allergies,
                };

                batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity));

                if (batch.Count == 100)
                {
                    await Table.SubmitTransactionAsync(batch, cancellationToken);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await Table.SubmitTransactionAsync(batch, cancellationToken);
            }
        }
    }

    public async Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken)
    {
        await Table.CreateIfNotExistsAsync(cancellationToken);

        // Wedding scale: scan one partition and delete.
        var toDelete = new List<TableTransactionAction>(capacity: 100);

        await foreach (var entity in Table.QueryAsync<ExportRowEntity>(
                           filter: $"PartitionKey eq '{groupId.Replace("'", "''")}'",
                           cancellationToken: cancellationToken))
        {
            toDelete.Add(new TableTransactionAction(TableTransactionActionType.Delete, entity));

            if (toDelete.Count == 100)
            {
                await Table.SubmitTransactionAsync(toDelete, cancellationToken);
                toDelete.Clear();
            }
        }

        if (toDelete.Count > 0)
        {
            await Table.SubmitTransactionAsync(toDelete, cancellationToken);
        }
    }
}
