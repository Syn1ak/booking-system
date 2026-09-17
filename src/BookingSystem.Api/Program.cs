using BookingSystem.Api.Authentication;
using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using BookingSystem.Api.Features.Rooms;
using BookingSystem.Api.RealTime;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationIdentity();
builder.Services.AddApplicationAuthorization();

// Retries cover transient faults and deadlock victims (1205), which are "ask again" failures
// rather than answers. Three attempts and a two second cap, because a request is waiting: the
// six-retry default backs off for up to thirty seconds.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null)));

builder.Services.AddValidatorsFromAssembly(typeof(IEndpoint).Assembly, includeInternalTypes: true);

// Azure SignalR where it is configured, self-hosted otherwise, so the repository runs and
// tests without an Azure subscription.
var signalR = builder.Services.AddSignalR();
var azureSignalR = builder.Configuration["Azure:SignalR:ConnectionString"];

if (!string.IsNullOrWhiteSpace(azureSignalR))
{
    signalR.AddAzureSignalR(azureSignalR);
}

builder.Services.AddSingleton<ScheduleNotifier>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SlotGenerator>();

builder.Services.AddOptions<SchedulingOptions>()
    .Bind(builder.Configuration.GetSection(SchedulingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SeedOptions>()
    .Bind(builder.Configuration.GetSection(SeedOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

await DatabaseSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();
app.MapHub<ScheduleHub>(ScheduleHub.Path);

// Client-side routes fall back to the SPA, but API and hub paths never do: an unmatched API
// route answered with index.html is a 200 the client cannot parse, not an honest 404.
app.MapFallbackToFile("{*path:regex(^(?!(api|hub)(/|$)).*$)}", "index.html");

app.Run();
