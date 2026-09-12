using Sem.Clausewitz;
using Sem.Designs;
using Sem.GameData;
using Sem.Rules;

namespace Sem.Ui.Services;

/// <summary>
/// What an edit implies for the rest of the design, written back into it.
/// </summary>
/// <remarks>
/// The game stores several things it also derives - the government an empire's choices add up to,
/// the traits a species class forces on its founders - and a design that carries the wrong one is
/// wrong in the file rather than merely on screen. These are the rules for keeping the written
/// answer and the derived one the same, and they run on the edits that could have changed it.
/// </remarks>
public sealed partial class DesignSession
{
    /// <summary>
    /// The choices the design works something out from, rather than holds.
    /// </summary>
    /// <remarks>
    /// Two things are worked out: the traits the empire imposes on its founders, and the name of its
    /// government. Between them they turn on everything here. The ethics are in the list for the
    /// government's sake alone - nothing forces a trait on account of an ethic - which costs a
    /// context on an ethic being changed that was not being paid before.
    /// </remarks>
    private static string Shape(EmpireDesign design) => string.Join(
        '|',
        design.Authority,
        design.Origin,
        design.PlanetClass,
        design.Species.Class,
        string.Join(',', design.Civics),
        string.Join(',', design.Ethics));

    /// <summary>
    /// Does the same for every empire in a file, and says whether it changed any of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer matters as much as the work: a file that gained a trait really has changed and the
    /// desktop should say "not yet written back" until it is saved, while a file that was already
    /// complete has not and should say nothing. Told apart by writing each design out either side of
    /// the call and comparing, which is how <see cref="Edit"/> avoids marking a design dirty for a
    /// change that was not one.
    /// </para>
    /// <para>
    /// A rules context per empire, which is the expensive part. The same order as the list itself,
    /// which builds one per row to say what each empire's government is and whether it is playable.
    /// </para>
    /// </remarks>
    private bool DeriveAll(EmpireDesignsFile file)
    {
        var changed = false;

        foreach (var design in file.Designs)
        {
            var before = Written(design);
            Derive(design);
            changed |= !string.Equals(before, Written(design), StringComparison.Ordinal);
        }

        return changed;
    }

    /// <summary>
    /// Writes back everything the design's own choices decide for it.
    /// </summary>
    /// <remarks>
    /// One context for both, since building one is the expensive part and it has already worked the
    /// government out by the time it is handed over.
    /// </remarks>
    private void Derive(EmpireDesign design)
    {
        var context = Rules.CreateContext(design, OwnedDlc);

        AddForcedTraits(design, context);
        WriteGovernment(design, context);
    }

    /// <summary>
    /// Writes down what the empire's government is called.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nobody chooses one: it is what an authority, some ethics and some civics add up to, and the
    /// game writes the answer into the design - which is why the empires it saved carry
    /// gov_executive_committee and gov_megacorporation while the ones made here all carried
    /// gov_despotic_empire, the blank template's own value, whatever they had since become.
    /// </para>
    /// <para>
    /// It went unseen because nothing here reads it. Every place that shows a government derives it
    /// again from the design, so the card said Military Dictatorship while the file said despotic
    /// empire, and the two only ever met on the way out - in an export, in a shared link, or in the
    /// game.
    /// </para>
    /// <para>
    /// Only when there is one. An empire whose choices match no government the game has is told so
    /// on its own card; writing nothing over what the design arrived holding would be throwing away
    /// the better of two answers.
    /// </para>
    /// </remarks>
    private static void WriteGovernment(EmpireDesign design, DesignContext context)
    {
        if (context.Government is { Length: > 0 } government)
        {
            design.Government = government;
        }
    }

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
    private void AddForcedTraits(EmpireDesign design, DesignContext context)
    {
        var forced = Rules.GetWrittenForcedTraits(context);
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
}
