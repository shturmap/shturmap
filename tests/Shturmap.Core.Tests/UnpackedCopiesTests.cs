namespace Shturmap.Core.Tests;

// The single exe unpacks to %TEMP%\.net\<exe name>\<id>; a start from one removes the other copies, and nothing else.
public class UnpackedCopiesTests
{
    private const string Temp = @"C:\Users\p\AppData\Local\Temp\";
    private const string Own = Temp + @".net\Shturmap-0.1.0-win-x64\AbC123\";

    [Fact]
    public void Other_versions_and_names_of_shturmap_may_go()
    {
        var others = UnpackedCopies.Others(Own, Temp,
        [
            Temp + @".net\Shturmap-0.1.0-win-x64\Old999",
            Temp + @".net\Shturmap\J6EwRDCunGdo",
            Temp + @".net\Shturmap-0.1.0-win-x64 (1)\Zz1",
            Temp + @".net\shturmap-0.2.0-win-x64\Q7\",
        ]);
        Assert.Equal(4, others.Count);
    }

    [Fact]
    public void Its_own_copy_and_anything_else_stay()
    {
        var others = UnpackedCopies.Others(Own, Temp,
        [
            Own,
            Temp + @".net\SHTURMAP-0.1.0-WIN-X64\ABC123",
            Temp + @".net\OtherApp\X1",
            Temp + @".net\Shturmap-0.1.0-win-x64",
            Temp + @".net\Shturmap-0.1.0-win-x64\X1\nested",
            Temp + @"Shturmap\X1",
            @"C:\.net\Shturmap\X1",
            Temp + @".net\Shturmap\..\..\Documents",
        ]);
        Assert.Empty(others);
    }

    [Theory]
    [InlineData(@"C:\Users\p\source\repos\Shturmap\artifacts\Shturmap\")]
    [InlineData(Temp + @".net\OtherApp\X1\")]
    [InlineData(@"D:\unpack\Shturmap\X1\")]
    public void A_start_from_anywhere_else_removes_nothing(string baseDirectory) =>
        Assert.Empty(UnpackedCopies.Others(baseDirectory, Temp, [Temp + @".net\Shturmap\J6EwRDCunGdo"]));
}
