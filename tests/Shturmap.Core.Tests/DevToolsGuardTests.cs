using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace Shturmap.Core.Tests;

/// <summary>
/// The developer view never ships (owner, 2026-10-03; docs/DESIGN.md §8, "Developer aids"): its code lives in Dev
/// folders, each file wholly inside "#if DEVTOOLS", every use of it elsewhere is inside "#if DEVTOOLS" too, and
/// DEVTOOLS is defined only for Debug builds and eng\dev.ps1's ShturmapDev builds.
/// </summary>
public partial class DevToolsGuardTests
{
    // The developer view's types and members, as they are named outside its folders.
    [GeneratedRegex(@"\b(?:FakeGame|DevMap|DevPlaces|DevChangelog|DevCommit|DevScript|DevStep|DevController|DevView|Dev[A-Z]\w*|Shturmap\.(?:Session|App)\.Dev)\b")]
    private static partial Regex DevName();

    private static string Src => Path.Combine(RepositoryRoot(), "src");

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static bool InDevFolder(string file) => file.Contains($"{Path.DirectorySeparatorChar}Dev{Path.DirectorySeparatorChar}");

    [Fact]
    public void Every_file_in_a_dev_folder_is_wholly_inside_devtools()
    {
        var files = SourceFiles().Where(InDevFolder).ToList();
        Assert.NotEmpty(files);
        var violations = files.Where(f =>
        {
            var lines = File.ReadAllLines(f).Where(l => l.Trim().Length > 0).ToList();
            return lines.Count == 0 || lines[0].Trim() != "#if DEVTOOLS" || lines[^1].Trim() != "#endif";
        }).Select(f => Path.GetRelativePath(Src, f)).ToList();
        Assert.True(violations.Count == 0, "Not wrapped in #if DEVTOOLS: " + string.Join(", ", violations));
    }

