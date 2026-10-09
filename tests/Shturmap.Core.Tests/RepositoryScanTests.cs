using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Shturmap.Core.Tests;

/// <summary>
/// Everything committed is public for good (CLAUDE.md, "Secrets and private data"). This reads every file git tracks
/// and fails on what must never be in one: an access token, a Sentry DSN, a private key, a claude.ai link, a path in
/// someone's user folder, an email address. A failure names the file, the line and what kind of thing stands there,
/// never the text itself: CI's logs are public too. Each pattern is put together from parts, so this file holds none
/// of the shapes it looks for and is scanned like any other.
/// </summary>
public class RepositoryScanTests
{
    private static readonly (string What, Regex Pattern)[] Shapes =
    [
        // GitHub's classic, OAuth, user-to-server, server-to-server and refresh tokens, and fine-grained ones.
        ("a GitHub token", new(@"\bgh" + "[pousr]_[A-Za-z0-9]{30,}|" + @"\bgithub" + "_pat_[A-Za-z0-9_]{22,}")),
        // Sentry's user and organization auth tokens.
        ("a Sentry token", new(@"\bsntry" + "[us]_[A-Za-z0-9+/=_-]{20,}")),
        // A DSN: a key before the @ and Sentry's host. Sentry's keys are 32 hex digits; the reporting tests' made-up
        // DSNs have short keys on purpose (abc123), so they stand as examples.
        ("a Sentry DSN", new(@"https?://[A-Za-z0-9]{16,}(:[A-Za-z0-9]+)?" + "@" + @"[A-Za-z0-9.-]*(sentry\.io|ingest)", RegexOptions.IgnoreCase)),
        ("a private key", new("-----BEGIN" + " [A-Z ]*PRIVATE" + " KEY-----")),
        // A link into a claude.ai workspace (a session, an artifact); the words "claude.ai links" are no link.
        ("a claude.ai link", new(@"https?://([\w-]+\.)*claude" + @"\.ai\b|\bclaude" + @"\.ai/[\w-]", RegexOptions.IgnoreCase)),
    ];

    // A Windows profile folder, with either slash, doubled in strings and JSON too; the name is up to the next one.
    private static readonly Regex UserFolder = new(@"(?<![A-Za-z])[A-Za-z]:(\\{1,2}|/)" + "Users" + @"(\\{1,2}|/)(?<name>[^\\/:*?""<>|\r\n]+)[\\/]", RegexOptions.IgnoreCase);

