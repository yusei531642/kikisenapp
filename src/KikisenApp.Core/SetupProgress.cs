namespace KikisenApp.Core;

public sealed record SetupProgress(
    string Stage,
    string Message,
    long? ReceivedBytes = null,
    long? TotalBytes = null);
