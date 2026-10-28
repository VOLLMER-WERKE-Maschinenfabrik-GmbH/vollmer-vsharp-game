using Microsoft.AspNetCore.SignalR;
using VGame.Contracts;
using VGame.Domain;
using VGame.Server.Security;
using VGame.Server.Services;

namespace VGame.Server.Hubs;

public interface IGameClient
{
    Task SnapshotUpdated(GameSnapshot snapshot);
    Task ActionResolved(ActionResolved action);
    Task DemoUpdated(DemoSnapshot snapshot);
}

public sealed class GameHub(GameEngine gameEngine, IClientOriginPolicy clientOriginPolicy, HighscoreService highscores,
    DemoGameService demoGame) : Hub<IGameClient>
{
    private const string ClientInstanceContextKey = "vgame.client-instance-id";

    public async Task<JoinResponse> JoinGame(JoinRequest request)
    {
        var isLocalClient = clientOriginPolicy.IsLocal(Context.GetHttpContext());
        var response = gameEngine.JoinClient(request, isLocalClient);
        Context.Items[ClientInstanceContextKey] = response.ClientInstanceId;
        await Clients.All.SnapshotUpdated(response.Snapshot);
        return response;
    }

    public async Task<GameSnapshot> GetSnapshot()
    {
        EnsureJoined();
        var snapshot = gameEngine.GetSnapshot();
        await Clients.Caller.SnapshotUpdated(snapshot);
        return snapshot;
    }

    public DemoSnapshot GetDemoSnapshot()
    {
        EnsureJoined();
        return demoGame.GetSnapshot();
    }

    public async Task<bool> SelectMode(GameMode mode)
    {
        var clientId = GetJoinedClientId();
        if (clientId is null || !gameEngine.SelectMode(clientId, mode))
        {
            return false;
        }

        await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        return true;
    }

    public async Task<bool> StartRound()
    {
        var clientId = GetJoinedClientId();
        if (clientId is null || !highscores.StartRound(clientId))
        {
            return false;
        }

        await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        return true;
    }

    public async Task<bool> StartRegisteredRound(PlayerRegistration registration)
    {
        var clientId = GetJoinedClientId();
        if (clientId is null) return false;
        try
        {
            if (!highscores.StartRound(clientId, registration)) return false;
        }
        catch (ArgumentException exception)
        {
            throw new HubException(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new HubException("Die Rangliste kann derzeit nicht gespeichert werden.");
        }
        await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        return true;
    }

    public IReadOnlyList<HighscoreEntry> GetHighscores(GameMode mode)
    {
        EnsureJoined();
        return highscores.GetHighscores(mode);
    }

    public OwnHighscore? GetOwnHighscore(Guid roundId)
    {
        var clientId = GetJoinedClientId();
        return clientId is null ? null : highscores.GetOwnHighscore(clientId, roundId);
    }

    public bool DeleteHighscore(string deletionCode)
    {
        EnsureJoined();
        return clientOriginPolicy.IsLocal(Context.GetHttpContext()) && highscores.DeleteHighscore(deletionCode);
    }

    public async Task<bool> AbortRound(Guid roundId)
    {
        var clientId = GetJoinedClientId();
        if (clientId is null || !gameEngine.AbortRound(clientId, roundId))
        {
            return false;
        }

        await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        return true;
    }

    public async Task<bool> RequestPlayerRole()
    {
        var clientId = GetJoinedClientId();
        if (clientId is null
            || !clientOriginPolicy.IsLocal(Context.GetHttpContext())
            || !gameEngine.RequestPlayerRole(clientId, isLocalClient: true))
        {
            return false;
        }

        await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        return true;
    }

    public async Task<bool> ReleaseRole(bool confirmationComplete)
    {
        if (!clientOriginPolicy.IsLocal(Context.GetHttpContext())
            || !gameEngine.ReleaseRole(isLocalOperator: true, confirmationComplete))
        {
            return false;
        }

        await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        return true;
    }

    public async Task<bool> AbortRoundAndReleaseRole(bool confirmationComplete)
    {
        if (!clientOriginPolicy.IsLocal(Context.GetHttpContext())
            || !gameEngine.AbortRoundAndReleaseRole(isLocalOperator: true, confirmationComplete))
        {
            return false;
        }

        await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        return true;
    }

    public async Task<ActionCommandResult> SubmitAction(ActionRequested request)
    {
        var clientId = GetJoinedClientId();
        if (clientId is null || !string.Equals(clientId, request.ClientInstanceId, StringComparison.Ordinal))
        {
            return new ActionCommandResult(false, false, null, "client_mismatch");
        }

        var result = gameEngine.ResolveAction(request);
        if (result.Resolution is not null && !result.Duplicate)
        {
            await Clients.All.ActionResolved(result.Resolution);
            await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        }
        else if (!result.Accepted && result.RejectionReason == "round_ended")
        {
            await Clients.All.SnapshotUpdated(gameEngine.GetSnapshot());
        }

        return result;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var clientId = GetJoinedClientId();
        if (clientId is not null)
        {
            var snapshot = gameEngine.DisconnectClient(clientId);
            if (snapshot is not null)
            {
                await Clients.All.SnapshotUpdated(snapshot);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string? GetJoinedClientId()
    {
        return Context.Items.TryGetValue(ClientInstanceContextKey, out var clientId)
            ? clientId as string
            : null;
    }

    private void EnsureJoined()
    {
        if (GetJoinedClientId() is null)
        {
            throw new HubException("Join the game before requesting a snapshot.");
        }
    }
}