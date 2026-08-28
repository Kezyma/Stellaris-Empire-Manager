using Sem.Clausewitz;

namespace Sem.Designs;

/// <summary>
/// One file from the game's <c>prescripted_countries/</c> folder, holding the built-in empires.
/// </summary>
/// <remarks>
/// Read-only. These belong to the game install, which this project never writes to. Turning one
/// into an editable empire happens by copying it into the player's designs file with
/// <see cref="EmpireDesignsFile.AddFromPrescripted"/>.
/// </remarks>
public sealed class PrescriptedCountriesFile
{
    private PrescriptedCountriesFile(CwDocument document, IReadOnlyList<PrescriptedEmpire> empires)
    {
        Document = document;
        Empires = empires;
    }

    /// <summary>The parsed file.</summary>
    public CwDocument Document { get; }

    /// <summary>The empires the file defines, in order.</summary>
    public IReadOnlyList<PrescriptedEmpire> Empires { get; }

    /// <summary>Reads a prescripted countries file. Game content is parsed leniently.</summary>
    public static PrescriptedCountriesFile Load(ReadOnlySpan<byte> bytes)
    {
        var document = CwDocument.Parse(bytes, CwParseOptions.Lenient);

        var empires = document.Nodes
            .Where(n => n.IsAssignment && n.Block is not null)
            .Select(n => new PrescriptedEmpire(n))
            .ToList();

        return new PrescriptedCountriesFile(document, empires);
    }
}
