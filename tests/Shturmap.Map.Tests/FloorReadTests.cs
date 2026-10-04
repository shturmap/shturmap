using Shturmap.Core.Maps;

namespace Shturmap.Map.Tests;

// An SVG floor's picture was read by the first paint that showed the floor, on the drawing thread (the review of
// 2026-10-04, A37). A view has the floors read ahead in the background now, and a paint never waits for one.
public sealed class FloorReadTests : IDisposable
{
    private static readonly MapDefinition Map = new()
    {
        Key = "test",
        Transform = [1, 0, 1, 0],
        Bounds = new WorldBox(-500, -500, 500, 500),
        Layers =
        [
            new MapLayer("First floor", "First_Floor", null, false, []),
            new MapLayer("Basement", "Basement", null, false, []),
        ],
    };

    private readonly string _svg = Path.Combine(Path.GetTempPath(), $"shturmap-floors-{Guid.NewGuid():N}.svg");

    public FloorReadTests() => File.WriteAllText(_svg,
        """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 1000">
          <g id="Ground"><rect width="1000" height="1000" fill="#808080"/></g>
          <g id="First_Floor"><rect x="100" y="100" width="200" height="200" fill="#a0a0a0"/></g>
          <g id="Basement"><rect x="600" y="600" width="200" height="200" fill="#606060"/></g>
        </svg>
        """);

    public void Dispose() => File.Delete(_svg);

    [Fact]
    public void Without_reading_ahead_a_floor_is_read_when_it_is_asked_for()
    {
        using var artwork = MapArtwork.Load(_svg, Map);
        Assert.NotNull(artwork.Layer("First_Floor"));
        Assert.NotNull(artwork.Layer("Basement"));
        // The ground is part of the base picture, and a floor the artwork doesn't have is none.
        Assert.Null(artwork.Layer("Ground"));
        Assert.Null(artwork.Layer("Attic"));
        Assert.Null(artwork.Layer(null));
    }

    [Fact]
    public async Task Read_ahead_a_paint_never_waits_for_a_floor()
    {
        using var artwork = MapArtwork.Load(_svg, Map);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var read = 0;
        artwork.FloorRead += () => Interlocked.Increment(ref read);
        // The first floor's read is held back, as a slow SVG would hold it.
        artwork.BeforeFloorRead = _ =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        };

        var reading = artwork.ReadFloorsAsync();
        Assert.Same(reading, artwork.ReadFloorsAsync());
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));

        // A paint asks for a floor that isn't there yet: no picture, and no waiting.
        var asked = Task.Run(() => (artwork.Layer("First_Floor"), artwork.Layer("Basement")), TestContext.Current.CancellationToken);
        Assert.Same(asked, await Task.WhenAny(asked, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        Assert.Equal((null, null), await asked);
        Assert.Equal(0, read);

        release.Set();
        await reading.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.NotNull(artwork.Layer("First_Floor"));
        Assert.NotNull(artwork.Layer("Basement"));
        Assert.Equal(2, read);
    }

    [Fact]
    public async Task A_floor_that_cannot_be_read_is_left_out_and_the_others_are_read()
    {
        using var artwork = MapArtwork.Load(_svg, Map);
        var failed = new List<string>();
        artwork.FloorFailed += (id, _) => failed.Add(id);
        artwork.BeforeFloorRead = id =>
        {
            if (id == "First_Floor")
                throw new FormatException("This floor can't be drawn.");
        };

        await artwork.ReadFloorsAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(["First_Floor"], failed);
        Assert.Null(artwork.Layer("First_Floor"));
        Assert.NotNull(artwork.Layer("Basement"));
    }
}
