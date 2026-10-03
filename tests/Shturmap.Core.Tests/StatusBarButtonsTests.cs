using System.Xml.Linq;

namespace Shturmap.Core.Tests;

/// <summary>
/// The status bar's buttons at the top right (owner, 2026-10-03: "It's not clear that the settings are with the
/// questionmark … a settings button next to it", and "a separate feedback/bugreport button"): feedback, help, settings,
/// in that order, each panel holding only its own part. Read from MainWindow.xaml, which the app compiles.
/// </summary>
public class StatusBarButtonsTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument MainWindow() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Shturmap.App", "MainWindow.xaml"));

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static HashSet<string> Clicks(XElement e) =>
        e.DescendantsAndSelf().Select(d => (string?)d.Attribute("Click")).OfType<string>().ToHashSet();

    [Fact]
    public void Feedback_help_and_settings_sit_side_by_side_in_that_order()
    {
        var doc = MainWindow();
        var buttons = doc.Descendants().Where(e => e.Name.LocalName == "Button" && (string?)e.Attribute(X + "Name") is
            "FeedbackButton" or "HelpButton" or "SettingsButton").Select(e => (string)e.Attribute(X + "Name")!).ToList();
        Assert.Equal(["FeedbackButton", "HelpButton", "SettingsButton"], buttons);
        Assert.Equal("OnFeedbackClick", (string?)Named(doc, "FeedbackButton").Attribute("Click"));
        // The same frame for all three.
        foreach (var name in buttons)
        {
            var button = Named(doc, name);
            Assert.Equal("28", (string?)button.Attribute("Width"));
            Assert.Equal("28", (string?)button.Attribute("Height"));
        }
    }

    [Fact]
    public void Settings_holds_the_preferences_and_the_app_s_data()
    {
        var doc = MainWindow();
        var settings = Clicks(Named(doc, "SettingsFlyout"));
        foreach (var handler in new[] { "OnStudyLogClick", "OnCrashModeClick", "OnUpdateModeClick", "OnLogFolderClick", "OnPrivacyClick",
                     "OnLicencesClick", "OnUninstallClick", "OnUninstallConfirmClick" })
            Assert.Contains(handler, settings);
        Assert.DoesNotContain("OnCopyDiagnosticsClick", settings);
    }

    [Fact]
    public void Help_holds_the_explanations_and_the_diagnostics_but_no_settings()
    {
        var doc = MainWindow();
        var help = Clicks(Named(doc, "HelpFlyout"));
        Assert.Contains("OnCopyDiagnosticsClick", help);
        foreach (var handler in new[] { "OnStudyLogClick", "OnCrashModeClick", "OnUpdateModeClick", "OnUninstallClick", "OnLogFolderClick" })
            Assert.DoesNotContain(handler, help);
        // The feedback button replaced help's report link.
        Assert.DoesNotContain("OnReportClick", help);
    }

    [Fact]
    public void The_feedback_and_settings_symbols_mean_nothing_else()
    {
        // One symbol, one meaning (docs/DESIGN.md §4): the speech bubble and the gear appear once each in the app.
        var app = Path.Combine(RepositoryRoot(), "src", "Shturmap.App");
        var text = string.Concat(Directory.EnumerateFiles(app, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".xaml") || f.EndsWith(".cs")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText));
        Assert.Equal(1, Count(text, "&#xE939;") + Count(text, "\\uE939"));
        Assert.Equal(1, Count(text, "&#xE713;") + Count(text, "\\uE713"));
    }

    private static int Count(string text, string what)
    {
        var n = 0;
        for (var i = text.IndexOf(what, StringComparison.Ordinal); i >= 0; i = text.IndexOf(what, i + what.Length, StringComparison.Ordinal))
            n++;
        return n;
    }

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
