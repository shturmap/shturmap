namespace Shturmap.App.Controls;

/// <summary>What a card is about: a quest or an item.</summary>
public abstract record CardKey
{
    public sealed record Quest(string Id) : CardKey
    {
        public override string ToString() => "quest:" + Id;
    }

    public sealed record Item(string Id) : CardKey
    {
        public override string ToString() => "item:" + Id;
    }
}

public enum CardMode
{
    /// <summary>Shown while the pointer is on its subject or on the card; goes away with it. Semi-transparent.</summary>
    Hover,

    /// <summary>Clicked: stays until a click elsewhere, Esc, or another click on its subject.</summary>
    Held,

    /// <summary>Its own window, open until closed.</summary>
    Pinned,
}

/// <summary>A card a <see cref="CardStack"/> can show: the quest card or the item card.</summary>
public interface ICard
{
    CardKey Key { get; }

    /// <summary>The quest's or item's name (for the study log).</summary>
    string Title { get; }

    CardMode Mode { get; }

    void SetMode(CardMode mode);
}

internal static class CardLook
{
    /// <summary>An unheld card is see-through enough not to hide the map behind it.</summary>
    public const double HoverOpacity = 0.8;
}
