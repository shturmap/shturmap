namespace Shturmap.Core.Tests;

// The 0.1.0 single exe unpacked to %TEMP%\.net\<exe name>\<id>; the installed app (0.2.0 on) removes those copies
// once, and nothing else.
public class UnpackedCopiesTests
{
    private const string Temp = @"C:\Users\p\AppData\Local\Temp\";

    [Fact]
    public void Every_version_and_name_of_the_single_exe_may_go()
    {
        var leftovers = UnpackedCopies.Leftovers(Temp,
        [
            Temp + @".net\Shturmap-0.1.0-win-x64\AbC123",
            Temp + @".net\Shturmap\J6EwRDCunGdo",
            Temp + @".net\Shturmap-0.1.0-win-x64 (1)\Zz1",
            Temp + @".net\shturmap-0.1.0-win-x64\Q7\",
            Temp + @".net\SHTURMAP-0.1.0-WIN-X64\XYZ9",
            // The same folder again, as Windows' case-blind paths allow: once.
            Temp + @".net\SHTURMAP-0.1.0-WIN-X64\ABC123",
        ]);
        Assert.Equal(5, leftovers.Count);
    }

    [Fact]
    public void Anything_else_stays()
    {
        var leftovers = UnpackedCopies.Leftovers(Temp,
        [
            Temp + @".net\OtherApp\X1",
            Temp + @".net\Shturmap-0.1.0-win-x64",
            Temp + @".net\Shturmap-0.1.0-win-x64\X1\nested",
            Temp + @"Shturmap\X1",
            @"C:\.net\Shturmap\X1",
            Temp + @".net\Shturmap\..\..\Documents",
            // The install folder and the data folder are never under %TEMP%\.net.
            @"C:\Users\p\AppData\Local\ShturmapApp\current",
            @"C:\Users\p\AppData\Local\Shturmap\logs",
        ]);
        Assert.Empty(leftovers);
    }
}
