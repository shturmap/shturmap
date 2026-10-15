using System.Collections;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core.Planning;
using Shturmap.Map;
using Shturmap.Session;
using Windows.UI.Core;

namespace Shturmap.App.Controls;

/// <summary>A note as its card lists it: when it was written ("1 Jan 15:40"), its words, and its × on a held card.</summary>
public sealed record NoteRow(string Id, string Stamp, string Text, Visibility Delete);

/// <summary>The quest card (docs/DESIGN.md §4, "Quest cards"): the same card on hover, held and nested.</summary>
public sealed partial class QuestCard : UserControl, ICard
{
    public QuestCard(QuestCardView view)
    {
        InitializeComponent();
        // No longer than the session keeps: it would cut the rest (QuestNotes).
        NoteField.MaxLength = QuestNotes.MaxLength;
        Key = new CardKey.Quest(view.QuestId);
        Show(view);
        SetMode(CardMode.Hover);
    }

    /// <summary>A note was written in the card's field: the quest and the words, for the session to add.</summary>
    public event Action<string, string>? NoteAdded;

    /// <summary>A note's × was clicked: its id, for the session to delete.</summary>
    public event Action<string>? NoteDeleted;

    public CardKey Key { get; }

    public string Title => View.Name;

    public QuestCardView View { get; private set; } = null!;

    public CardMode Mode { get; private set; } = CardMode.Hover;

    public void Show(QuestCardView view)
    {
        View = view;
        Bindings.Update();
        ShowNotes();
    }

    public void SetMode(CardMode mode)
    {
        Mode = mode;
        Opacity = mode == CardMode.Hover ? CardLook.HoverOpacity : 1;
        Frame.BorderBrush = (Brush)Application.Current.Resources[mode == CardMode.Held ? "AmberBrush" : "LineStrongBrush"];
        ShowNotes();
    }

    private void OnWikiClick(object sender, RoutedEventArgs e) => Study.Ui("wiki.open", ("quest", View.QuestId), ("name", View.Name));

    // ---- notes (owner, 2026-10-15; docs/DESIGN.md §4, "Quest cards", Notes) ----

    private IReadOnlyList<QuestNote>? _shownNotes;
    private bool _shownHeld;

    // NOTES: the list on any card with notes; the field and the × only on a held card, so a hover card without notes
    // has no section. The rows are made again only when the notes or the mode change (the session hands the same list
    // until a note is added or deleted), not at every snapshot under the pointer. The field is never made again: what
    // is typed in it, and its focus, stay while the card follows the session.
    private void ShowNotes()
    {
        var notes = View.Notes;
        var held = Mode == CardMode.Held;
        NotesSection.Visibility = held || notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoteField.Visibility = held ? Visibility.Visible : Visibility.Collapsed;
        if (_shownNotes is not null && held == _shownHeld && (ReferenceEquals(notes, _shownNotes) || notes.Count + _shownNotes.Count == 0))
            return;
        var added = _shownNotes is not null && notes.Count > _shownNotes.Count;
        _shownNotes = notes;
        _shownHeld = held;
        NoteRows.ItemsSource = notes
            .Select(n => new NoteRow(n.Id, QuestCards.NoteStamp(n.At), n.Text, held ? Visibility.Visible : Visibility.Collapsed))
            .ToList();
        // The new note pushes the field down: in a card that scrolls, it is brought back into view.
        if (added && NoteField.FocusState != FocusState.Unfocused)
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => NoteField.StartBringIntoView());
    }

    // Enter adds the note and empties the field, Shift+Enter breaks the line (the box takes it), Esc clears the field.
    // Before the box takes the key; the window's shortcuts leave every key to a box with the focus (MainWindow.TypingText).
    private void OnNoteKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            NoteField.Text = "";
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Enter
                 && !InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            // Only words make a note (the session would refuse the rest too).
            if (string.IsNullOrWhiteSpace(NoteField.Text))
                return;
            NoteAdded?.Invoke(View.QuestId, NoteField.Text);
            NoteField.Text = "";
        }
    }

    private void OnNoteDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NoteRow row })
            NoteDeleted?.Invoke(row.Id);
    }

    // ---- x:Bind helpers ----

    public static Visibility ShownIf(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>A hand-over mark's tooltip: "Hand over ×3 to Therapist after the raid"; empty without one.</summary>
    public static string HandoverTip(string handover) => handover.Length > 0 ? AppTexts.HandoverAfterRaid(handover: handover) : "";

    public static Visibility ShownIfTrue(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    // A BRING row's mark just arrived: its gold overlay fades to ink, the row flashes (Controls.Glow).
    private void OnGlowLoaded(object sender, RoutedEventArgs e) => Glow.OnLoaded(sender, e);

    /// <summary>An objective's text: muted once it is ticked as done.</summary>
    public static Brush TextBrush(bool done) => (Brush)Application.Current.Resources[done ? "MutedBrush" : "InkBrush"];

    /// <summary>An objective's type glyph: muted once it is ticked as done (grey means done, on the map too).</summary>
    public static Brush GlyphBrush(bool done) => (Brush)Application.Current.Resources[done ? "MutedBrush" : "AmberBrush"];

    /// <summary>
    /// The state line's colour: the accent while the quest is active, muted for every other state. It was green,
    /// which on the map and in the rail is a PMC extract (one colour, one meaning; the review of 2026-10-04, B2).
    /// </summary>
    public Brush StatusBrush(Shturmap.Core.Quests.QuestState state) =>
        (Brush)Application.Current.Resources[state == Shturmap.Core.Quests.QuestState.Active ? "AmberBrush" : "MutedBrush"];

    public Visibility Shown(string? text) => ShownIf(text);

    public Visibility ShownIfAny(IEnumerable? items) => items?.GetEnumerator().MoveNext() == true ? Visibility.Visible : Visibility.Collapsed;

    public static string NeedGlyph(RequirementKind kind) => kind == RequirementKind.Key ? Glyphs.Key : Glyphs.Bring;

    // The wiki link comes out of tarkov.dev's data: only an https address on the wiki is one (Rules.OutsideLink).
    public Uri? WikiUri(string? link) => Rules.OutsideLink.Wiki(link);

    public Visibility ShownIfWiki(string? link) => Rules.OutsideLink.Wiki(link) is null ? Visibility.Collapsed : Visibility.Visible;
}
