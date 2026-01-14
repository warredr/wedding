using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WeddingApi.Domain;
using WeddingApi.Invites;
using WeddingApi.Storage;

namespace WeddingApi.Functions;

public sealed class ManageFunctions
{
    private readonly IInviteRepository _invites;
    private readonly IRsvpStorage _storage;

    public ManageFunctions(IInviteRepository invites, IRsvpStorage storage)
    {
        _invites = invites;
        _storage = storage;
    }

    public async Task<HttpResponseData> ExportPersons(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "manage/export/persons")] HttpRequestData req)
    {
        var groups = await _invites.GetAllGroupsAsync(req.FunctionContext.CancellationToken);
        
        // Map personId -> GroupDefinition
        var personGroupMap = new Dictionary<string, GroupDefinition>();
        foreach (var g in groups)
        {
            foreach (var m in g.Members)
            {
                personGroupMap[m.PersonId] = g;
            }
        }

        var responses = await _storage.GetAllResponsesAsync(req.FunctionContext.CancellationToken);
        var groupStates = await _storage.GetAllGroupStatesAsync(req.FunctionContext.CancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("FullName|Dinner|EveningParty|Allergies");

        // Flatten all people
        var allPeople = groups.SelectMany(g => g.Members).OrderBy(p => p.FullName).ToList();

        foreach (var person in allPeople)
        {
            var fullName = person.FullName;
            var dinner = "";
            var evening = "";
            var allergies = "";

            if (personGroupMap.TryGetValue(person.PersonId, out var staticGroup) && 
                groupStates.TryGetValue(staticGroup.GroupId, out var state) &&
                state.Status == ConfirmationStatus.Confirmed)
            {
                // Check Dinner
                var dinnerAtt = state.EventResponse?.DinnerAttendance;
                if (dinnerAtt == EventAttendance.All) dinner = "X";
                else if (dinnerAtt == EventAttendance.One && state.EventResponse?.DinnerSingleAttendeePersonId == person.PersonId) dinner = "X";
                else if (dinnerAtt == EventAttendance.Some && state.EventResponse?.DinnerAttendeePersonIds?.Contains(person.PersonId) == true) dinner = "X";

                // Check Evening
                var eveningAtt = state.EventResponse?.EveningPartyAttendance;
                if (eveningAtt == EventAttendance.All) evening = "X";
                else if (eveningAtt == EventAttendance.One && state.EventResponse?.EveningPartySingleAttendeePersonId == person.PersonId) evening = "X";
                else if (eveningAtt == EventAttendance.Some && state.EventResponse?.EveningPartyAttendeePersonIds?.Contains(person.PersonId) == true) evening = "X";
                
                // Get allergies from specific response
                if (responses.TryGetValue(person.PersonId, out var r) && r.HasAllergies)
                {
                    allergies = r.AllergiesText?.Replace("|", " ") ?? "";
                }
            }

            // CSV safe? Pipe delimiter requested.
            // If Names contain pipe, replace it?
            fullName = fullName.Replace("|", " ");

            sb.AppendLine($"{fullName}|{dinner}|{evening}|{allergies}");
        }
        
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/csv; charset=utf-8");
        res.Headers.Add("Content-Disposition", "attachment; filename=guests.csv");
        await res.Body.WriteAsync(bytes);
        return res;
    }
}
