using Microsoft.AspNetCore.SignalR;
using VGame.Domain;
using VGame.Server.Hubs;

namespace VGame.Server.Services;

public sealed class RoundClockService(
    GameEngine gameEngine,
    IHubContext<GameHub, IGameClient> hubContext) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = gameEngine.AdvanceClock();
            if (snapshot is not null)
            {
                await hubContext.Clients.All.SnapshotUpdated(snapshot);
            }
        }
    }
}