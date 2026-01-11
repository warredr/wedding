using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WeddingApi.Domain;
using WeddingApi.Infrastructure.ExportTable;
using WeddingApi.Invites;
using WeddingApi.Storage;

namespace WeddingApi.Functions;

public sealed class SheetExportTimer
{
    private readonly ILogger<SheetExportTimer> _logger;
    private readonly ISheetExportOutbox _outbox;
    private readonly IRsvpStorage _storage;
    private readonly IInviteRepository _invites;
    private readonly IExportTableExporter _exporter;

    public SheetExportTimer(
        ILogger<SheetExportTimer> logger,
        ISheetExportOutbox outbox,
        IRsvpStorage storage,
        IInviteRepository invites,
        IExportTableExporter exporter)
    {
        _logger = logger;
        _outbox = outbox;
        _storage = storage;
        _invites = invites;
        _exporter = exporter;
    }

    [Function("sheet_export_timer")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var pending = await _outbox.GetPendingAsync(maxItems: 25, cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        foreach (var item in pending)
        {
            if (item.AttemptCount >= 50)
            {
                _logger.LogWarning("Export skipped for group {GroupId} due to high attempt count.", item.GroupId);
                continue;
            }

            try
            {
                if (item.Operation == SheetExportOperation.Delete)
                {
                    await _exporter.DeleteGroupAsync(item.GroupId, cancellationToken);
                    await _outbox.MarkSucceededAsync(item.GroupId, item.ETag, cancellationToken);
                    continue;
                }

                // Upsert
                var group = await _invites.GetGroupAsync(item.GroupId, cancellationToken);
                if (group is null)
                {
                    await _outbox.MarkFailedAsync(item.GroupId, item.ETag, "Unknown groupId.", cancellationToken);
                    continue;
                }

                var state = await _storage.GetOrCreateGroupAsync(item.GroupId, cancellationToken);
                if (state.Status != ConfirmationStatus.Confirmed || state.EventResponse is null)
                {
                    await _outbox.MarkFailedAsync(item.GroupId, item.ETag, "Group not confirmed.", cancellationToken);
                    continue;
                }

                var responses = await _storage.GetResponsesAsync(item.GroupId, cancellationToken);
                var personResponses = new Dictionary<string, PersonResponse>(StringComparer.Ordinal);

                foreach (var member in group.Members)
                {
                    if (!responses.TryGetValue(member.PersonId, out var stored))
                    {
                        personResponses[member.PersonId] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null);
                        continue;
                    }

                    var attending = Enum.TryParse<AttendingStatus>(stored.Attending, out var a) ? a : AttendingStatus.No;
                    if (attending == AttendingStatus.No)
                    {
                        personResponses[member.PersonId] = new PersonResponse(AttendingStatus.No, HasAllergies: null, AllergiesText: null);
                        continue;
                    }

                    personResponses[member.PersonId] = new PersonResponse(
                        AttendingStatus.Yes,
                        HasAllergies: stored.HasAllergies,
                        AllergiesText: stored.AllergiesText);
                }

                var submission = new GroupSubmission(state.EventResponse, personResponses);
                var rows = Validation.ToSheetRows(group, submission);

                await _exporter.UpsertRowsAsync(rows, cancellationToken);
                await _outbox.MarkSucceededAsync(item.GroupId, item.ETag, cancellationToken);
            }
            catch (Exception ex)
            {
                // Don't log PII (names/allergies). groupId is safe.
                _logger.LogWarning(
                    "Export table write failed for group {GroupId}. ErrorType={ErrorType}",
                    item.GroupId,
                    ex.GetType().FullName);

                await _outbox.MarkFailedAsync(item.GroupId, item.ETag, ex.GetType().Name, cancellationToken);
            }
        }
    }
}
