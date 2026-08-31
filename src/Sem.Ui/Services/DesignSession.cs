using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// What the designer is currently working on: the loaded file, the empire being edited, and
/// everything the rules say about it.
/// </summary>
/// <remarks>
/// Every change goes through <see cref="Edit"/>, which applies it and then works the rules out
/// again from scratch. Recomputing rather than patching means the displayed budgets and blocked
/// options cannot drift away from the design they describe.
/// </remarks>
public sealed class DesignSession
{
    private readonly List<string> _ownedDlc = [];

    /// <summary>
    /// The empire being edited, as it stood when it was opened or last saved.
    /// </summary>
    /// <remarks>
    /// One empire, not the file. Editing an empire is the thing that has a Save button in front of
    /// it, and the thing whose changes are worth warning somebody about; adding an empire to the
    /// file or removing one from it is a list being managed, and those keep themselves.
    /// </remarks>
    private EmpireSnapshot? _saved;

    /// <summary>
    /// An empire created but not yet saved, which is in the file only because it has to live
    /// somewhere.
    /// </summary>
    /// <remarks>
    /// Pressing Create used to add an empire to the file there and then, and store it: going to the
    /// designer, looking at what a new empire starts as and going back left "New Empire" behind for
    /// good. So a new one is held here instead. It is in the file because the designer, the rules
    /// and the preview all work on an empire that is in one — but it is not stored until it is
    /// saved, and abandoning it takes it back out.
    /// </remarks>
    private EmpireDesign? _unsaved;

    /// <param name="data">The extracted game data.</param>
    /// <param name="assumeAllPacks">
    /// Whether to open with every content pack enabled rather than only those installed where the
    /// data was read. True on the web, where the installation the data came from is mine and not the
    /// player's; false on the desktop, where it is theirs.
    /// </param>
    public DesignSession(GameData data, bool assumeAllPacks = false)
    {
        ArgumentNullException.ThrowIfNull(data);

        Data = data;
        Localizer = new Localizer(
            data.Localisation,
            data.Database.TextIcons,
            data.AssetUrl,
            data.Database.ScriptedValues,
            data.Database.ScriptedText);
        Reasons = new ReasonWriter(Localizer);
        Rules = new EmpireRules(data.Database);
        Modifiers = new ModifierFormatter(Localizer, data.Database);
        Conditions = new ConditionWriter(Localizer);
        Names = new NameGenerator(data.Database);

        _ownedDlc.AddRange(data.Database.Dlc
            .Where(d => assumeAllPacks || d.Installed)
            .Select(d => d.Name));

        RebuildOwned();
    }

    /// <summary>Raised whenever anything an interface displays has changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised when the file gains or loses an empire, or a different one is opened.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Changed"/> because it means something different: this is the list
    /// itself changing, which is kept as it happens, while an edit to one empire waits to be saved.
    /// </remarks>
    public event Action? FileChanged;

    /// <summary>The extracted game data.</summary>
    public GameData Data { get; }

    /// <summary>Display text for the game's keys.</summary>
    public Localizer Localizer { get; }

    /// <summary>Turns the reasons the rules give into sentences.</summary>
    public ReasonWriter Reasons { get; }

    /// <summary>Turns modifiers into the lines the game would show for them.</summary>
    public ModifierFormatter Modifiers { get; }

    /// <summary>Says in words when a conditional modifier applies.</summary>
    public ConditionWriter Conditions { get; }

    /// <summary>Invents names the way the game's randomise buttons do.</summary>
    public NameGenerator Names { get; }

    /// <summary>The rules being enforced.</summary>
    public EmpireRules Rules { get; }

    /// <summary>The loaded designs file, or null before one is opened.</summary>
    public EmpireDesignsFile? File { get; private set; }

    /// <summary>The name the file was opened from, for showing and for saving back.</summary>
    public string? FileName { get; private set; }

    /// <summary>The empire being edited, or null when none is selected.</summary>
    public EmpireDesign? Current { get; private set; }

    /// <summary>What the rules make of the current empire.</summary>
    public DesignContext? Context { get; private set; }

    /// <summary>What is wrong with the current empire, if anything.</summary>
    public ValidationReport Report { get; private set; } = new([]);

