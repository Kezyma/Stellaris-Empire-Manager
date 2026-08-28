namespace Sem.Designs;

/// <summary>
/// Translates a built-in empire into the player's designs format.
/// </summary>
/// <remarks>
/// Names arrive as plain localisation keys and become structured names referring to those same
/// keys, so a converted empire still displays its original translated name until the player types
/// over it. Prescripted-only fields are dropped: <c>playable</c> gates which built-ins the game
/// offers, and <c>heir_title</c> has no equivalent in a player design.
/// </remarks>
internal static class PrescriptedConverter
{
    public static void Populate(PrescriptedEmpire source, EmpireDesign target)
    {
        CopyName(target.Name, source.Name);
        CopyName(target.Adjective, source.Adjective);
        CopyName(target.ShipPrefix, source.ShipPrefix);
        CopyName(target.PlanetName, source.PlanetName);
        CopyName(target.SystemName, source.SystemName);

        target.Authority = source.Authority;
        target.Government = source.Government;
        target.Origin = source.Origin;
        target.PlanetClass = source.PlanetClass;
        target.Initializer = source.Initializer ?? string.Empty;
        target.GraphicalCulture = source.GraphicalCulture;
        target.CityGraphicalCulture = source.CityGraphicalCulture;
        target.Room = source.Room;
        target.AdvisorVoiceType = source.AdvisorVoiceType;
        target.PrescriptedFlag = source.PrescriptedFlag;
        target.IsNomadic = source.IsNomadic;
        target.ShipSize = source.ShipSize;
        target.SpawnAsFallen = source.SpawnAsFallen ?? false;
        target.IgnorePortraitDuplication = source.IgnorePortraitDuplication ?? false;

        // Deliberately not copied from the source: importing a preset must never quietly add an
        // AI empire to every future game.
        target.SpawnEnabled = "no";

        target.SetEthics(source.Ethics);
        target.SetCivics(source.Civics);

        if (source.Species is { } species)
        {
            CopySpecies(species, target.Species);
        }

        if (source.SecondarySpecies is { } secondary)
        {
            CopySpecies(secondary, target.AddSecondarySpecies());
        }

        if (source.Flag is { } flag)
        {
            CopyFlag(flag, target.Flag);
        }

        if (source.Ruler is { } ruler)
        {
            CopyRuler(ruler, target.Ruler);
        }
    }

    private static void CopySpecies(PrescriptedSpecies source, SpeciesDesign target)
    {
        target.Class = source.Class;
        target.Portrait = source.Portrait;
        target.NameList = source.NameList;
        target.Gender = source.Gender ?? "not_set";
        target.SetTraits(source.Traits);

        CopyName(target.Name, source.Name);
        CopyName(target.Plural, source.Plural);
        CopyName(target.Adjective, source.Adjective);
    }

    /// <summary>
    /// Copies the flag field by field rather than cloning its nodes, so the result is written in
    /// the designs file's style instead of inheriting the game script's spacing.
    /// </summary>
    private static void CopyFlag(EmpireFlag source, EmpireFlag target)
    {
        target.Icon.Category = source.Icon.Category;
        target.Icon.File = source.Icon.File;
        target.Background.Category = source.Background.Category;
        target.Background.File = source.Background.File;
        target.SetColors(source.Colors);
    }

    private static void CopyRuler(PrescriptedRuler source, RulerDesign target)
    {
        target.Gender = source.Gender ?? "not_set";
        target.Portrait = source.Portrait;
        target.Texture = source.Texture ?? 0;
        target.EvolutionMask = source.EvolutionMask ?? 0;
        target.Attachment = source.Attachment ?? 0;
        target.Clothes = source.Clothes ?? 0;
        target.LeaderClass = source.LeaderClass;
        target.SetTraits(source.Traits);

        // Player designs always use the full_names form, even when the source split the name.
        var name = source.Name ?? source.FirstName;
        if (name is not null)
        {
            target.Name.GetOrAddFullNames().Key = name;
        }

        if (source.Title is not null)
        {
            target.GetOrAddTitle().Key = source.Title;
        }

        if (source.TitleFemale is not null)
        {
            target.GetOrAddTitleFemale().Key = source.TitleFemale;
        }
    }

    /// <summary>
    /// Points a structured name at a localisation key, leaving it non-literal so the game still
    /// translates it.
    /// </summary>
    private static void CopyName(LocRef target, string? localisationKey)
    {
        if (localisationKey is not null)
        {
            target.Key = localisationKey;
        }
    }
}
