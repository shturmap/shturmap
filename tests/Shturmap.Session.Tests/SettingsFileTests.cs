using Shturmap.Data.Progress;

namespace Shturmap.Session.Tests;

// A shturmap.db that can't be read is set aside and a fresh one opened, with a notice; one that is only in use is left
// alone (review of 2026-10-09: the session didn't start, and the app ran on with nothing and said nothing).
public class SettingsFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-settings-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public async Task A_settings_file_that_is_no_database_is_set_aside_and_the_session_starts_on_a_fresh_one()
    {
        AppPaths? data = null;
        await using var rig = new SessionRig(configure: (paths, locations) =>
        {
            // Before the first start only; a restart opens what the first left.
            if (data is null)
            {
                Directory.CreateDirectory(paths.Root);
                File.WriteAllText(paths.Database, "not a database " + new string('x', 4000));
            }
            data = paths;
            return new GameSession(paths, locations) { GivenData = SessionRig.Data };
        });
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");

        Assert.Contains(SettingsFile.SetAsideNotice, rig.Notices);
        var aside = Assert.Single(Directory.GetFiles(data!.Root, "shturmap.db.unreadable-*"));
        Assert.Matches(@"shturmap\.db\.unreadable-\d{8}-\d{4}$", aside);
        Assert.StartsWith("not a database", File.ReadAllText(aside));

        // The fresh file keeps settings, and the next start opens it without a word.
        await rig.Session.SetDeleteScreenshotsAsync(true);
        await rig.RestartAsync();
        await rig.StartAsync();
        Assert.Equal("on", rig.Session.GetSetting(GameSession.DeleteScreenshotsSetting));
        Assert.Single(rig.Notices, SettingsFile.SetAsideNotice);
        Assert.Single(Directory.GetFiles(data.Root, "shturmap.db.unreadable-*"));
    }

    [Fact]
    public void A_damaged_file_is_set_aside_too()
    {
        var path = Path.Combine(_folder, "shturmap.db");
        using (var store = new ProgressStore(path))
            store.SetSetting("mode", "Pve");
        // The header stays, the pages behind it don't.
        var bytes = File.ReadAllBytes(path);
        Array.Fill(bytes, (byte)0x5A, 100, bytes.Length - 100);
        File.WriteAllBytes(path, bytes);

        var (opened, notice) = SettingsFile.Open(path, Now);
        using (opened)
        {
            Assert.Equal(SettingsFile.SetAsideNotice, notice);
            Assert.Null(opened.GetSetting("mode"));
        }
        Assert.True(File.Exists(path + ".unreadable-20260101-1200"));
    }

    [Fact]
    public void A_second_Shturmap_opens_the_same_file_as_it_is()
    {
        var path = Path.Combine(_folder, "shturmap.db");
        using var first = new ProgressStore(path);
        first.SetSetting("mode", "Regular");

        var (second, notice) = SettingsFile.Open(path, Now);
        using (second)
        {
            Assert.Null(notice);
            Assert.Equal("Regular", second.GetSetting("mode"));
        }
        Assert.Empty(Directory.GetFiles(_folder, "*.unreadable-*"));
    }

    [Fact]
    public void A_file_another_program_holds_is_left_alone_and_nothing_is_kept_this_time()
    {
        var path = Path.Combine(_folder, "shturmap.db");
        using (var store = new ProgressStore(path))
            store.SetSetting("mode", "Regular");
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var (opened, notice) = SettingsFile.Open(path, Now);
            using (opened)
            {
                Assert.Equal(SettingsFile.NotKeptNotice, notice);
                opened.SetSetting("mode", "Pve");
                Assert.Equal("Pve", opened.GetSetting("mode"));
            }
        }
        Assert.Empty(Directory.GetFiles(_folder, "*.unreadable-*"));
        using var again = new ProgressStore(path);
        Assert.Equal("Regular", again.GetSetting("mode"));
    }

    [Fact]
    public void The_wal_and_shm_go_with_it_and_a_name_already_taken_gets_a_number()
    {
        var path = Path.Combine(_folder, "shturmap.db");
        File.WriteAllText(path + "-wal.unreadable-20260101-1200", "earlier");
        File.WriteAllText(path, "now");
        File.WriteAllText(path + "-wal", "now's wal");
        File.WriteAllText(path + "-shm", "now's shm");
        Assert.Equal(path + ".unreadable-20260101-1200-2", SettingsFile.SetAside(path, Now));
        Assert.Equal("earlier", File.ReadAllText(path + "-wal.unreadable-20260101-1200"));
        Assert.Equal("now", File.ReadAllText(path + ".unreadable-20260101-1200-2"));
        Assert.Equal("now's wal", File.ReadAllText(path + "-wal.unreadable-20260101-1200-2"));
        Assert.Equal("now's shm", File.ReadAllText(path + "-shm.unreadable-20260101-1200-2"));
        Assert.Equal(["shturmap.db-shm.unreadable-20260101-1200-2", "shturmap.db-wal.unreadable-20260101-1200", "shturmap.db-wal.unreadable-20260101-1200-2",
            "shturmap.db.unreadable-20260101-1200-2"], Directory.GetFiles(_folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }
}
