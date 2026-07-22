namespace AkotaStartupManager.Core.Models;

public enum ManagedEntryStatus
{
    Disabled,
    Waiting,
    Checking,
    ReadyDelay,
    Starting,
    Verifying,
    Running,
    Failed,
    Cancelled
}

public sealed record ConditionResult(
    Guid ConditionId,
    bool IsSatisfied,
    string Message,
    IReadOnlyList<ConditionResult>? Children = null);

public sealed record ManagedEntryRuntimeState(
    Guid EntryId,
    ManagedEntryStatus Status,
    string Message,
    int ConsecutiveSuccesses = 0,
    int Attempt = 0,
    DateTimeOffset? LastCheckedAt = null);

public enum LaunchResultKind
{
    Started,
    AlreadyRunning,
    Failed
}

public sealed record LaunchResult(LaunchResultKind Kind, int? ProcessId = null, string? Error = null);
