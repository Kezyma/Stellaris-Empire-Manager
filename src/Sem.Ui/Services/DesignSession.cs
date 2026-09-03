using Sem.Clausewitz;
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
    /// <param name="preferences">
    /// What the player has settled on about how the designer is arranged. Optional, because a
    /// session with nowhere to remember them still works — it simply opens with the defaults.
    /// </param>
    public DesignSession(GameData data, bool assumeAllPacks = false, Preferences? preferences = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        Data = data;
        Preferences = preferences ?? new Preferences();
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

    /// <summary>What the player has settled on about how the designer is arranged.</summary>
    public Preferences Preferences { get; }

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
    /// <remarks>
    /// A change that changed nothing is not an edit, and saying otherwise cost the player something
    /// real: pressing the room an empire already had armed the Save button and made leaving the
    /// designer ask whether to save an empire nobody had touched. Two pickers were worse than that -
    /// they decide whether to allow the change from inside this callback and return without making
    /// one, so a refusal marked the design dirty every time.
    ///
    /// Compared by writing the empire out both times rather than by asking each picker to check its
    /// own field first. There are a dozen such fields and the comparison has to be right for all of
    /// them; the written form is the whole design by definition, and at a kilobyte and a half the
    /// two writes are far below the cost of the render that follows.
    /// </remarks>
    public void Edit(Action<EmpireDesign> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (Current is not { } current)
        {
            return;
        }

        var before = Written(current);
        var shape = Shape(current);

        change(current);

        // Only when one of the five things that impose a trait has moved. A name being typed cannot
        // change what the empire forces on its founders, and working it out costs a whole context.
        if (!string.Equals(shape, Shape(current), StringComparison.Ordinal))
        {
            AddForcedTraits(current);
        }

        if (string.Equals(before, Written(current), StringComparison.Ordinal))
        {
            return;
        }

        IsModified = true;
        Recompute();
    }

    /// <summary>
    /// Renames the empire, refusing a name another empire in the file already has.
    /// </summary>
    /// <remarks>
    /// The file keys empires by name, so two of a name would make one of them unreachable. Rather
    /// than silently keeping the old key - which left the two disagreeing with no sign of it - the
    /// caller is told no and says so.
    ///
    /// Here rather than in the section that used to own it, because the lite editor names the empire
    /// too, and a second copy of this rule is a second answer to "is that name taken". How the
    /// display name is written is the caller's, though: a name picked from the generator is stored as
    /// the shape and the pieces the game would have stored, and only a typed one is stored as text.
    /// </remarks>
    /// <param name="name">The new name.</param>
    /// <param name="writeDisplayName">
    /// How to record it as the name shown. Omitted, it is written as text somebody typed.
    /// </param>
    /// <returns>False when the name is taken, in which case nothing was changed.</returns>
    public bool TryRename(string? name, Action<EmpireDesign>? writeDisplayName = null)
    {
        if (Current is null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (File?.Find(name) is { } taken && !ReferenceEquals(taken, Current))
        {
            return false;
        }

        Edit(design =>
        {
            if (writeDisplayName is not null)
            {
                writeDisplayName(design);
            }
            else
            {
                design.Name.SetLiteral(name);
            }

            design.Rename(name);
        });

        return true;
    }

    /// <summary>
    /// The choices that decide which traits the empire imposes on its founders.
    /// </summary>
    private static string Shape(EmpireDesign design) => string.Join(
        '|',
        design.Authority,
        design.Origin,
        design.PlanetClass,
        design.Species.Class,
        string.Join(',', design.Civics));

    /// <summary>
    /// Writes in the traits the empire's own choices impose, where the design lacks them.
    /// </summary>
    /// <remarks>
    /// The game forces these and, in its own words, verifies them "only for empire designs" - which
    /// is exactly what this app writes. They were computed for the picker, which showed them among
    /// the chosen traits and would not let them go, and never reached the file: an empire switched to
    /// Hive Mind displayed the trait and did not carry it.
    ///
    /// Added and never removed. A trait that has stopped being forced is left where it is, because
    /// this cannot tell one it put there from one that arrived with an imported design or from a
    /// mechanic this app does not model - and the picker will now let the player take it off, since
    /// nothing is forcing it any more.
    /// </remarks>
    private void AddForcedTraits(EmpireDesign design)
    {
        var forced = Rules.GetWrittenForcedTraits(Rules.CreateContext(design, OwnedDlc));
        var held = design.Species.Traits;

        if (forced.Where(t => !held.Contains(t)).ToList() is not { Count: > 0 } missing)
        {
            return;
        }

        design.Species.SetTraits([.. held, .. missing]);
    }

    /// <summary>
    /// The empire exactly as it would be written to the file, which is the only complete account of
    /// it - a design carries fields this app does not model, and they count as much as the rest.
    /// </summary>
    private static string Written(EmpireDesign design)
    {
        var document = new CwDocument();

        // Cloned, so that wrapping the block in a node to write it cannot reparent the live one.
        document.Add(CwNode.Assignment(design.Key, design.Block.Clone(), quoteKey: true));

        return document.ToText(CwWriteOptions.Compact);
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
    /// <para>
    /// An origin may call for a second, and it is judged by different rules from the founders'.
    /// This exists because every control that edits a species — its traits, its class, its portrait,
    /// its names — holds whichever species it was handed and has to ask which of the two it has;
    /// before there was anywhere to ask, they all used the empire's own context, and the trait
    /// budget counted the founders' traits however many the second species was given.
    /// </para>
    /// <para>
    /// Asked of the design, which answers from the second species rather than the founders. Two
    /// views are never the same object even when they are views of one block, so this cannot be a
    /// comparison of views; and it cannot reach for the founders' block either, because a design
    /// that has not got one would gain an empty one from being asked, on every render.
    /// </para>
    /// </remarks>
    public DesignContext? ContextFor(SpeciesDesign? species)
    {
        if (Context is not { } context || species is null)
        {
            return Context;
        }

        return Current is { } design && design.IsSecondary(species)
            ? context.ForSpecies(species, secondary: true)
            : context;
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
