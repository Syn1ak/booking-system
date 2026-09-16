using System.Text.Json;
using System.Threading.Channels;
using BookingSystem.Api.RealTime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace BookingSystem.IntegrationTests.Fixtures;

/// <summary>
/// A schedule hub connection that records what it receives. Slot changes are kept as raw JSON,
/// so a test can assert on the payload's shape and not only on the fields a record would bind.
/// </summary>
internal sealed class HubClient : IAsyncDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private readonly HubConnection _connection;
    private readonly Channel<JsonElement> _slotChanges = Channel.CreateUnbounded<JsonElement>();
    private readonly Channel<Guid> _scheduleResets = Channel.CreateUnbounded<Guid>();

    private HubClient(HubConnection connection)
    {
        _connection = connection;
        _connection.On<JsonElement>(nameof(IScheduleClient.SlotChanged), change => _slotChanges.Writer.TryWrite(change));
        _connection.On<Guid>(nameof(IScheduleClient.ScheduleReset), roomId => _scheduleResets.Writer.TryWrite(roomId));
    }

    public static async Task<HubClient> ConnectAsync(ApiFactory factory, string? accessToken)
    {
        var connection = new HubConnectionBuilder()
            // Long polling through the TestServer handler: WebSockets need wiring TestServer does
            // not provide, and a misrouted connection times out without any server-side error.
            .WithUrl(new Uri(factory.Server.BaseAddress, ScheduleHub.Path), HttpTransportType.LongPolling, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();

        var client = new HubClient(connection);

        try
        {
            await connection.StartAsync();
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }

        return client;
    }

    public Task WatchAsync(Guid roomId) => _connection.InvokeAsync(nameof(ScheduleHub.Watch), roomId);

    public Task<JsonElement> NextSlotChangeAsync() => _slotChanges.Reader.ReadAsync().AsTask().WaitAsync(ReceiveTimeout);

    public Task<Guid> NextScheduleResetAsync() => _scheduleResets.Reader.ReadAsync().AsTask().WaitAsync(ReceiveTimeout);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