    /// <summary>Whether the empire being edited has changes that have not been saved.</summary>
    public bool IsModified { get; private set; }

    /// <summary>
    /// Whether the list of empires has changed since the file was last written.
    /// </summary>
    /// <remarks>
    /// Only the desktop has anything to do with this. A browser stores the file as soon as the list
    /// changes, so there is never anything outstanding; the desktop's one copy is the player's own
    /// file, which is not written behind their back, so it can be out of date and should say so.
    /// </remarks>
    public bool HasUnwrittenFileChanges { get; private set; }

    /// <summary>
    /// The content packs to judge availability against.
    /// </summary>
    /// <remarks>
    /// Held rather than built on each read. This is asked once per pack while the bar draws, once
    /// per empire while the list draws, and again by every context the rules build — dozens of sets
    /// allocated per frame, in a runtime where that is not free, for something that changes only
    /// when a switch is clicked.
    /// </remarks>
    public IReadOnlySet<string> OwnedDlc => _owned;

    private HashSet<string> _owned = new(StringComparer.Ordinal);

    private void RebuildOwned() => _owned = [.. _ownedDlc];

    /// <summary>Opens a designs file.</summary>
    public void Load(byte[] contents, string fileName)
    {
        ArgumentNullException.ThrowIfNull(contents);

        File = EmpireDesignsFile.Load(contents);
        FileName = fileName;
        HasUnwrittenFileChanges = false;
        Select(File.Designs.FirstOrDefault());
        FileChanged?.Invoke();
    }

    /// <summary>Opens a designs file already in hand as text.</summary>
    public void LoadText(string contents, string fileName)
    {
        ArgumentNullException.ThrowIfNull(contents);

        File = EmpireDesignsFile.LoadText(contents);
        FileName = fileName;
        HasUnwrittenFileChanges = false;
        Select(File.Designs.FirstOrDefault());
        FileChanged?.Invoke();
    }

    /// <summary>Starts an empty file, for someone who has none yet.</summary>
    public void StartEmptyFile()
    {
        File = EmpireDesignsFile.CreateEmpty();
        FileName = EmpireDesignsFile.FileName;
        HasUnwrittenFileChanges = false;
        Select(null);
        FileChanged?.Invoke();
    }

    /// <summary>
    /// Chooses which empire to edit, taking the point a revert would come back to.
    /// </summary>
    /// <remarks>
    /// Choosing a different empire abandons the copy held of the last one. Nothing is lost by that:
    /// the only way to leave the designer is past a question about unsaved work.
    /// </remarks>
    public void Select(EmpireDesign? design)
    {
        // Turning to a different empire abandons one that was never saved, so it goes back out of
        // the file. This is also what deleting one does, and what reverting one does.
        if (_unsaved is { } pending && !ReferenceEquals(pending, design))
        {
            File?.Remove(pending);
            _unsaved = null;
        }

        Current = design;
        _saved = design?.Snapshot();
        _savedContext = null;
        IsModified = false;
        Recompute();
    }

    /// <summary>
    /// Starts an empire that is not in the file until it is saved.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="EditFile"/> this announces nothing, so the host does not store it. It
    /// counts as unsaved from the first moment, which is what makes leaving the designer ask about
    /// it exactly as it would for an empire somebody had edited.
    /// </remarks>
    public EmpireDesign? CreateEmpire(Func<EmpireDesignsFile, EmpireDesign> add)
    {
        ArgumentNullException.ThrowIfNull(add);

        if (File is null)
        {
            StartEmptyFile();
        }

        var design = add(File!);

        Select(design);

        _unsaved = design;
        IsModified = true;
        Recompute();

        return design;
    }

    /// <summary>
    /// Applies a change to the current empire and works the rules out again.
    /// </summary>
    public void Edit(Action<EmpireDesign> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (Current is null)
        {
            return;
        }

        change(Current);
        IsModified = true;
        Recompute();
    }

    /// <summary>
    /// Applies a change to the file itself, such as adding or removing an empire.
    /// </summary>
    /// <remarks>
    /// Not an edit to the empire being designed, so it does not go behind the Save button — the list
    /// of empires is managed directly, and a deletion the player confirmed is one they meant. It is
    /// announced separately so the host can keep the file.
    /// </remarks>
    public void EditFile(Action<EmpireDesignsFile> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (File is null)
        {
            return;
        }

        change(File);
        HasUnwrittenFileChanges = true;
        Recompute();
        FileChanged?.Invoke();
    }

