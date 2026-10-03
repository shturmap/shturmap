using Shturmap.Data.Progress;

namespace Shturmap.Session.Tests;

// Follow my position is kept between runs, in shturmap.db, and off until the player turns it on (owner, 2026-10-03).
public class MapFollowTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-follow-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void Following_is_off_unless_the_player_turned_it_on()
    {
        Assert.False(MapFollow.Parse(null));
        Assert.False(MapFollow.Parse("something else"));
        Assert.True(MapFollow.Parse(MapFollow.Format(true)));
        Assert.False(MapFollow.Parse(MapFollow.Format(false)));
    }

    [Fact]
    public void The_choice_comes_back_next_time()
    {
        var path = Path.Combine(_folder, "shturmap.db");
        using (var store = new ProgressStore(path))
        {
            Assert.False(MapFollow.Parse(store.GetSetting(MapFollow.Setting)));
            store.SetSetting(MapFollow.Setting, MapFollow.Format(true));
        }
        using (var store = new ProgressStore(path))
        {
            Assert.True(MapFollow.Parse(store.GetSetting(MapFollow.Setting)));
            store.SetSetting(MapFollow.Setting, MapFollow.Format(false));
        }
        using (var again = new ProgressStore(path))
            Assert.False(MapFollow.Parse(again.GetSetting(MapFollow.Setting)));
    }
}
