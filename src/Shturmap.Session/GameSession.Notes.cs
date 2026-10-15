namespace Shturmap.Session;

// The player's notes on quests (owner, 2026-10-15): written on a quest's card, any time, also in a raid, but nothing
// ever needs them; each with when it was written; added and deleted, never edited. One set per quest and game mode,
// kept in notes.json in the data folder (QuestNotes). The player's own words: never in a report, the diagnostics, the
// app log or the study log, which counts them. docs/DESIGN.md §8, "The notes file".
public sealed partial class GameSession
{
    private QuestNotes? _notes;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<QuestNote>> NoNotes = new Dictionary<string, IReadOnlyList<QuestNote>>();

    private string NotesPath => Path.Combine(paths.Root, QuestNotes.FileName);

    // At open: the file from the last run, if any.
    private void OpenNotes() => _notes = QuestNotes.Load(NotesPath, warning => AppLog.Warn(warning));

    /// <summary>
    /// Adds a note to a quest in the mode Shturmap is in, stamped now. A text that is empty or only white space adds
    /// nothing (the card doesn't offer it); a longer one than <see cref="QuestNotes.MaxLength"/> characters is cut.
    /// </summary>
    /// <param name="save">False for a note for this session only (the developer script's <c>note</c>, as its <c>tick</c>):
    /// a script leaves no notes in the data folder.</param>
    public async Task AddQuestNoteAsync(string questId, string text, bool save = true)
    {
        await _gate.WaitAsync();
        try
        {
            if (_notes is null || string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(text))
                return;
            var note = _notes.Add(_mode, questId, text, new DateTimeOffset(Clock()), save);
            // How long, never what: the words are the player's.
            Study.Ui("note.add", ("quest", questId), ("length", note.Text.Length));
            if (save)
                SaveNotes();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Deletes a note; nothing when there is none with the id (deleted already).</summary>
    public async Task DeleteQuestNoteAsync(string noteId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_notes?.QuestOf(noteId) is not { } questId || !_notes.Delete(noteId))
                return;
            Study.Ui("note.delete", ("quest", questId));
            SaveNotes();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under the gate, after each change and before the snapshot that shows it, so that a snapshot is never ahead of the
    // file (as with the stash file). The whole file each time: notes are few, and change at a click.
    private void SaveNotes()
    {
        try
        {
            _notes?.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The change stays for this run, and goes with the next write that works.
            Failed("Saving the notes file", e);
        }
    }
}
