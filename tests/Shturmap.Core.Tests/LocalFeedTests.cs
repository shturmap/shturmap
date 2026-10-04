using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// "--update-feed &lt;folder&gt;" (testing updates with an installed build; docs/DESIGN.md §8, "Distribution") takes a
/// folder on a fixed local drive and nothing else: "//server/share" passed as a local folder (the review of
/// 2026-10-04).
/// </summary>
public class LocalFeedTests
{
    // C: is a fixed disk, E: a USB stick, Z: a mapped share; every folder exists.
    private static DriveType Drives(string root) => root switch
    {
        @"C:\" or @"D:\" => DriveType.Fixed,
        @"E:\" => DriveType.Removable,
        @"Z:\" => DriveType.Network,
        _ => DriveType.NoRootDirectory,
    };

    private static bool Local(string? folder, bool exists = true) => LocalFeed.IsLocalFolder(folder, Drives, _ => exists);

    [Theory]
    [InlineData(@"C:\feed")]
    [InlineData(@"D:\repos\Shturmap\artifacts\dev\feed")]
    [InlineData("C:/feed")]
    [InlineData(@"c:\feed")]
    public void A_folder_on_a_fixed_drive_is_local(string folder) => Assert.True(Local(folder));

    [Theory]
    [InlineData(@"\\server\share\feed")]
    [InlineData("//server/share/feed")]
    [InlineData(@"\/server\share")]
    [InlineData(@"\\?\C:\feed")]
    [InlineData(@"\\.\C:\feed")]
    [InlineData(@"\\localhost\c$\feed")]
    public void A_share_or_device_path_is_not_in_either_slash_form(string folder) => Assert.False(Local(folder));

    [Theory]
    [InlineData(@"Z:\feed")] // a mapped network drive
    [InlineData(@"E:\feed")] // a USB stick
    [InlineData(@"Q:\feed")] // no such drive
    public void Only_a_fixed_drive_counts(string folder) => Assert.False(Local(folder));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("feed")]
    [InlineData(@"C:feed")]
    [InlineData(@"\feed")]
    [InlineData("https://github.com/shturmap/shturmap")]
    public void Anything_that_isn_t_a_full_local_path_is_not(string? folder) => Assert.False(Local(folder));

    [Fact]
    public void The_folder_has_to_exist() => Assert.False(Local(@"C:\feed", exists: false));

    [Fact]
    public void A_drive_that_can_t_be_asked_is_not_local() =>
        Assert.False(LocalFeed.IsLocalFolder(@"C:\feed", _ => throw new ArgumentException("no such drive"), _ => true));

    [Fact]
    public void On_this_pc_the_test_s_own_folder_is_local_only_on_a_fixed_drive()
    {
        var here = AppContext.BaseDirectory;
        var isFixed = Path.GetPathRoot(here) is { Length: 3 } root && new DriveInfo(root).DriveType == DriveType.Fixed;
        Assert.Equal(isFixed, LocalFeed.IsLocalFolder(here));
    }
}
