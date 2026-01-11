namespace WeddingApi.Infrastructure.ExportTable;

public sealed class ExportTableOptions
{
    public const string SectionName = "ExportTable";

    public string TableName { get; init; } = "RsvpExportRows";
}
