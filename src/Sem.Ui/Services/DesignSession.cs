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
            data.Database.ScriptedValues);
        Reasons = new ReasonWriter(Localizer);
        Rules = new EmpireRules(data.Database);
        Modifiers = new ModifierFormatter(Localizer, data.Database);
        Conditions = new ConditionWriter(Localizer);
        Names = new NameGenerator(data.Database);

        _ownedDlc.AddRange(data.Database.Dlc
            .Where(d => assumeAllPacks || d.Installed)
            .Select(d => d.Name));
    }

    /// <summary>Raised whenever anything an interface displays has changed.</summary>
    public event Action? Changed;

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

    /// <summary>Whether anything has been changed since the file was opened.</summary>
    public bool IsModified { get; private set; }

    /// <summary>The content packs to judge availability against.</summary>
    public IReadOnlySet<string> OwnedDlc => _ownedDlc.ToHashSet(StringComparer.Ordinal);

    /// <summary>Opens a designs file.</summary>
    public void Load(byte[] contents, string fileName)
    {
        ArgumentNullException.ThrowIfNull(contents);

        File = EmpireDesignsFile.Load(contents);
        FileName = fileName;
        IsModified = false;
        Select(File.Designs.FirstOrDefault());
    }

    /// <summary>Opens a designs file already in hand as text.</summary>
    public void LoadText(string contents, string fileName)
    {
        ArgumentNullException.ThrowIfNull(contents);

        File = EmpireDesignsFile.LoadText(contents);
        FileName = fileName;
        IsModified = false;
        Select(File.Designs.FirstOrDefault());
    }

    /// <summary>Starts an empty file, for someone who has none yet.</summary>
    public void StartEmptyFile()
    {
        File = EmpireDesignsFile.CreateEmpty();
        FileName = EmpireDesignsFile.FileName;
        IsModified = false;
        Select(null);
    }

    /// <summary>Chooses which empire to edit.</summary>
    public void Select(EmpireDesign? design)
    {
        Current = design;
        Recompute();
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

    /// <summary>Applies a change to the file itself, such as adding or removing an empire.</summary>
    public void EditFile(Action<EmpireDesignsFile> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (File is null)
        {
            return;
        }

        change(File);
        IsModified = true;
        Recompute();
    }

    /// <summary>Sets which content packs are available.</summary>
    public void SetOwnedDlc(IEnumerable<string> owned)
    {
        ArgumentNullException.ThrowIfNull(owned);

        _ownedDlc.Clear();
        _ownedDlc.AddRange(owned);
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

        Recompute();
    }

    /// <summary>
    /// Writes the file back out. An empire nobody touched comes out byte for byte as it went in.
    /// </summary>
    public byte[] Save() => File?.Save() ?? [];

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
