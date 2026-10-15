using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shturmap.Core.Logs;

namespace Shturmap.Session;

/// <summary>A note the player wrote on a quest's card (owner, 2026-10-15).</summary>
/// <param name="Id">A short random id, by which it is deleted.</param>
/// <param name="At">When it was written, with the PC's offset then, to the second. Notes aren't edited, so it is always
/// when these words were written.</param>
/// <param name="Text">The words: trimmed, at most <see cref="QuestNotes.MaxLength"/> characters, line breaks kept (as
/// "\n").</param>
public sealed record QuestNote(string Id, DateTimeOffset At, string Text);

/// <summary>
/// The player's notes on quests (owner, 2026-10-15), kept in <c>notes.json</c> in the data folder: one set per quest
/// and game mode (PvE and PvP apart, as ticks and picks are), each note with when it was written. Add and delete only,
/// no editing: the time always says when the words were written. A note can be added any time, also in a raid, but
/// nothing ever needs one. They are the player's own words, so they never go into a report, the diagnostics, the app
/// log or the study log (which counts them). The file's shape is docs/DESIGN.md §8, "The notes file".
/// </summary>
public sealed class QuestNotes
{
    /// <summary>The file's name in the data folder.</summary>
    public const string FileName = "notes.json";

    public const int Version = 1;

    /// <summary>The most characters a note keeps; a longer text is cut (owner, 2026-10-15).</summary>
    public const int MaxLength = 500;

    private readonly string _path;
    // In the order they were written: the file keeps that order, and of two notes stamped in the same second the
    // later one is listed first.
    private readonly List<Entry> _notes = [];
    // Each mode's notes by quest, newest first, made when first asked for after a change. Never changed once made, so
    // a snapshot can hand one to the UI thread while the session changes the notes.
    private readonly Dictionary<GameMode, IReadOnlyDictionary<string, IReadOnlyList<QuestNote>>> _views = [];
    // False when the file is there but couldn't be read for now, or couldn't be set aside: the notes in it are the
    // player's, so nothing is written over it this run.
    private bool _keep = true;

    // Kept false for a note that must not outlive the session (a developer script's): shown, never written.
    private sealed record Entry(GameMode Mode, string QuestId, QuestNote Note, bool Kept = true);

    private QuestNotes(string path) => _path = path;

    /// <summary>A quest's notes in a mode, newest first; empty when it has none.</summary>
    public IReadOnlyList<QuestNote> For(GameMode mode, string questId) => Of(mode).GetValueOrDefault(questId) ?? [];