    // The made-up names the tests use for a player's profile folder (AppLogTests also its near misses and its short
    // name); anything else could be someone's.
    private static readonly HashSet<string> MadeUpNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Jane Doe", "Jane Doen", "Jane Doe.old", "JANEDO~1", "Player", "tester", "p",
    };

    private static readonly Regex Email = new(@"(?<![\w.%+-])(?<local>[A-Za-z0-9._%+-]+)" + "@" + @"(?<domain>[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,})\b");

    [Fact]
    public void Tracked_files_hold_no_secret_or_private_data()
    {
        var root = SafetyTests.RepositoryRoot();
        var violations = TrackedFiles(root)
            .SelectMany(file => TextLines(Path.Combine(root, file))
                .SelectMany((line, i) => Scan(line).Select(what => $"{file}:{i + 1} has {what}")))
            .ToList();
        Assert.True(violations.Count == 0,
            string.Join(Environment.NewLine, violations.Take(30).Append(violations.Count > 30 ? $"… and {violations.Count - 30} more" : "")));
    }

    // What the patterns are for, made up and put together here so no line of this file holds it.
    [Theory]
    [InlineData("token: " + "gh" + "p_" + "aB3dE5fG7hJ9kL1mN3pQ5rS7tU9vW1xY3zA5", "a GitHub token")]
    [InlineData("GITHUB_TOKEN=" + "github" + "_pat_" + "11ABCDEFG0123456789_abcdefghijklmnopqrstuvwxyz", "a GitHub token")]
    [InlineData("auth " + "sntry" + "u_" + "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "a Sentry token")]
    [InlineData("dsn = https://" + "0123456789abcdef0123456789abcdef" + "@" + "o4500.ingest.de.sentry.io/4501", "a Sentry DSN")]
    [InlineData("-----BEGIN" + " OPENSSH PRIVATE" + " KEY-----", "a private key")]
    [InlineData("see https://" + "claude" + ".ai/code/session_01ABC", "a claude.ai link")]
    [InlineData("artifact: " + "claude" + ".ai/artifact/0f3e", "a claude.ai link")]
    [InlineData(@"Screenshots: C:\" + @"Users\Someone\Documents", "a path in a user folder (write %USERPROFILE%)")]
    [InlineData(@"{""path"":""D:\" + @"\Users\\Someone\\AppData""}", "a path in a user folder (write %USERPROFILE%)")]
    [InlineData("file:///C:/" + "Users/Someone/x.svg", "a path in a user folder (write %USERPROFILE%)")]
    [InlineData("contact: jane.doe" + "@" + "provider.net", "an email address")]
    public void Each_shape_is_found(string line, string what) => Assert.Equal([what], Scan(line));

    // What may stand in the repository: placeholders, examples, mentions without a value.
    [Theory]
    [InlineData(@"%USERPROFILE%\Documents and %LOCALAPPDATA%\Shturmap")]
    [InlineData(@"C:\Users\<user>\AppData")]
    [InlineData("the Sentry user token (sntry" + "u_…) and no claude.ai links")]
    [InlineData("https://abc123" + "@" + "o4500.ingest.de.sentry.io/4501")]
    [InlineData("Co-Authored-By: shturmap <339214139+shturmap" + "@" + "users.noreply.github.com>")]
    [InlineData("Co-Authored-By: Claude <noreply" + "@" + "anthropic.com>")]
    [InlineData("Contact = \"me" + "@" + "example.org\"")]
    [InlineData("uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1")]
    [InlineData("#:package AngleSharp@1.8.3 and shturmap@0.1.0+abc1234")]
    public void What_may_stand_passes(string line) => Assert.Empty(Scan(line));

    /// <summary>What kinds of things a line holds that must not be committed.</summary>
    private static List<string> Scan(string line)
    {
        var found = Shapes.Where(s => s.Pattern.IsMatch(line)).Select(s => s.What).ToList();
        if (UserFolder.Matches(line).Any(m => !MadeUpNames.Contains(m.Groups["name"].Value)))
            found.Add("a path in a user folder (write %USERPROFILE%)");
        // An address right after "://" is a URL's user part (a DSN's key), which the DSN shape looks at.
        if (Email.Matches(line).Any(m => !line[..m.Index].EndsWith("://", StringComparison.Ordinal) && !MayStand(m.Groups["local"].Value, m.Groups["domain"].Value)))
            found.Add("an email address");
        return found;
    }

    // The addresses that may stand, each for its reason; there is no other on purpose.
    private static bool MayStand(string local, string domain) =>
        // GitHub's no-reply addresses: the commit author's is the one identity of the repository (CLAUDE.md).
        domain.Equals("users.noreply.github.com", StringComparison.OrdinalIgnoreCase)
        // The address of the Co-Authored-By line every commit ends with.
        || (local.Equals("noreply", StringComparison.OrdinalIgnoreCase) && domain.Equals("anthropic.com", StringComparison.OrdinalIgnoreCase))
        // Domains reserved for examples (RFC 2606), which no one can own: the reporting tests' contacts.
        || Regex.IsMatch(domain, @"(^|\.)example\.(com|org|net)$|\.(example|test|invalid|localhost)$", RegexOptions.IgnoreCase);

    // The files git tracks, by their path in the repository; ignored and untracked ones (eng\sentry.dsn, the
    // fixtures that stay on the PC) are not in it, and neither is anything outside it.
    private static List<string> TrackedFiles(string root)
    {
        foreach (var git in new[] { "git", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe") })
        {
            var start = new ProcessStartInfo(git, "ls-files -z")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
            };
            Process? process;
            try
            {
                process = Process.Start(start);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                continue;
            }
            using (process)
            {
                var errors = process!.StandardError.ReadToEndAsync();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                _ = errors.Result;
                if (process.ExitCode != 0)
                    Assert.Skip("Not a git checkout: the tracked files can't be listed.");
                return output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }
        Assert.Skip("No git on this PC: the tracked files can't be listed.");
        return [];
    }

    // A file's lines, or none for a binary file (a NUL byte in its first 8000, as git decides) or one deleted
    // since it was tracked.
    private static string[] TextLines(string path)
    {
        if (!File.Exists(path))
            return [];
        var bytes = File.ReadAllBytes(path);
        if (Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0)
            return [];
        return Encoding.UTF8.GetString(bytes).Split('\n');
    }
}
