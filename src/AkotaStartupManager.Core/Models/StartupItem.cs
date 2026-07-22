namespace AkotaStartupManager.Core.Models;

public enum StartupSourceType
{
    Registry,
    StartupFolder,
    ScheduledTask
}

public enum StartupScope
{
    CurrentUser,
    AllUsers
}

public sealed record StartupItem(
    string Id,
    string Name,
    string Command,
    string Arguments,
    string WorkingDirectory,
    StartupSourceType SourceType,
    StartupScope Scope,
    bool IsEnabled,
    bool RequiresElevation,
    string Location,
    IReadOnlyDictionary<string, string>? Metadata = null);