    [Fact]
    public void Outside_the_dev_folders_the_dev_view_is_only_named_inside_devtools()
    {
        var violations = ReleaseCode()
            .Select(l => (l.File, l.Number, Match: DevName().Match(l.Text)))
            .Where(l => l.Match.Success)
            .Select(l => $"{l.File}:{l.Number} names {l.Match.Value} outside #if DEVTOOLS")
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    // The study log's switch, flag, setting key and writer, as they are named in the source.
    [GeneratedRegex(@"\b(?:StudySetting|StudyOverride\w*|StudyOn|SetStudyLogAsync|AddStudySwitch|WriteValue)\b|""--study""|Keep a study log")]
    private static partial Regex StudyPart();

    /// <summary>
    /// The study log is in developer builds only (owner, 2026-10-03): what a release compiles names none of its switch,
    /// flag, setting key or writer, and no XAML holds the switch (it is built in code under DEVTOOLS).
    /// <see cref="A_release_build_has_no_study_log"/> checks a build itself.
    /// </summary>
    [Fact]
    public void Outside_devtools_there_is_no_study_switch_flag_or_writer()
    {
        var violations = ReleaseCode()
            .Where(l => StudyPart().IsMatch(l.Text))
            .Select(l => $"{l.File}:{l.Number}: {l.Text.Trim()}")
            .Concat(Directory.EnumerateFiles(Src, "*.xaml", SearchOption.AllDirectories)
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"Keep a study log|StudyLogOn|OnStudyLogClick"))
                .Select(f => $"{Path.GetRelativePath(Src, f)} holds the study switch"))
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    // The code lines a release compiles, outside the Dev folders: not inside "#if DEVTOOLS" (its #else is), no comments.
    private static IEnumerable<(string File, int Number, string Text)> ReleaseCode()
    {
        foreach (var file in SourceFiles().Where(f => !InDevFolder(f)))
        {
            // Which of the open #if blocks are DEVTOOLS ones (their #else is not).
            var open = new Stack<bool>();
            var number = 0;
            foreach (var raw in File.ReadLines(file))
            {
                number++;
                var line = raw.Trim();
                if (line.StartsWith("#if ", StringComparison.Ordinal))
                {
                    open.Push(line.Contains("DEVTOOLS", StringComparison.Ordinal) && !line.Contains("!DEVTOOLS", StringComparison.Ordinal));
                    continue;
                }
                if (line.StartsWith("#else", StringComparison.Ordinal) && open.Count > 0)
                {
                    // The other branch of a DEVTOOLS block is what a release compiles.
                    open.Pop();
                    open.Push(false);
                    continue;
                }
                if (line.StartsWith("#endif", StringComparison.Ordinal) && open.Count > 0)
                {
                    open.Pop();
                    continue;
                }
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("///", StringComparison.Ordinal))
                    continue;
                if (!open.Contains(true))
                    yield return (Path.GetRelativePath(Src, file), number, raw);
            }
        }
    }

    [Fact]
    public void Devtools_is_defined_only_for_debug_and_dev_builds()
    {
        var root = RepositoryRoot();
        var definitions = new[] { "Directory.Build.props", "Directory.Build.targets" }
            .SelectMany(f => File.ReadAllLines(Path.Combine(root, f)).Select((l, i) => (File: f, Line: i, Text: l)))
            .ToList();
        var defines = definitions.Where(d => d.Text.Contains("DEVTOOLS</DefineConstants>", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(defines);
        foreach (var (file, line, _) in defines)
        {
            // The PropertyGroup just above says when.
            var group = definitions.Last(d => d.File == file && d.Line < line && d.Text.Contains("<PropertyGroup", StringComparison.Ordinal)).Text;
            Assert.Contains("'$(Configuration)' == 'Debug'", group, StringComparison.Ordinal);
            Assert.DoesNotContain("Release", group, StringComparison.Ordinal);
        }
        // No project defines it on its own.
        var projects = Directory.EnumerateFiles(Src, "*.csproj", SearchOption.AllDirectories)
            .Where(p => File.ReadAllText(p).Contains("DEVTOOLS", StringComparison.Ordinal))
            .Select(p => Path.GetRelativePath(Src, p));
        Assert.Empty(projects);
    }

    /// <summary>A Release build on this PC (eng\publish.ps1, eng\release.ps1) holds no developer-view type or member.
    /// Skipped where no Release build exists yet.</summary>
    [Fact]
    public void A_release_build_holds_none_of_it()
    {
        var builds = ShippedBuilds();
        if (builds.Count == 0)
            Assert.Skip("No Release build on this PC.");
        var found = new List<string>();
        foreach (var dll in builds)
        {
            using var pe = new PEReader(File.OpenRead(dll));
            var md = pe.GetMetadataReader();
            foreach (var handle in md.TypeDefinitions)
            {
                var type = md.GetTypeDefinition(handle);
                var name = $"{md.GetString(type.Namespace)}.{md.GetString(type.Name)}";
                if (DevName().IsMatch(name))
                    found.Add($"{Path.GetFileName(dll)}: type {name}");
                foreach (var m in type.GetMethods())
                {
                    var method = md.GetString(md.GetMethodDefinition(m).Name);
                    if (Regex.IsMatch(method, @"^(?:get_|set_)?Dev[A-Z]"))
                        found.Add($"{Path.GetFileName(dll)}: {name}.{method}");
                }
            }
        }
        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found));
    }

    /// <summary>
    /// The study log is in developer builds only (owner, 2026-10-03: "The study log should only be part of the dev version
    /// and not be in the release version"): a Release build has no settings switch (neither in code nor in the compiled
    /// XAML), no <c>--study</c>, no setting key and no line writer. Skipped where no Release build exists yet.
    /// </summary>
    [Fact]
    public void A_release_build_has_no_study_log()
    {
        var builds = ShippedBuilds();
        if (builds.Count == 0)
            Assert.Skip("No Release build on this PC.");
        string[] members = ["SetStudyLogAsync", "StudyOverrideFor", "StudyOn", "get_StudyOverride", "set_StudyOverride", "StudySetting",
            "WriteValue", "AddStudySwitch", "OnStudyLogClick"];
        // String literals are kept as UTF-16 (the #US heap and constant blobs, and the XAML in resources.pri).
        string[] literals = ["studyLog", "--study", "Keep a study log"];
        var found = new List<string>();
        foreach (var file in builds.Concat(builds.Select(Path.GetDirectoryName).Distinct().Select(d => Path.Combine(d!, "resources.pri")).Where(File.Exists)))
        {
            var bytes = File.ReadAllBytes(file);
            foreach (var literal in literals)
            {
                if (bytes.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes(literal)) >= 0)
                    found.Add($"{Shipped(file)}: \"{literal}\"");
            }
        }
        foreach (var dll in builds)
        {
            using (var pe = new PEReader(File.OpenRead(dll)))
            {
                var md = pe.GetMetadataReader();
                foreach (var handle in md.TypeDefinitions)
                {
                    var type = md.GetTypeDefinition(handle);
                    var name = md.GetString(type.Name);
                    if (name is not ("GameSession" or "StudyLog" or "MainWindow"))
                        continue;
                    foreach (var m in type.GetMethods())
                    {
                        var method = md.GetString(md.GetMethodDefinition(m).Name);
                        if (members.Contains(method))
                            found.Add($"{Shipped(dll)}: {name}.{method}");
                    }
                    foreach (var f in type.GetFields())
                    {
                        var field = md.GetString(md.GetFieldDefinition(f).Name);
                        if (members.Contains(field))
                            found.Add($"{Shipped(dll)}: {name}.{field}");
                    }
                }
            }
        }
        Assert.True(found.Count == 0, string.Join(Environment.NewLine, found));
    }

    // What ships: eng\release.ps1's app and eng\publish.ps1's folder build. Not bin\Release, which eng\dev.ps1's dev build
    // (Release with ShturmapDev=true, developer tools on purpose) shares.
    private static List<string> ShippedBuilds()
    {
        var artifacts = Path.Combine(RepositoryRoot(), "artifacts");
        return new[] { Path.Combine(artifacts, "release", "app"), Path.Combine(artifacts, "Shturmap") }
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "Shturmap*.dll", SearchOption.TopDirectoryOnly))
            .Where(f => Path.GetFileName(f) is "Shturmap.dll" or "Shturmap.Session.dll")
            .ToList();
    }

    // "release\app\Shturmap.dll": which build a finding is in.
    private static string Shipped(string file) => Path.GetRelativePath(Path.Combine(RepositoryRoot(), "artifacts"), file);

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shturmap.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