    /// <summary>Sets which content packs are available.</summary>
    public void SetOwnedDlc(IEnumerable<string> owned)
    {
        ArgumentNullException.ThrowIfNull(owned);

        _ownedDlc.Clear();
        _ownedDlc.AddRange(owned);
        RebuildOwned();

        // The saved empire is read against the packs as well, so it is no longer the same reading.
        _savedContext = null;
        Recompute();
    }

    /// <summary>Turns one content pack on or off.</summary>
    public void ToggleDlc(string name, bool owned)
    {
        if (owned)
        {
            if (!_ownedDlc.Contains(name, StringComparer.Ordinal))
            {
                _ownedDlc.Add(name);
            }
        }
        else
        {
            _ownedDlc.RemoveAll(d => string.Equals(d, name, StringComparison.Ordinal));
        }

        RebuildOwned();
        _savedContext = null;
        Recompute();
    }

    /// <summary>
    /// Writes the file back out. An empire nobody touched comes out byte for byte as it went in.
    /// </summary>
    public byte[] Save() => File?.Save() ?? [];

    /// <summary>
    /// The empire as it stood when it was last saved, read against the rules.
    /// </summary>
    /// <remarks>
    /// Null where there is nothing to compare with: an empire created and never saved was never
    /// anything else. Held rather than rebuilt, since the copy only changes when the empire is
    /// saved and building a context walks every government the game defines.
    /// </remarks>
    public DesignContext? SavedContext
    {
        get
        {
            if (_saved is not { } stored)
            {
                return null;
            }

            return _savedContext ??= Rules.CreateContext(stored.ToDesign(), OwnedDlc);
        }
    }

    private DesignContext? _savedContext;

    /// <summary>
    /// Records that the empire being edited is now the empire that is stored.
    /// </summary>
    /// <remarks>
    /// Called by whoever did the storing, since only they know whether it worked. The copy is taken
    /// here rather than being passed in, so what is remembered is the state of the design and not
    /// somebody's account of it.
    /// </remarks>
    public void MarkSaved()
    {
        // Whatever was written is in the file now, including an empire that had only been created.
        _unsaved = null;
        _saved = Current?.Snapshot();
        _savedContext = null;
        IsModified = false;

        // Saving writes the whole file, so whatever the list was owed is settled too.
        HasUnwrittenFileChanges = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// Puts the empire being edited back to how it was when it was opened or last saved.
    /// </summary>
    public void Revert()
    {
        // An empire that was never saved has nothing to go back to: it was not there before, so
        // putting it back means taking it out. Selecting anything else does that.
        if (_unsaved is { } pending && ReferenceEquals(pending, Current))
        {
            Select(File?.Designs.FirstOrDefault(d => !ReferenceEquals(d, pending)));
            return;
        }

        if (_saved is not { } stored || Current is not { } design)
        {
            return;
        }

        design.Restore(stored);
        IsModified = false;
        Recompute();
    }

    /// <summary>
    /// What the rules make of one of the empire's species, which is not always the founders.
    /// </summary>
    /// <remarks>
    /// An origin may call for a second, and every control that edits a species — its traits, its
    /// class, its portrait, its names — was judging whichever it was given by the founders' context.
    /// The visible symptom was the trait budget: it counted the founders' traits, so adding to the
    /// second species moved no counter and exceeded no limit.
    /// </remarks>
    public DesignContext? ContextFor(SpeciesDesign? species)
    {
        if (Context is not { } context || species is null)
        {
            return Context;
        }

        return ReferenceEquals(species, Current?.Species)
            ? context
            : context.ForSpecies(species, secondary: true);
    }

    /// <summary>The report for any empire in the file, for showing status in a list.</summary>
    public ValidationReport Validate(EmpireDesign design) => Rules.Validate(design, OwnedDlc);

    private void Recompute()
    {
        if (Current is null)
        {
            Context = null;
            Report = new ValidationReport([]);
        }
        else
        {
            Context = Rules.CreateContext(Current, OwnedDlc);
            Report = Rules.Validate(Context, Current);
        }

        Changed?.Invoke();
    }
}
