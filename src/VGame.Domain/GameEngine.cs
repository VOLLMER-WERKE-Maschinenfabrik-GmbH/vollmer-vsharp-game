using System.Security.Cryptography;
using VGame.Contracts;

namespace VGame.Domain;

public sealed class GameEngine
{
    private const int CachedResolutionsPerClient = 512;
    private const double GeometryEpsilon = 1e-9;
    private readonly object _sync = new();
    private readonly GameplayOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, ClientSession> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToolSnapshot> _tools = new(StringComparer.Ordinal);
    private readonly Dictionary<(string ClientId, long Sequence), ActionResolved> _resolvedCommands = [];
    private readonly Dictionary<string, Queue<long>> _clientCommandSequences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastCommandSequence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _resolvedToolTimestamps = new(StringComparer.Ordinal);
    private string? _activeClientId;
    private GameMode _mode = GameMode.Laser;
    private GamePhase _phase = GamePhase.Attract;
    private Guid? _roundId;
    private long? _roundStartedTimestamp;
    private long _lastMovementTimestamp;
    private long _stateVersion;
    private long _roleVersion;
    private int _score;
    private int _combo;

    public GameEngine(GameplayOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public JoinResponse JoinClient(JoinRequest request, bool isLocalClient)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ClientInstanceId) || request.ClientInstanceId.Length > 128)
        {
            throw new ArgumentException("Client instance ID must contain 1 to 128 characters.", nameof(request));
        }

        if (!Enum.IsDefined(request.RequestedRole))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Requested client role is not supported.");
        }

        lock (_sync)
        {
            var clientId = request.ClientInstanceId;
            if (_clients.TryGetValue(clientId, out var existingSession))
            {
                if (existingSession.IsConnected || !string.Equals(existingSession.ResumeToken, request.ResumeToken, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Client instance is already connected or the resume token is invalid.");
                }

                existingSession.IsConnected = true;
                existingSession.RequestedRole = request.RequestedRole;
                existingSession.IsLocalClient = isLocalClient;
                if (existingSession.Role == ClientRole.Watcher
                    && request.RequestedRole == ClientRole.Player
                    && isLocalClient
                    && (_activeClientId is null || _phase != GamePhase.Playing))
                {
                    AssignActiveRole(clientId, existingSession);
                }

                _stateVersion++;
                return new JoinResponse(clientId, existingSession.Role, _roleVersion, existingSession.ResumeToken, CreateSnapshot());
            }

            var role = request.RequestedRole == ClientRole.Player
                && isLocalClient
                && (_activeClientId is null || _phase != GamePhase.Playing)
                ? ClientRole.Player
                : ClientRole.Watcher;
            var session = new ClientSession(
                role,
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                request.RequestedRole,
                isLocalClient);
            _clients.Add(clientId, session);
            if (role == ClientRole.Player)
            {
                AssignActiveRole(clientId, session);
            }

            _stateVersion++;
            return new JoinResponse(clientId, role, _roleVersion, session.ResumeToken, CreateSnapshot());
        }
    }

    public GameSnapshot? DisconnectClient(string clientInstanceId)
    {
        lock (_sync)
        {
            if (!_clients.TryGetValue(clientInstanceId, out var session) || !session.IsConnected)
            {
                return null;
            }

            session.IsConnected = false;
            if (string.Equals(_activeClientId, clientInstanceId, StringComparison.Ordinal))
            {
                if (_phase == GamePhase.Playing)
                {
                    ResetRound();
                }

                session.Role = ClientRole.Watcher;
                _activeClientId = null;
                _roleVersion++;
            }

            _stateVersion++;
            return CreateSnapshot();
        }
    }

    public ClientRole? GetClientRole(string clientInstanceId)
    {
        lock (_sync)
        {
            return _clients.TryGetValue(clientInstanceId, out var session) && session.IsConnected
                ? session.Role
                : null;
        }
    }

    public bool RequestPlayerRole(string clientInstanceId, bool isLocalClient)
    {
        lock (_sync)
        {
            if (!isLocalClient
                || _phase == GamePhase.Playing
                || !_clients.TryGetValue(clientInstanceId, out var session)
                || !session.IsConnected
                || !session.IsLocalClient
                || session.RequestedRole != ClientRole.Player
                || session.Role == ClientRole.Player && string.Equals(_activeClientId, clientInstanceId, StringComparison.Ordinal))
            {
                return false;
            }

            AssignActiveRole(clientInstanceId, session);
            _stateVersion++;
            return true;
        }
    }

    public bool SelectMode(string clientInstanceId, GameMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            return false;
        }

        lock (_sync)
        {
            if (!IsActiveClient(clientInstanceId) || _phase == GamePhase.Playing)
            {
                return false;
            }

            _mode = mode;
            _stateVersion++;
            return true;
        }
    }

    public bool StartRound(string clientInstanceId)
    {
        lock (_sync)
        {
            CompleteRoundIfExpired();
            if (!IsActiveClient(clientInstanceId) || _phase == GamePhase.Playing)
            {
                return false;
            }

            _roundId = Guid.NewGuid();
            _roundStartedTimestamp = _timeProvider.GetTimestamp();
            _lastMovementTimestamp = _roundStartedTimestamp.Value;
            _phase = GamePhase.Playing;
            _score = 0;
            _combo = 0;
            _tools.Clear();
            _resolvedToolTimestamps.Clear();
            _stateVersion++;
            return true;
        }
    }

    public bool RegisterTool(ToolDefinition definition, ToolPlacement? placement = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateToolDefinition(definition);
        ValidateToolPlacement(definition, placement, _options);

        lock (_sync)
        {
            if (_phase != GamePhase.Playing || HasRoundExpired() || _tools.ContainsKey(definition.ToolId))
            {
                return false;
            }

            if (placement is not null && IsPlacementTooClose(definition, placement))
            {
                return false;
            }

            _tools.Add(definition.ToolId, new ToolSnapshot(definition, ToolState.Dull, placement));
            _stateVersion++;
            return true;
        }
    }

    public ActionCommandResult ResolveAction(ActionRequested request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sync)
        {
            var commandKey = (request.ClientInstanceId, request.CommandSequence);
            if (_resolvedCommands.TryGetValue(commandKey, out var previousResolution))
            {
                return new ActionCommandResult(true, true, previousResolution, null);
            }

            if (string.IsNullOrWhiteSpace(request.ClientInstanceId)
                || request.CommandSequence <= 0
                || string.IsNullOrWhiteSpace(request.ToolId))
            {
                return Reject("invalid_command");
            }

            if (!_clients.TryGetValue(request.ClientInstanceId, out var session)
                || !session.IsConnected
                || session.Role != ClientRole.Player
                || !string.Equals(request.ClientInstanceId, _activeClientId, StringComparison.Ordinal))
            {
                return Reject("inactive_client");
            }

            if (request.CommandSequence <= _lastCommandSequence.GetValueOrDefault(request.ClientInstanceId))
            {
                return Reject("stale_command");
            }

            if (_phase != GamePhase.Playing || _roundId is null)
            {
                return Reject("round_not_active");
            }

            if (request.RoundId != _roundId)
            {
                return Reject("stale_round");
            }

            if (CompleteRoundIfExpired())
            {
                _stateVersion++;
                return Reject("round_ended");
            }

            if (!IsValidGesture(request.Gesture, out var gestureError))
            {
                return Reject(gestureError);
            }

            var activeTools = _tools.Values.ToArray();
            var contact = HitZoneClassifier.FindFirstContact(
                activeTools,
                request.Gesture.Points,
                _options.ContactToleranceNormalized);
            var resolvedTool = contact is null ? null : _tools[contact.ToolId];
            var outcome = ClassifyOutcome(request.Gesture, contact, resolvedTool);
            var scoreDelta = GetScoreDelta(outcome);
            _score = checked(_score + scoreDelta);
            _combo = outcome == Outcome.Success ? checked(_combo + 1) : 0;

            ToolState? newToolState = null;
            if (contact is not null && resolvedTool is not null && outcome != Outcome.Miss)
            {
                newToolState = outcome == Outcome.Success ? ToolState.Sharp : ToolState.Failed;
                _tools[contact.ToolId] = resolvedTool with { State = newToolState.Value };
                _resolvedToolTimestamps[contact.ToolId] = _timeProvider.GetTimestamp();
            }

            _stateVersion++;
            var resolution = new ActionResolved(
                _roundId.Value,
                Guid.NewGuid(),
                _stateVersion,
                outcome,
                contact?.ToolId,
                contact is null ? null : new ResolvedHitZone(contact.HitZone.ZoneId, contact.HitZone.Role),
                GetAudioEvent(outcome),
                scoreDelta,
                _score,
                _combo,
                newToolState,
                _timeProvider.GetUtcNow(),
                GetFeedbackPoint(request.Gesture.Points, contact));

            CacheResolution(commandKey, resolution);
            _lastCommandSequence[request.ClientInstanceId] = request.CommandSequence;
            return new ActionCommandResult(true, false, resolution, null);
        }
    }

    public GameSnapshot? AdvanceClock()
    {
        lock (_sync)
        {
            var timestamp = _timeProvider.GetTimestamp();
            var changed = RemoveExpiredResolvedTools(timestamp);
            var roundEnded = CompleteRoundIfExpired();
            changed |= roundEnded;
            if (!roundEnded && _phase == GamePhase.Playing)
            {
                changed |= MoveTools(timestamp);
            }

            _lastMovementTimestamp = timestamp;
            if (!changed)
            {
                return null;
            }

            _stateVersion++;
            return CreateSnapshot();
        }
    }

    public GameSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return CreateSnapshot();
        }
    }

    public bool ReleaseRole(bool isLocalOperator, bool confirmationComplete)
    {
        lock (_sync)
        {
            if (!isLocalOperator || !confirmationComplete || _phase == GamePhase.Playing || _activeClientId is null)
            {
                return false;
            }

            if (_clients.TryGetValue(_activeClientId, out var session))
            {
                session.Role = ClientRole.Watcher;
            }

            _activeClientId = null;
            _roleVersion++;
            _stateVersion++;
            return true;
        }
    }

    public bool AbortRoundAndReleaseRole(bool isLocalOperator, bool confirmationComplete)
    {
        lock (_sync)
        {
            if (!isLocalOperator || !confirmationComplete || _phase != GamePhase.Playing || _activeClientId is null)
            {
                return false;
            }

            if (_clients.TryGetValue(_activeClientId, out var session))
            {
                session.Role = ClientRole.Watcher;
            }

            ResetRound();
            _activeClientId = null;
            _roleVersion++;
            _stateVersion++;
            return true;
        }
    }

    private bool IsActiveClient(string clientInstanceId)
    {
        return _clients.TryGetValue(clientInstanceId, out var session)
            && session.IsConnected
            && session.Role == ClientRole.Player
            && string.Equals(clientInstanceId, _activeClientId, StringComparison.Ordinal);
    }

    private bool CompleteRoundIfExpired()
    {
        if (!HasRoundExpired())
        {
            return false;
        }

        _phase = GamePhase.Results;
        _roundStartedTimestamp = null;
        return true;
    }

    private bool HasRoundExpired()
    {
        return _phase == GamePhase.Playing
            && _roundStartedTimestamp is not null
            && _timeProvider.GetElapsedTime(_roundStartedTimestamp.Value).TotalMilliseconds >= _options.RoundDurationSeconds * 1000d;
    }

    private bool RemoveExpiredResolvedTools(long timestamp)
    {
        var expiredToolIds = _resolvedToolTimestamps
            .Where(entry =>
            {
                var despawnDelay = _tools[entry.Key].State == ToolState.Failed
                    ? _options.FailedDespawnMs
                    : _options.SharpDespawnMs;
                return _timeProvider.GetElapsedTime(entry.Value, timestamp).TotalMilliseconds >= despawnDelay;
            })
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var toolId in expiredToolIds)
        {
            _resolvedToolTimestamps.Remove(toolId);
            _tools.Remove(toolId);
        }

        return expiredToolIds.Length > 0;
    }

    private void ResetRound()
    {
        _phase = GamePhase.Attract;
        _roundId = null;
        _roundStartedTimestamp = null;
        _score = 0;
        _combo = 0;
        _tools.Clear();
        _resolvedToolTimestamps.Clear();
    }

    private GameSnapshot CreateSnapshot()
    {
        var remainingMs = 0L;
        if (_phase == GamePhase.Playing && _roundStartedTimestamp is not null)
        {
            var elapsedMs = _timeProvider.GetElapsedTime(_roundStartedTimestamp.Value).TotalMilliseconds;
            remainingMs = Math.Max(0, (long)Math.Ceiling(_options.RoundDurationSeconds * 1000d - elapsedMs));
        }

        var tools = _tools.Values
            .OrderBy(tool => tool.Definition.ZOrder)
            .ThenBy(tool => tool.Definition.ToolId, StringComparer.Ordinal)
            .ToArray();

        return new GameSnapshot(
            _roundId,
            _stateVersion,
            _phase,
            _activeClientId,
            _mode,
            remainingMs,
            _score,
            _combo,
            _roleVersion,
                tools,
                new GameStageArea(_options.StageAreaX, _options.StageAreaY, _options.StageAreaWidth, _options.StageAreaHeight));
    }

            private static Point2 GetFeedbackPoint(IReadOnlyList<Point2> points, ZoneContact? contact)
            {
            if (contact is null) return points[^1];
            if (points.Count == 1) return points[0];
            var position = Math.Clamp(contact.PathProgress, 0, 1) * (points.Count - 1);
            var index = Math.Min((int)position, points.Count - 2);
            var progress = position - index;
            return new Point2(points[index].X + (points[index + 1].X - points[index].X) * progress,
                points[index].Y + (points[index + 1].Y - points[index].Y) * progress);
            }

    private bool IsValidGesture(GesturePayload? gesture, out string rejectionReason)
    {
        if (gesture is null || gesture.Points is null || !Enum.IsDefined(gesture.Kind))
        {
            rejectionReason = "invalid_gesture";
            return false;
        }

        var expectedPointCount = gesture.Kind == GestureKind.Tap ? 1 : 2;
        if (gesture.Points.Count < expectedPointCount
            || gesture.StartedAtMs < 0
            || gesture.EndedAtMs < gesture.StartedAtMs
            || gesture.Points.Any(point => point is null
                || !double.IsFinite(point.X)
                || !double.IsFinite(point.Y)
                || point.X is < 0 or > 1
                || point.Y is < 0 or > 1))
        {
            rejectionReason = "invalid_gesture";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    private bool MoveTools(long timestamp)
    {
        var elapsedSeconds = _timeProvider.GetElapsedTime(_lastMovementTimestamp, timestamp).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return false;
        }

        var changed = false;
        foreach (var tool in _tools.Values.ToArray())
        {
            if (tool.State != ToolState.Dull || tool.Placement is null)
            {
                continue;
            }

            var definition = tool.Definition;
            var placement = tool.Placement;
            var minimumX = _options.StageAreaX + definition.BoundingWidthNormalized / 2;
            var maximumX = _options.StageAreaX + _options.StageAreaWidth - definition.BoundingWidthNormalized / 2;
            var minimumY = _options.StageAreaY + definition.BoundingHeightNormalized / 2;
            var maximumY = _options.StageAreaY + _options.StageAreaHeight - definition.BoundingHeightNormalized / 2;
            var nextX = placement.Center.X + placement.VelocityXPerSecond * elapsedSeconds;
            var nextY = placement.Center.Y + placement.VelocityYPerSecond * elapsedSeconds;
            var velocityX = placement.VelocityXPerSecond;
            var velocityY = placement.VelocityYPerSecond;

            if (nextX < minimumX)
            {
                nextX = minimumX + minimumX - nextX;
                velocityX = Math.Abs(velocityX);
            }
            else if (nextX > maximumX)
            {
                nextX = maximumX - (nextX - maximumX);
                velocityX = -Math.Abs(velocityX);
            }

            if (nextY < minimumY)
            {
                nextY = minimumY + minimumY - nextY;
                velocityY = Math.Abs(velocityY);
            }
            else if (nextY > maximumY)
            {
                nextY = maximumY - (nextY - maximumY);
                velocityY = -Math.Abs(velocityY);
            }

            nextX = Math.Clamp(nextX, minimumX, maximumX);
            nextY = Math.Clamp(nextY, minimumY, maximumY);
            var candidatePlacement = placement with
            {
                Center = new Point2(nextX, nextY),
                VelocityXPerSecond = velocityX,
                VelocityYPerSecond = velocityY
            };
            if (IsPlacementTooClose(definition, candidatePlacement, definition.ToolId))
            {
                candidatePlacement = placement with
                {
                    VelocityXPerSecond = -placement.VelocityXPerSecond,
                    VelocityYPerSecond = -placement.VelocityYPerSecond
                };
            }

            _tools[definition.ToolId] = tool with
            {
                Placement = candidatePlacement
            };
            changed = true;
        }

        return changed;
    }

    private Outcome ClassifyOutcome(GesturePayload gesture, ZoneContact? contact, ToolSnapshot? tool)
    {
        if (contact is null || tool is null)
        {
            return Outcome.Miss;
        }

        if (_mode == GameMode.Laser)
        {
            if (contact.HitZone.Role == HitZoneRole.Shaft)
            {
                return Outcome.ShaftHit;
            }

            return gesture.Kind == GestureKind.Tap ? Outcome.Success : Outcome.WrongSharpening;
        }

        if (gesture.Kind != GestureKind.Swipe || contact.HitZone.Role != HitZoneRole.Cut)
        {
            return Outcome.WrongSharpening;
        }

        var pathLength = GetPathLength(gesture.Points);
        var requiredLength = _options.MinStrokeLengthNormalized
            * Math.Max(tool.Definition.BoundingWidthNormalized, tool.Definition.BoundingHeightNormalized);
        var directionDifference = GetDirectionDifference(gesture.Points, tool.Definition.CutDirectionDegrees);
        return pathLength + GeometryEpsilon >= requiredLength
            && directionDifference <= _options.MaxAngleDeviationDeg + GeometryEpsilon
            ? Outcome.Success
            : Outcome.WrongSharpening;
    }

    private int GetScoreDelta(Outcome outcome)
    {
        return outcome switch
        {
            Outcome.Success => _options.CorrectScore,
            Outcome.ShaftHit => _options.ShaftHitPenalty,
            Outcome.WrongSharpening => _options.WrongSharpeningPenalty,
            Outcome.Miss => _options.MissPenalty,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }

    private string GetAudioEvent(Outcome outcome)
    {
        var success = outcome == Outcome.Success;
        return (_mode, success) switch
        {
            (GameMode.Laser, true) => "laser_success",
            (GameMode.Laser, false) => "laser_fail",
            (GameMode.Saw, true) => "saw_success",
            (GameMode.Saw, false) => "saw_fail",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }

    private static double GetPathLength(IReadOnlyList<Point2> points)
    {
        var totalLength = 0d;
        for (var index = 1; index < points.Count; index++)
        {
            totalLength += Math.Sqrt(
                Math.Pow(points[index].X - points[index - 1].X, 2)
                + Math.Pow(points[index].Y - points[index - 1].Y, 2));
        }

        return totalLength;
    }

    private static double GetDirectionDifference(IReadOnlyList<Point2> points, double cutDirectionDegrees)
    {
        var start = points[0];
        var end = points[^1];
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        if (Math.Abs(deltaX) <= GeometryEpsilon && Math.Abs(deltaY) <= GeometryEpsilon)
        {
            return 180;
        }

        var directionDegrees = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;
        var difference = Math.Abs((directionDegrees - cutDirectionDegrees) % 360);
        return difference > 180 ? 360 - difference : difference;
    }

    private static void ValidateToolDefinition(ToolDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ToolId)
            || definition.HitZones is null
            || definition.HitZones.Count != 2
            || !double.IsFinite(definition.CutDirectionDegrees)
            || definition.CutDirectionDegrees is < 0 or > 360
            || !double.IsFinite(definition.BoundingWidthNormalized)
            || definition.BoundingWidthNormalized is <= 0 or > 1
            || !double.IsFinite(definition.BoundingHeightNormalized)
            || definition.BoundingHeightNormalized is <= 0 or > 1)
        {
            throw new ArgumentException("Tool definition has invalid identity, zones, direction, or bounds.", nameof(definition));
        }

        var hasCutZone = false;
        var hasShaftZone = false;
        foreach (var hitZone in definition.HitZones)
        {
            if (string.IsNullOrWhiteSpace(hitZone.ZoneId)
                || hitZone.Points is null
                || hitZone.Points.Count < 3
                || hitZone.Points.Any(point => point is null
                    || !double.IsFinite(point.X)
                    || !double.IsFinite(point.Y)
                    || point.X is < 0 or > 1
                    || point.Y is < 0 or > 1))
            {
                throw new ArgumentException("Tool hit zones must be valid normalized polygons.", nameof(definition));
            }

            if (hitZone.Role == HitZoneRole.Cut && !hasCutZone)
            {
                hasCutZone = true;
            }
            else if (hitZone.Role == HitZoneRole.Shaft && !hasShaftZone)
            {
                hasShaftZone = true;
            }
            else
            {
                throw new ArgumentException("A tool requires exactly one cut and one shaft zone.", nameof(definition));
            }
        }

        if (!hasCutZone || !hasShaftZone)
        {
            throw new ArgumentException("A tool requires exactly one cut and one shaft zone.", nameof(definition));
        }
    }

    private static void ValidateToolPlacement(ToolDefinition definition, ToolPlacement? placement, GameplayOptions options)
    {
        if (placement is null)
        {
            return;
        }

        if (placement.Center is null
            || !double.IsFinite(placement.Center.X)
            || !double.IsFinite(placement.Center.Y)
            || placement.Center.X is < 0 or > 1
            || placement.Center.Y is < 0 or > 1
            || !double.IsFinite(placement.VelocityXPerSecond)
            || Math.Abs(placement.VelocityXPerSecond) > 1
            || !double.IsFinite(placement.VelocityYPerSecond)
            || Math.Abs(placement.VelocityYPerSecond) > 1
            || !double.IsFinite(placement.RotationDegrees))
        {
            throw new ArgumentException("Tool placement contains invalid normalized coordinates or velocities.", nameof(placement));
        }

        var angleRadians = placement.RotationDegrees * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(angleRadians));
        var sine = Math.Abs(Math.Sin(angleRadians));
        var halfWidth = (cosine * definition.BoundingWidthNormalized + sine * definition.BoundingHeightNormalized) / 2;
        var halfHeight = (sine * definition.BoundingWidthNormalized + cosine * definition.BoundingHeightNormalized) / 2;
        const double boundaryRoundingTolerance = 1e-12;
        if (placement.Center.X - halfWidth < options.StageAreaX - boundaryRoundingTolerance
            || placement.Center.X + halfWidth > options.StageAreaX + options.StageAreaWidth + boundaryRoundingTolerance
            || placement.Center.Y - halfHeight < options.StageAreaY - boundaryRoundingTolerance
            || placement.Center.Y + halfHeight > options.StageAreaY + options.StageAreaHeight + boundaryRoundingTolerance)
        {
            throw new ArgumentException("Tool placement must stay entirely inside the configured stage area.", nameof(placement));
        }
    }

    private bool IsPlacementTooClose(ToolDefinition definition, ToolPlacement placement, string? ignoredToolId = null)
    {
        return _tools.Values.Any(other =>
        {
            if (other.Placement is null || string.Equals(other.Definition.ToolId, ignoredToolId, StringComparison.Ordinal))
            {
                return false;
            }

            var minimumDistance = _options.MinTargetSpacingFactor
                * Math.Max(
                    Math.Max(definition.BoundingWidthNormalized, definition.BoundingHeightNormalized),
                    Math.Max(other.Definition.BoundingWidthNormalized, other.Definition.BoundingHeightNormalized));
            var deltaX = placement.Center.X - other.Placement.Center.X;
            var deltaY = placement.Center.Y - other.Placement.Center.Y;
            return deltaX * deltaX + deltaY * deltaY < minimumDistance * minimumDistance;
        });
    }

    private void AssignActiveRole(string clientId, ClientSession session)
    {
        if (_activeClientId is not null
            && !string.Equals(_activeClientId, clientId, StringComparison.Ordinal)
            && _clients.TryGetValue(_activeClientId, out var previousSession))
        {
            previousSession.Role = ClientRole.Watcher;
        }

        session.Role = ClientRole.Player;
        _activeClientId = clientId;
        _roleVersion++;
    }

    private void CacheResolution((string ClientId, long Sequence) commandKey, ActionResolved resolution)
    {
        _resolvedCommands.Add(commandKey, resolution);
        if (!_clientCommandSequences.TryGetValue(commandKey.ClientId, out var sequences))
        {
            sequences = new Queue<long>();
            _clientCommandSequences.Add(commandKey.ClientId, sequences);
        }

        sequences.Enqueue(commandKey.Sequence);
        if (sequences.Count <= CachedResolutionsPerClient)
        {
            return;
        }

        var expiredSequence = sequences.Dequeue();
        _resolvedCommands.Remove((commandKey.ClientId, expiredSequence));
    }

    private static ActionCommandResult Reject(string reason)
    {
        return new ActionCommandResult(false, false, null, reason);
    }

    private sealed class ClientSession(
        ClientRole role,
        string resumeToken,
        ClientRole requestedRole,
        bool isLocalClient)
    {
        public ClientRole Role { get; set; } = role;
        public string ResumeToken { get; } = resumeToken;
        public ClientRole RequestedRole { get; set; } = requestedRole;
        public bool IsLocalClient { get; set; } = isLocalClient;
        public bool IsConnected { get; set; } = true;
    }
}