namespace Spotter.App.Controls;

/// <summary>What a card is about: a quest or an item.</summary>
public abstract record CardKey
{
    public sealed record Quest(string Id) : CardKey;

    public sealed record Item(string Id) : CardKey;
}

public enum CardMode
{
    /// <summary>Shown while the pointer rests on its subject; goes away with it.</summary>
    Hover,

    /// <summary>The pointer rested long enough or moved in: the card stays while the pointer is on it.</summary>
    Held,

    /// <summary>Its own window, open until closed.</summary>
    Pinned,
}

/// <summary>A card a <see cref="CardStack"/> can show: the quest card or the item card.</summary>
public interface ICard
{
    CardKey Key { get; }

    CardMode Mode { get; }

    void SetMode(CardMode mode);

    /// <summary>Fills the hold bar over the given time, then holds the card.</summary>
    void StartHold(TimeSpan duration);

    void StopHold();

    event Action<ICard>? Held;
}
