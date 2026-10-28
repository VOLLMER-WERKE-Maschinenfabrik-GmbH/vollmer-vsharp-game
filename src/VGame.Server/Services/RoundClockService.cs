using Microsoft.AspNetCore.SignalR;
using VGame.Domain;
using VGame.Server.Hubs;

namespace VGame.Server.Services;

public sealed class RoundClockService(
    GameEngine gameEngine,
    IHubContext<GameHub, IGameClient> hubContext,
    HighscoreService highscores,
    DemoGameService demoGame,
    ILogger<RoundClockService> logger) : BackgroundService
{
    private bool _storageWarningReported;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = gameEngine.AdvanceClock();
            try
            {
                highscores.ProcessRound();
                _storageWarningReported = false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (!_storageWarningReported) logger.LogWarning("Highscores können derzeit nicht gespeichert werden; erneuter Versuch folgt.");
                _storageWarningReported = true;
            }
            if (snapshot is not null)
            {
                await hubContext.Clients.All.SnapshotUpdated(snapshot);
            }
            await hubContext.Clients.All.DemoUpdated(demoGame.Advance());
        }
    }
}