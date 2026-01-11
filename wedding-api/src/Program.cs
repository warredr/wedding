using Microsoft.Azure.Functions.Worker.Builder;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeddingApi.Application;
using WeddingApi.Functions;
using WeddingApi.Infrastructure.ExportTable;
using WeddingApi.Invites;
using WeddingApi.Storage;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<CorsMiddleware>();

builder.Services.AddSingleton<AccessGate>();

builder.Services
	.AddOptions<RsvpOptions>()
	.BindConfiguration(RsvpOptions.SectionName)
	.Validate(o =>
		Guid.TryParse(o.QrAccessKey, out _) &&
		o.SixDigitAccessCode.Length == 6 &&
		o.SixDigitAccessCode.All(char.IsDigit) &&
		!string.IsNullOrWhiteSpace(o.SessionSigningKey) &&
		Guid.TryParse(o.AdminKey, out _),
		"Invalid RSVP configuration")
	.ValidateOnStart();

builder.Services
	.AddOptions<InvitesOptions>()
	.BindConfiguration(InvitesOptions.SectionName)
	.ValidateOnStart();

builder.Services
	.AddOptions<StorageOptions>()
	.BindConfiguration(StorageOptions.SectionName)
	.ValidateOnStart();


builder.Services
	.AddOptions<ExportTableOptions>()
	.BindConfiguration(ExportTableOptions.SectionName)
	.ValidateOnStart();

builder.Services.AddSingleton<IInviteRepository, JsonInviteRepository>();

builder.Services.AddSingleton(sp =>
{
	var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
		?? throw new InvalidOperationException("AzureWebJobsStorage is not configured.");
	return new TableServiceClient(connectionString);
});

builder.Services.AddSingleton<IRsvpStorage, TableStorageRepository>();

builder.Services.AddSingleton<ISheetExportOutbox, SheetExportOutboxRepository>();
builder.Services.AddSingleton<IExportTableExporter, ExportTableExporter>();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
