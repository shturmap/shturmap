using Microsoft.Win32;
using Shturmap.Game.Install;

namespace Shturmap.Game.Tests;

public class InstallLocatorTests
{
    private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
    private const string Session = "log_2026.01.01_15-00-00_1.1.5.1.47510";

    [Fact]
    public void Steam_install_like_the_development_pc()
    {
        var env = new FakeGameEnvironment()
            .Registry(RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Valve\Steam", "SteamPath", "c:/program files (x86)/steam")
            .File(@"c:\program files (x86)\steam\steamapps\libraryfolders.vdf", "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n}")
            .File(@"C:\Program Files (x86)\Steam\steamapps\appmanifest_3932890.acf", "\"AppState\"\n{\n\t\"installdir\"\t\t\"Escape from Tarkov\"\n}")
            .Game(@"C:\Program Files (x86)\Steam\steamapps\common\Escape from Tarkov\build", Session)
            // Steam's uninstall entry exists but has no InstallLocation.
            .Registry(RegistryHive.LocalMachine, RegistryView.Registry64, Uninstall + "Steam App 3932890", "DisplayName", "Escape from Tarkov")
            // A leftover launcher folder holding only game data must not be picked.
            .Dir(@"C:\Battlestate Games\Escape from Tarkov\EscapeFromTarkov_Data")
            .Dir(@"C:\Battlestate Games\BsgLauncher");

        var found = new InstallLocator(env).Locate();

        Assert.Equal(InstallKind.Steam, found.Install?.Kind);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Escape from Tarkov\build", found.Install?.Root, ignoreCase: true);
        Assert.Equal(@"C:\Program Files (x86)\Steam\steamapps\common\Escape from Tarkov\build\Logs", found.LogsFolder, ignoreCase: true);
        Assert.Equal(new DateTime(2026, 1, 1, 15, 0, 0), found.Install?.NewestSession);
        Assert.Contains(found.Candidates, c => c.Root.EndsWith(@"Battlestate Games\Escape from Tarkov", StringComparison.OrdinalIgnoreCase) && !c.IsValid);
        Assert.DoesNotContain(found.Candidates, c => c.Root.EndsWith("BsgLauncher", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(RegistryHive.LocalMachine, RegistryView.Registry32)]
    [InlineData(RegistryHive.LocalMachine, RegistryView.Registry64)]
    [InlineData(RegistryHive.CurrentUser, RegistryView.Default)]
    public void Launcher_install_from_its_uninstall_entry(RegistryHive hive, RegistryView view)
    {
        var env = new FakeGameEnvironment()
            .Registry(hive, view, Uninstall + "EscapeFromTarkov", "InstallLocation", @"D:\Games\EFT\")
            .Game(@"D:\Games\EFT", Session);

        var found = new InstallLocator(env).Locate();

        Assert.Equal(InstallKind.BsgLauncher, found.Install?.Kind);
        Assert.Equal(@"D:\Games\EFT\Logs", found.LogsFolder);
    }

    [Theory]
    [InlineData("gamesRootDir", @"E:\Battlestate", @"E:\Battlestate\EFT (live)")]
    [InlineData("gamesRootDir", @"E:\Battlestate", @"E:\Battlestate\Escape from Tarkov")]
    [InlineData("gameRootDir", @"E:\Battlestate\EFT", @"E:\Battlestate\EFT")] // older launchers pointed at the game folder
    public void Launcher_install_from_its_settings(string key, string root, string gameFolder)
    {
        var settings = $$"""{ "{{key}}": "{{root.Replace(@"\", @"\\")}}", "login": "someone", "at": "token-must-be-ignored", "language": "en" }""";
        var env = new FakeGameEnvironment()
            .File(@"C:\Users\Player\AppData\Roaming\Battlestate Games\BsgLauncher\settings", settings)
            .Game(gameFolder, Session);

        var found = new InstallLocator(env).Locate();

        Assert.Equal(InstallKind.BsgLauncher, found.Install?.Kind);
        Assert.Equal(gameFolder, found.Install?.Root);
        Assert.Equal(Path.Combine(gameFolder, "Logs"), found.LogsFolder);
    }

    [Fact]
    public void Launcher_default_folder_without_registry_or_settings()
    {
        var env = new FakeGameEnvironment().Game(@"C:\Battlestate Games\EFT", Session);

        var found = new InstallLocator(env).Locate();

        Assert.Equal(InstallKind.BsgLauncher, found.Install?.Kind);
        Assert.Equal(@"C:\Battlestate Games\EFT\Logs", found.LogsFolder);
    }

    [Fact]
    public void Launcher_logs_under_build_are_found_too()
    {
        var env = new FakeGameEnvironment()
            .Registry(RegistryHive.LocalMachine, RegistryView.Registry32, Uninstall + "EscapeFromTarkov", "InstallLocation", @"D:\EFT")
            .File(@"D:\EFT\build\EscapeFromTarkov.exe")
            .Dir(@"D:\EFT\build\Logs\" + Session);

        var found = new InstallLocator(env).Locate();

        Assert.Equal(@"D:\EFT\build", found.Install?.Root);
        Assert.Equal(@"D:\EFT\build\Logs", found.LogsFolder);
    }

    [Fact]
    public void With_both_installs_the_newest_log_session_wins_and_both_feed_history()
    {
        var env = new FakeGameEnvironment()
            .Registry(RegistryHive.LocalMachine, RegistryView.Registry32, Uninstall + "EscapeFromTarkov", "InstallLocation", @"C:\Battlestate Games\EFT")
            .Game(@"C:\Battlestate Games\EFT", "log_2026.01.01_10-00-00_1.0.5.0.46000")
            .Registry(RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Valve\Steam", "SteamPath", @"D:\Steam")
            .File(@"D:\Steam\steamapps\appmanifest_3932890.acf", "\"installdir\" \"Escape from Tarkov\"")
            .Game(@"D:\Steam\steamapps\common\Escape from Tarkov\build", Session);

        var found = new InstallLocator(env).Locate();

        Assert.Equal(InstallKind.Steam, found.Install?.Kind);
        Assert.Equal(
            new[] { @"D:\Steam\steamapps\common\Escape from Tarkov\build\Logs", @"C:\Battlestate Games\EFT\Logs" },
            found.AllLogsFolders.ToArray());
    }

    [Fact]
    public void Manual_folder_wins_when_valid()
    {
        var env = new FakeGameEnvironment()
            .Game(@"C:\Battlestate Games\EFT", "log_2026.01.01_20-00-00_1.1.5.1.47510")
            .Game(@"F:\Portable\EFT", Session);

        var found = new InstallLocator(env).Locate(@"F:\Portable\EFT");

        Assert.Equal(InstallKind.Manual, found.Install?.Kind);
    }

    [Fact]
    public void Nothing_installed()
    {
        var found = new InstallLocator(new FakeGameEnvironment()).Locate();
        Assert.Null(found.Install);
        Assert.Equal(@"C:\Users\Player\Documents\Escape from Tarkov\Screenshots", found.ScreenshotsFolder);
        Assert.Equal(@"C:\Users\Player\AppData\Roaming\Battlestate Games\Escape from Tarkov\Settings", found.SettingsFolder);
    }

    [Theory]
    [InlineData("log_2026.01.01_15-00-00_1.1.5.1.47510", "2026-01-01 15:00:00")]
    [InlineData("log_2026.01.01_7-05-00_1.1.0.1.46699", "2026-01-01 07:05:00")]
    public void Session_folder_names(string name, string expected) =>
        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), InstallLocator.SessionStart(name));
}
