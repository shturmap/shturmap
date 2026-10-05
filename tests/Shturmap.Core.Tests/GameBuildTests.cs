using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests;

// The game's build from its log folders' names, for the developer build's notice that the checks after a patch are
// due (docs/UPDATES.md; owner, 2026-10-05).
public class GameBuildTests
{
    [Theory]
    [InlineData("log_2026.01.01_15-00-00_1.1.5.1.47510", "1.1.5.1.47510")]
    [InlineData("log_2026.01.01_9-30-00_1.2.0.0.50001", "1.2.0.0.50001")]
    [InlineData("log_2026.01.01_15-00-00", null)]
    [InlineData("something else", null)]
    [InlineData(null, null)]
    public void The_build_is_read_from_a_log_folders_name(string? folder, string? build) => Assert.Equal(build, GameBuild.Of(folder));

    [Theory]
    [InlineData("1.1.5.1.47510", "1.1.5.1.47509", true)]
    [InlineData("1.1.10.0.1", "1.1.9.9.99999", true)] // as numbers, not as text
    [InlineData("1.1.5.1.47510", "1.1.5.1.47510", false)]
    [InlineData("1.1.5.0.47000", "1.1.5.1.47510", false)] // an older install, a test server: no news
    public void Builds_compare_part_by_part_as_numbers(string build, string than, bool newer) =>
        Assert.Equal(newer, GameBuild.IsNewer(build, than));

    [Fact]
    public void The_first_build_is_kept_without_a_word_and_only_a_newer_one_is_news()
    {
        Assert.Equal(("1.1.5.1.47510", false), GameBuild.Seen("1.1.5.1.47510", null));
        Assert.Equal(("1.1.6.0.48001", true), GameBuild.Seen("1.1.6.0.48001", "1.1.5.1.47510"));
        Assert.Equal(((string?)null, false), GameBuild.Seen("1.1.5.1.47510", "1.1.6.0.48001"));
        Assert.Equal(((string?)null, false), GameBuild.Seen(null, "1.1.6.0.48001"));
    }
}
