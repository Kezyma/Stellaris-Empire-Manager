namespace Sem.Clausewitz;

/// <summary>How strict the parser should be.</summary>
public sealed record CwParseOptions
{
    /// <summary>
    /// Whether a block left open at the end of the file is accepted instead of raising an error.
    /// </summary>
    /// <remarks>
    /// Stellaris 4.4.6 ships one script file that genuinely ends without closing its last block
    /// (<c>common/scripted_loc/scripted_loc_ruloc.txt</c>), and the game loads it anyway. Reading
    /// game content therefore has to tolerate it, or a single defect in Paradox's data would stop
    /// extraction outright.
    /// </remarks>
    public bool AllowUnclosedBlocks { get; init; }

    /// <summary>
    /// Rejects malformed input. Used for the player's empire designs file, where a truncated block
    /// most likely means the file was damaged and quietly loading half of it would risk their
    /// empires.
    /// </summary>
    public static CwParseOptions Strict { get; } = new();

    /// <summary>
    /// Tolerates the defects Paradox's own content contains. Used when reading game files.
    /// </summary>
    public static CwParseOptions Lenient { get; } = new() { AllowUnclosedBlocks = true };
}
