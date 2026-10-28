using System.Runtime.InteropServices;

namespace Shturmap.Session.Reporting;

/// <summary>
/// When the user signed in to this Windows session. A running marker from before it was left by a Shturmap whose
/// Windows session ended, by a shutdown, restart or sign-out, not by a crash (docs/DESIGN.md §8, "Reports"). The time
/// Windows last started can't say so alone: Windows' Fast Startup turns a shutdown into a sign-out and a hibernation of
/// the system, which keeps the tick count, so that time can lie days back; a sign-in is new after each of them.
/// </summary>
/// <remarks>
/// Read from Windows' own record of the session (WTSQuerySessionInformation, WTSSessionInfo), which any user may read of
/// their own session without rights or a token; of the record only its two times are read. The logon session's record
/// (LsaGetLogonSessionData) holds the same time but needs the process's token and its logon id first.
/// </remarks>
public static class WindowsLogon
{
    /// <summary>This session's sign-in, in local time; null when Windows doesn't say (the boot time alone then holds).</summary>
    public static DateTime? Time()
    {
        try
        {
            if (!WTSQuerySessionInformationW(CurrentServer, CurrentSession, SessionInfo, out var buffer, out var bytes))
            {
                AppLog.Warn($"Crash records: Windows' sign-in time couldn't be read (error {Marshal.GetLastPInvokeError()}); only Windows' start tells a shutdown");
                return null;
            }
            try
            {
                var time = bytes >= Marshal.SizeOf<WtsInfo>()
                    ? FromTimes(Marshal.ReadInt64(buffer, LogonAt), Marshal.ReadInt64(buffer, CurrentAt), DateTime.UtcNow)
                    : null;
                if (time is null)
                    AppLog.Warn("Crash records: Windows gave no sign-in time; only Windows' start tells a shutdown");
                return time;
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            AppLog.Warn("Crash records: Windows' sign-in time couldn't be read; only Windows' start tells a shutdown", e);
            return null;
        }
    }

    /// <summary>
    /// The sign-in from the record's own two times (FILETIME ticks): it was as long before <paramref name="utcNow"/>
    /// as <paramref name="logon"/> is before <paramref name="current"/>, the record's time of asking, so whether Windows
    /// keeps them in UTC or local time doesn't count. Null for a record without a sign-in.
    /// </summary>
    public static DateTime? FromTimes(long logon, long current, DateTime utcNow) =>
        logon <= 0 || current < logon ? null : (utcNow - TimeSpan.FromTicks(current - logon)).ToLocalTime();

    private const nint CurrentServer = 0; // WTS_CURRENT_SERVER_HANDLE
    private const int CurrentSession = -1; // WTS_CURRENT_SESSION
    private const int SessionInfo = 24; // WTSSessionInfo: a WTSINFOW

    private static readonly int LogonAt = (int)Marshal.OffsetOf<WtsInfo>(nameof(WtsInfo.LogonTime));
    private static readonly int CurrentAt = (int)Marshal.OffsetOf<WtsInfo>(nameof(WtsInfo.CurrentTime));

    // WTSINFOW's layout (216 bytes), for the places of its last two times; nothing before them is read.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WtsInfo
    {
        public int State;
        public int SessionId;
        public int IncomingBytes;
        public int OutgoingBytes;
        public int IncomingFrames;
        public int OutgoingFrames;
        public int IncomingCompressedBytes;
        public int OutgoingCompressedBytes;

        // WinStationName[32], Domain[17], UserName[21]: never read.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 70)]
        public char[] Names;

        public long ConnectTime;
        public long DisconnectTime;
        public long LastInputTime;
        public long LogonTime;
        public long CurrentTime;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformationW(nint server, int session, int infoClass, out nint buffer, out int bytes);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(nint memory);
}
