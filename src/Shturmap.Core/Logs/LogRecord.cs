namespace Shturmap.Core.Logs;

/// <summary>
/// One entry of an EFT log: the header line, plus the pretty-printed JSON block that some entries carry
/// on the following lines (it starts with "{" and ends with "}" at column 0).
/// </summary>
/// <param name="Timestamp">Local time as written by the game.</param>
/// <param name="Channel">The log's channel: "application", "push-notifications", ...</param>
/// <param name="Message">Everything after the channel field; may itself contain '|'.</param>
/// <param name="Body">Continuation lines (usually JSON), or null.</param>
public sealed record LogRecord(DateTime Timestamp, string GameVersion, string Level, string Channel, string Message, string? Body)
{
    /// <summary>
    /// Whether the entry began in text its reader was told is a replay (<see cref="LogRecordReader.Replay"/>): it was
    /// in the log before Shturmap started following it.
    /// </summary>
    public bool IsReplay { get; init; }
}
