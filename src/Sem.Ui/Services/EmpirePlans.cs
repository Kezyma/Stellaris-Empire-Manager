using Sem.Designs;

namespace Sem.Ui.Services;

/// <summary>
/// Reading and writing an empire's plan, which lives in one of its two biographies.
/// </summary>
/// <remarks>
/// <para>
/// Nothing records whether an empire has a plan or which biography holds it. Both answers come from
/// looking: a biography that reads as a plan is one, and it is the one. That is worth the small cost
/// of parsing twice, because the alternative is a flag stored somewhere that can disagree with the
/// text - and it would disagree, the first time a design arrived from somebody else's file.
/// </para>
/// <para>
/// Only one at a time. Turning planning on for the ruler when the species already carried it moves
/// the plan rather than leaving two, since two would make "which is the plan" a question again.
/// </para>
/// </remarks>
public sealed class EmpirePlans(PlanText text)
{
    /// <summary>How a plan is turned into prose and read back.</summary>
    public PlanText Text { get; } = text;

    /// <summary>
    /// Which biography is carrying a plan, or nothing when neither is.
    /// </summary>
    /// <remarks>
    /// The species is asked first only so that a design somehow carrying two gives a stable answer.
    /// Writing never produces that, but a hand-edited file can.
    /// </remarks>
    public PlanHome? HomeOf(EmpireDesign? design, PlanVocabulary vocabulary)
    {
        if (design is null)
        {
            return null;
        }

        if (Text.IsPlan(SpeciesBiography(design), vocabulary))
        {
            return PlanHome.Species;
        }

        return Text.IsPlan(RulerBiography(design), vocabulary) ? PlanHome.Ruler : null;
    }

    /// <summary>The plan an empire carries, which is empty when it carries none.</summary>
    public EmpirePlan PlanOf(EmpireDesign? design, PlanVocabulary vocabulary) =>
        HomeOf(design, vocabulary) switch
        {
            PlanHome.Species => Text.Read(SpeciesBiography(design), vocabulary),
            PlanHome.Ruler => Text.Read(RulerBiography(design), vocabulary),
            _ => EmpirePlan.Empty,
        };

    /// <summary>
    /// Writes a plan into the biography chosen for it, and takes it out of the other.
    /// </summary>
    /// <remarks>
    /// A plan with nothing in it empties the field rather than writing a heading with nothing under
    /// it, so turning planning off gives the player their biography back rather than leaving a
    /// husk of one.
    /// </remarks>
    public void Write(EmpireDesign design, EmpirePlan plan, PlanHome home, PlanVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(plan);

        // Never over something somebody wrote. The caller is expected to have asked first, and this
        // is the second lock on the same door: a plan that quietly replaced a biography would be
        // taking work away, and nothing in the design would say what it had been.
        if (!CanCarry(design, home, vocabulary))
        {
            return;
        }

        Clear(design, home is PlanHome.Species ? PlanHome.Ruler : PlanHome.Species, vocabulary);

        var written = plan.Any ? Text.Write(plan) : null;

        if (home is PlanHome.Species)
        {
            design.Species.Biography = written;
        }
        else if (written is { Length: > 0 })
        {
            design.Ruler.GetOrAddCustomBiography().SetLiteral(written);
        }
        else
        {
            design.Ruler.RemoveCustomBiography();
        }
    }

    /// <summary>
    /// Takes a plan out of a biography, leaving anything that was not a plan alone.
    /// </summary>
    /// <remarks>
    /// The check matters. Turning planning off, or moving it to the other field, must not delete a
    /// biography the player actually wrote - and the only thing that tells the two apart is whether
    /// the text reads as a plan.
    /// </remarks>
    public void Clear(EmpireDesign design, PlanHome home, PlanVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(design);

        if (home is PlanHome.Species)
        {
            if (Text.IsPlan(SpeciesBiography(design), vocabulary))
            {
                design.Species.Biography = null;
            }

            return;
        }

        if (Text.IsPlan(RulerBiography(design), vocabulary))
        {
            design.Ruler.RemoveCustomBiography();
        }
    }

    /// <summary>
    /// Whether a biography is free for a plan to use.
    /// </summary>
    /// <remarks>
    /// Free means empty, or already carrying a plan. A biography the player wrote is not free, and
    /// there is no way to tell one they wrote from one they would not miss - so the answer for
    /// anything with words in it is no, and the choice of what to do about that is theirs.
    /// </remarks>
    public bool CanCarry(EmpireDesign? design, PlanHome home, PlanVocabulary vocabulary)
    {
        var written = home is PlanHome.Species ? SpeciesBiography(design) : RulerBiography(design);

        return written is not { Length: > 0 } || Text.IsPlan(written, vocabulary);
    }

    /// <summary>What is written in the founding species' biography, if anything.</summary>
    public static string? SpeciesBiography(EmpireDesign? design) => design?.Species.Biography;

    /// <summary>What is written in the ruler's biography, if anything.</summary>
    public static string? RulerBiography(EmpireDesign? design) => design?.Ruler.CustomBiography?.Key;
}