    /// <summary>A mode's notes by quest id, each quest's newest first; the same object until the notes change.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<QuestNote>> Of(GameMode mode)
    {
        if (_views.TryGetValue(mode, out var view))
            return view;
        // Newest first by when they were written; the order is stable, so of two in the same second the later added
        // (first after the reversal) stays first.
        view = Enumerable.Reverse(_notes)
            .Where(n => n.Mode == mode)
            .GroupBy(n => n.QuestId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<QuestNote>)g.Select(n => n.Note).OrderByDescending(n => n.At).ToList(), StringComparer.Ordinal);
        return _views[mode] = view;
    }

    /// <summary>
    /// Adds a note to a quest: the text trimmed, line breaks kept, cut at <see cref="MaxLength"/> characters; the time
    /// to the second. Changes the notes in memory only: <see cref="Save"/> writes them.
    /// </summary>
    /// <param name="save">False for a note that must not outlive the session (a developer script's, for a snapshot):
    /// it is listed like any other, and <see cref="Save"/> leaves it out.</param>
    /// <exception cref="ArgumentException">The text is empty or only white space (nothing to keep), or the quest id is.</exception>
    public QuestNote Add(GameMode mode, string questId, string text, DateTimeOffset at, bool save = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questId);
        var words = Clean(text ?? "");
        if (words.Length == 0)
            throw new ArgumentException("A note needs words.", nameof(text));
        var note = new QuestNote(NewId(), at.AddTicks(-(at.Ticks % TimeSpan.TicksPerSecond)), words);
        _notes.Add(new Entry(mode, questId, note, save));
        _views.Remove(mode);
        return note;
    }

    /// <summary>Deletes a note, in whichever mode it is; false when there is none with the id. <see cref="Save"/> writes it.</summary>
    public bool Delete(string noteId)
    {
        var index = _notes.FindIndex(n => n.Note.Id == noteId);
        if (index < 0)
            return false;
        _views.Remove(_notes[index].Mode);
        _notes.RemoveAt(index);
        return true;
    }

    /// <summary>The quest a note is on, or null when there is none with the id (for the study log, which names the quest).</summary>
    public string? QuestOf(string noteId) => _notes.Find(n => n.Note.Id == noteId)?.QuestId;

    // Trimmed, one kind of line break (WinUI's text box gives "\r"), and cut on a whole character: never between the two
    // halves of a surrogate pair (an emoji).
    private static string Clean(string text)
    {
        var words = text.ReplaceLineEndings("\n").Trim();
        if (words.Length <= MaxLength)
            return words;
        return words[..(char.IsHighSurrogate(words[MaxLength - 1]) ? MaxLength - 1 : MaxLength)].TrimEnd();
    }

    // Eight hex digits, as a report's id; drawn again in the rare case it is taken.
    private string NewId()
    {
        while (true)
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            if (!_notes.Exists(n => n.Note.Id == id))
                return id;
        }
    }

    // ---- the file ----

    /// <summary>
    /// The notes in a file, or none when there is no file. A file that can't be read (not JSON, not the shape) is set
    /// aside with the time in its name (<c>notes.json.unreadable-20260101-1540</c>), never over an earlier one, and the
    /// notes start empty: unlike the stash's marks, nothing can bring them back, so a copy stays for the player. A file
    /// that is only out of reach for now (another program holds it) is left as it is, and nothing is written over it
    /// this run. Either is said through <paramref name="warn"/>, without a word of the notes.
    /// </summary>
    public static QuestNotes Load(string path, Action<string>? warn = null)
    {
        var notes = new QuestNotes(path);
        if (!File.Exists(path))
            return notes;
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"The notes file couldn't be opened ({e.GetType().Name}); notes aren't kept this time");
            notes._keep = false;
            return notes;
        }
        NotesFile? model;
        try
        {
            model = JsonSerializer.Deserialize(json, Json.NotesFile);
        }
        catch (JsonException e)
        {
            try
            {
                var aside = SetAside(path, DateTime.Now);
                warn?.Invoke($"The notes file couldn't be read ({e.GetType().Name}): set aside as {Path.GetFileName(aside)}, and the notes start over");
            }
            catch (Exception moving) when (moving is IOException or UnauthorizedAccessException)
            {
                warn?.Invoke($"The notes file couldn't be read ({e.GetType().Name}) nor set aside ({moving.GetType().Name}); notes aren't kept this time");
                notes._keep = false;
            }
            return notes;
        }
        var skipped = 0;
        foreach (var n in model?.Notes ?? [])
        {
            // A note without its quest, its mode, its time or its words can't be shown, and one whose id is taken
            // couldn't be deleted alone. The file is Shturmap's own: only a hand edit makes such a note.
            if (n is not { Id.Length: > 0, Quest.Length: > 0, At: { } at } || string.IsNullOrWhiteSpace(n.Text) || ModeOf(n.Mode) is not { } mode
                || notes._notes.Exists(e => e.Note.Id == n.Id))
            {
                skipped++;
                continue;
            }
            notes._notes.Add(new Entry(mode, n.Quest, new QuestNote(n.Id, at, n.Text)));
        }
        if (skipped > 0)
            warn?.Invoke($"The notes file has {skipped} notes that couldn't be read; they are left out");
        return notes;
    }

    /// <summary>
    /// Writes the notes: a temporary file, then one move, so a crash leaves the old file whole. Nothing when the file
    /// couldn't be read for now at <see cref="Load"/>: its notes stay as they are.
    /// </summary>
    /// <exception cref="IOException">The file can't be written; the notes stay in memory and go with the next write.</exception>
    /// <exception cref="UnauthorizedAccessException">The same.</exception>
    public void Save()
    {
        if (!_keep)
            return;
        var model = new NotesFile(Version, _notes.Where(n => n.Kept)
            .Select(n => new NoteFile(n.Note.Id, ModeName(n.Mode), n.QuestId, n.Note.At, n.Note.Text)).ToList());
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(model, Json.NotesFile));
        File.Move(temp, _path, overwrite: true);
    }

    // Moves the file to its name with ".unreadable-yyyyMMdd-HHmm", and a number when that is taken (as the settings
    // file is set aside; SettingsFile.SetAside).
    private static string SetAside(string path, DateTime now)
    {
        var stamp = ".unreadable-" + now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var aside = path + stamp;
        for (var n = 2; File.Exists(aside); n++)
            aside = $"{path}{stamp}-{n}";
        File.Move(path, aside);
        return aside;
    }

    // "pve", "pvp": the mode's name in lower case.
    private static string ModeName(GameMode mode) => mode.ToString().ToLowerInvariant();

    private static GameMode? ModeOf(string? name) =>
        Enum.GetValues<GameMode>().Where(m => ModeName(m) == name).Select(m => (GameMode?)m).FirstOrDefault();

    // The attribute's options, and the words as written, also in German or Russian: the file is the player's to read
    // and is never put into a web page, so only what JSON itself needs is escaped.
    private static readonly NotesJson Json = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });
}

// The file's shape (docs/DESIGN.md §8, "The notes file"): each note with its id, its mode, its quest's tarkov.dev id,
// when it was written (ISO 8601 with the offset) and its words. Read leniently: a note missing a part is left out.
internal sealed record NotesFile(int Version, List<NoteFile>? Notes);

internal sealed record NoteFile(string? Id, string? Mode, string? Quest, DateTimeOffset? At, string? Text);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(NotesFile))]
internal sealed partial class NotesJson : JsonSerializerContext;
