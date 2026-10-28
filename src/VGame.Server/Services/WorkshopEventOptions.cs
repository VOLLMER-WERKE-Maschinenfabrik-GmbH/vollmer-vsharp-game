namespace VGame.Server.Services;

public sealed record WorkshopEventOptions
{
    public bool Enabled { get; init; } = true;
    public int SurpriseUnlockAfterDistinctEvents { get; init; } = 3;
    public Dictionary<string, WorkshopEventSettings> Events { get; init; } = new(StringComparer.Ordinal)
    {
        ["gauge"] = new(), ["conveyor"] = new(), ["wiper"] = new(), ["stamp"] = new(),
        ["tool"] = new(), ["shield"] = new(), ["drive"] = new(), ["egg"] = new()
    };

    public void Validate()
    {
        string[] eventNames = ["gauge", "conveyor", "wiper", "stamp", "tool", "shield", "drive", "egg"];
        if (Events is null || Events.Count != eventNames.Length
            || SurpriseUnlockAfterDistinctEvents is < 1 or > 7
            || Events.Any(entry => !eventNames.Contains(entry.Key, StringComparer.Ordinal)
                || entry.Value is null
                || entry.Value.DurationMilliseconds is < 1 or > 2000
                || entry.Value.CooldownMilliseconds is < 0 or > 60000))
        {
            throw new ArgumentException("Workshop events require known names, durations from 1 to 2000 milliseconds and cooldowns from 0 to 60000 milliseconds.");
        }

        var availableUnlockEvents = Events.Count(entry => entry.Key != "egg" && entry.Value.Enabled);
        if (SurpriseUnlockAfterDistinctEvents > availableUnlockEvents)
        {
            throw new ArgumentException("The surprise unlock threshold cannot exceed the enabled non-surprise events.");
        }
    }
}

public sealed record WorkshopEventSettings
{
    public bool Enabled { get; init; } = true;
    public int DurationMilliseconds { get; init; } = 1400;
    public int CooldownMilliseconds { get; init; } = 3000;
}