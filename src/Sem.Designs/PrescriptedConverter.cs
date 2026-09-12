namespace Sem.Designs;

/// <summary>
/// Translates a built-in empire into the player's designs format.
/// </summary>
/// <remarks>
/// Names arrive as plain localisation keys and become structured names referring to those same
/// keys, so a converted empire still displays its original translated name until the player types
/// over it. One prescripted-only field is dropped: <c>playable</c>, which gates which built-ins the
/// game offers rather than saying anything about the empire.
/// <c>heir_title</c> used to be dropped beside it, on the claim that it has no equivalent in a
/// player design. It has an exact one — the game's editor keeps a box for it and its female form,
/// and the empire card already draws a row for them — so both are copied now.
/// </remarks>
internal static class PrescriptedConverter
{
    public static void Populate(PrescriptedEmpire source, EmpireDesign target)
    {
        CopyName(() => target.Name, source.Name);
        CopyName(() => target.Adjective, source.Adjective);
        CopyName(() => target.ShipPrefix, source.ShipPrefix);
        CopyName(() => target.PlanetName, source.PlanetName);
        CopyName(() => target.SystemName, source.SystemName);

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
        // Defaulted like the three booleans around it. The game writes is_nomadic into every
        // design it saves, and only one preset of the fifty-two states it, so leaving it null wrote
        // the field out of fifty-one empires that do have an answer to it.
        target.IsNomadic = source.IsNomadic ?? false;
        target.ShipSize = source.ShipSize;
        target.SpawnAsFallen = source.SpawnAsFallen ?? false;
        target.IgnorePortraitDuplication = source.IgnorePortraitDuplication ?? false;

        // Copied, like everything else. It had been forced to "no" on the reasoning that importing a
        // preset should not quietly add an AI empire to every future game - but thirty-three of the
        // fifty-one ship with it set to yes and the game already spawns them, so forcing it was not
        // preventing an AI empire, it was showing the wrong answer for two thirds of the list. What
        // an import owes the player is the empire as the game has it; what they do with it after is
        // a switch on the card.
        target.SpawnEnabled = source.SpawnEnabled ?? "no";

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

        CopyName(() => target.Name, source.Name);
        CopyName(() => target.Plural, source.Plural);
        CopyName(() => target.Adjective, source.Adjective);
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
        // All four, because all four are the game's: two the flag draws and two the map reads. The
        // fourth used to be dropped here, on the belief that nothing read it and this app could keep
        // something of its own there - which made the Infernals' pyrragthul, written
        // "orange" "red" "orange" "red", the one template that lost a colour on the way in.
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
        // A split name keeps both halves rather than only the first: the game says a two-part name
        // inside full_names, with a format key and the parts as numbered variables, so there is a
        // shape for it here and dropping the second name was never needed. Two presets have one -
        // gorthikan and knights, both from the Toxoids pack.
        //
        // %LEADER_1% of the two keys the game uses. Neither is defined in any file it ships - both
        // are compiled into the executable - so what separates them was read off the player's own
        // designs file, where the two entries carrying use_full_regnal_name=yes are the two written
        // %LEADER_2% and the one without it is written %LEADER_1%. A prescripted empire has no
        // regnal flag to copy, so it takes the key that goes with the flag's absence.
        //
        // Which does not decide what is displayed: Localizer.Name reads both keys the same way, as
        // a given name and a family name meaning the whole name, and says so from a corpus where
        // reading only the first part had dropped twelve rulers' surnames. So the choice is about
        // writing the file the way the game writes it, and both halves are read either way.
        if (source.FirstName is { } first && source.SecondName is { } second)
        {
            target.Name.GetOrAddFullNames().SetFormat("%LEADER_1%", [first, second]);
        }
        else if ((source.Name ?? source.FirstName) is { } name)
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

        if (source.HeirTitle is not null)
        {
            target.GetOrAddHeirTitle().Key = source.HeirTitle;
        }

        if (source.HeirTitleFemale is not null)
        {
            target.GetOrAddHeirTitleFemale().Key = source.HeirTitleFemale;
        }
    }

    /// <summary>
    /// Points a structured name at a localisation key, leaving it non-literal so the game still
    /// translates it.
    /// </summary>
    /// <remarks>
    /// The target is reached through a function rather than passed in, so a source with no such
    /// name leaves no empty block behind. The game's own blank template names almost nothing.
    /// </remarks>
    private static void CopyName(Func<LocRef> target, string? localisationKey)
    {
        if (localisationKey is not null)
        {
            target().Key = localisationKey;
        }
    }
}
