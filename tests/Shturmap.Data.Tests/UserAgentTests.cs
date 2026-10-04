using System.Text.RegularExpressions;
using Shturmap.Data.Http;

namespace Shturmap.Data.Tests;

// Requests say who they are (docs/DESIGN.md §3): Shturmap, in the version that is asking, with the project's address.
public class UserAgentTests
{
    // The version every Shturmap assembly is built with.
    private static string RepositoryVersion()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var props = Path.Combine(folder.FullName, "Directory.Build.props");
            if (File.Exists(props))
                return Regex.Match(File.ReadAllText(props), "<Version>([^<]+)</Version>").Groups[1].Value;
        }
        throw new InvalidOperationException("Directory.Build.props not found above the tests");
    }

    [Fact]
    public void Requests_carry_the_builds_version_and_the_projects_address()
    {
        using var client = CachedHttp.CreateClient();
        var agent = client.DefaultRequestHeaders.UserAgent.ToList();
        var product = agent[0].Product!;
        Assert.Equal("Shturmap", product.Name);
        // It said "0.1" whatever the version (the review of 2026-10-04). A developer build adds "-dev.<time>".
        Assert.StartsWith(RepositoryVersion(), product.Version);
        Assert.DoesNotContain('+', product.Version!); // no commit
        Assert.Equal("(+https://github.com/shturmap; Escape from Tarkov companion)", agent[1].Comment);
    }
}
