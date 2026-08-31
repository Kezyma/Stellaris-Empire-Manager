using Sem.Designs;

namespace Sem.Core.Tests.Designs;

public sealed class EmpireDesignTests
{
    /// <summary>A design covering the fields and shapes the real file uses.</summary>
    private const string Sample =
        "\"Peacock Dynamics\"=\r\n" +
        "{\r\n" +
        "\tkey=\"Peacock Dynamics\"\r\n" +
        "\tship_prefix=\r\n" +
        "\t{\r\n" +
        "\t\tkey=\"ISS\"\r\n" +
        "\t}\r\n" +
        "\tspecies=\r\n" +
        "\t{\r\n" +
        "\t\tclass=\"AVI\"\r\n" +
        "\t\tportrait=\"avi18\"\r\n" +
        "\t\tspecies_name=\r\n" +
        "\t\t{\r\n" +
        "\t\t\tkey=\"Peacock\"\r\n" +
        "\t\t\tliteral=yes\r\n" +
        "\t\t}\r\n" +
        "\t\tname_list=\"AVI3\"\r\n" +
        "\t\tgender=not_set\r\n" +
        "\t\ttrait=\"trait_aquatic\"\r\n" +
        "\t\ttrait=\"trait_organic\"\r\n" +
        "\t}\r\n" +
        "\tname=\r\n" +
        "\t{\r\n" +
        "\t\tkey=\"Peacock Dynamics\"\r\n" +
        "\t\tliteral=yes\r\n" +
        "\t}\r\n" +
        "\tadjective=\r\n" +
        "\t{\r\n" +
        "\t\tkey=\"Peacock\"\r\n" +
        "\t}\r\n" +
        "\tauthority=\"auth_corporate\"\r\n" +
        "\tgovernment=\"gov_megacorporation\"\r\n" +
        "\tis_nomadic=no\r\n" +
        "\tadvisor_voice_type=\"l_english\"\r\n" +
        "\tplanet_class=\"pc_tropical\"\r\n" +
        "\tinitializer=\"ocean_paradise_start\"\r\n" +
        "\tgraphical_culture=\"avian_01\"\r\n" +
        "\tcity_graphical_culture=\"avian_01\"\r\n" +
        "\tempire_flag=\r\n" +
        "\t{\r\n" +
        "\t\ticon=\r\n" +
        "\t\t{\r\n" +
        "\t\t\tcategory=\"zoological\"\r\n" +
        "\t\t\tfile=\"flag_zoological_4.dds\"\r\n" +
        "\t\t}\r\n" +
        "\t\tbackground=\r\n" +
        "\t\t{\r\n" +
        "\t\t\tcategory=\"backgrounds\"\r\n" +
        "\t\t\tfile=\"00_solid.dds\"\r\n" +
        "\t\t}\r\n" +
        "\t\tcolors=\r\n" +
        "\t\t{\r\n" +
        "\t\t\t\"ship_steel\"\r\n" +
        "\t\t\t\"red\"\r\n" +
        "\t\t\t\"black\"\r\n" +
        "\t\t\t\"null\"\r\n" +
        "\t\t}\r\n" +
        "\t}\r\n" +
        "\truler=\r\n" +
        "\t{\r\n" +
        "\t\tgender=female\r\n" +
        "\t\tname=\r\n" +
        "\t\t{\r\n" +
        "\t\t\tfull_names=\r\n" +
        "\t\t\t{\r\n" +
        "\t\t\t\tkey=\"%LEADER_2%\"\r\n" +
        "\t\t\t\tvariables=\r\n" +
        "\t\t\t\t{\r\n" +
        "\t\t\t\t\t\r\n" +
        "\t\t\t\t\t{\r\n" +
        "\t\t\t\t\t\tkey=\"1\"\r\n" +
        "\t\t\t\t\t\tvalue=\r\n" +
        "\t\t\t\t\t\t{\r\n" +
        "\t\t\t\t\t\t\tkey=\"AVI3_CHR_Feathers_of\"\r\n" +
        "\t\t\t\t\t\t}\r\n" +
        "\t\t\t\t\t}\r\n" +
        " \r\n" +
        "\t\t\t\t}\r\n" +
        "\t\t\t}\r\n" +
        "\t\t\tuse_full_regnal_name=yes\r\n" +
        "\t\t}\r\n" +
        "\t\tportrait=\"avi18\"\r\n" +
        "\t\ttexture=1\r\n" +
        "\t\tevolution_mask=0\r\n" +
        "\t\tattachment=0\r\n" +
        "\t\tclothes=0\r\n" +
        "\t\ttrait=\"trait_ruler_logistic_understanding\"\r\n" +
        "\t\tleader_class=\"official\"\r\n" +
        "\t}\r\n" +
        "\tspawn_as_fallen=no\r\n" +
        "\tignore_portrait_duplication=no\r\n" +
        "\troom=\"default_room\"\r\n" +
        "\tspawn_enabled=no\r\n" +
        "\tethic=\"ethic_fanatic_xenophile\"\r\n" +
        "\tethic=\"ethic_pacifist\"\r\n" +
        "\tcivics=\r\n" +
        "\t{\r\n" +
        "\t\t\"civic_corporate_catalytic_processing\"\r\n" +
        "\t\t\"civic_corporate_anglers\"\r\n" +
        "\t}\r\n" +
        "\torigin=\"origin_ocean_paradise\"\r\n" +
        "}\r\n";

    private static EmpireDesign LoadSample() => EmpireDesignsFile.LoadText(Sample).Designs[0];

    [Fact]
    public void ReadsEveryTopLevelField()
    {
        var design = LoadSample();

        Assert.Equal("Peacock Dynamics", design.Key);
        Assert.Equal("ISS", design.ShipPrefix.Key);
        Assert.Equal("auth_corporate", design.Authority);
        Assert.Equal("gov_megacorporation", design.Government);
        Assert.False(design.IsNomadic);
        Assert.Equal("l_english", design.AdvisorVoiceType);
        Assert.Equal("pc_tropical", design.PlanetClass);
        Assert.Equal("ocean_paradise_start", design.Initializer);
        Assert.Equal("avian_01", design.GraphicalCulture);
        Assert.Equal("avian_01", design.CityGraphicalCulture);
        Assert.Equal("default_room", design.Room);
        Assert.Equal("no", design.SpawnEnabled);
        Assert.False(design.SpawnAsFallen);
        Assert.False(design.IgnorePortraitDuplication);
        Assert.Equal("origin_ocean_paradise", design.Origin);
        Assert.Equal(["ethic_fanatic_xenophile", "ethic_pacifist"], design.Ethics);
        Assert.Equal(["civic_corporate_catalytic_processing", "civic_corporate_anglers"], design.Civics);
    }

    [Fact]
    public void DistinguishesPlayerTypedNamesFromLocalisationKeys()
    {
        var design = LoadSample();

        Assert.True(design.Name.IsLiteral);
        Assert.Equal("Peacock Dynamics", design.Name.Key);

        // The adjective has no literal flag, so it names a localisation key instead.
        Assert.False(design.Adjective.IsLiteral);
        Assert.Equal("Peacock", design.Adjective.Key);
    }

    [Fact]
    public void ReadsTheSpecies()
    {
        var species = LoadSample().Species;

        Assert.Equal("AVI", species.Class);
        Assert.Equal("avi18", species.Portrait);
        Assert.Equal("AVI3", species.NameList);
        Assert.Equal("not_set", species.Gender);
        Assert.Equal(["trait_aquatic", "trait_organic"], species.Traits);
        Assert.True(species.Name.IsLiteral);
        Assert.Equal("Peacock", species.Name.Key);
    }

    [Fact]
    public void ReadsTheRulerIncludingItsNestedName()
    {
        var ruler = LoadSample().Ruler;

        Assert.Equal("female", ruler.Gender);
        Assert.Equal("avi18", ruler.Portrait);
        Assert.Equal(1, ruler.Texture);
        Assert.Equal(0, ruler.EvolutionMask);
        Assert.Equal("official", ruler.LeaderClass);
        Assert.Equal(["trait_ruler_logistic_understanding"], ruler.Traits);
        Assert.True(ruler.Name.UseFullRegnalName);

        var fullNames = ruler.Name.FullNames;
        Assert.NotNull(fullNames);
        Assert.Equal("%LEADER_2%", fullNames!.Key);

        var variable = Assert.Single(fullNames.Variables);
        Assert.Equal("1", variable.Key);
        Assert.Equal("AVI3_CHR_Feathers_of", variable.Value?.Key);
    }

    [Fact]
    public void ReadsTheFlagIncludingItsPaddedColourSlots()
    {
        var flag = LoadSample().Flag;

        Assert.Equal("zoological", flag.Icon.Category);
        Assert.Equal("flag_zoological_4.dds", flag.Icon.File);
        Assert.Equal("backgrounds", flag.Background.Category);
        Assert.Equal(["ship_steel", "red", "black", "null"], flag.Colors);
    }

    [Fact]
    public void SecondarySpeciesIsAbsentUnlessTheOriginAddsOne()
    {
        Assert.Null(LoadSample().SecondarySpecies);
        Assert.False(LoadSample().HasSecondarySpecies);
    }

    [Fact]
    public void SavingAnUntouchedFileReproducesItExactly()
    {
        var file = EmpireDesignsFile.LoadText(Sample);

        Assert.Equal(Sample, file.Document.ToText());
    }

    [Fact]
    public void ChangingOneFieldChangesOnlyThatField()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        file.Designs[0].Authority = "auth_democratic";

        Assert.Equal(
            Sample.Replace("\"auth_corporate\"", "\"auth_democratic\"", StringComparison.Ordinal),
            file.Document.ToText());
    }

    /// <summary>
    /// The ruler's ascended form, which the designer can now set.
    /// </summary>
    /// <remarks>
    /// Every design the game has written holds <c>evolution_mask=0</c>, so this is the first field
    /// the app changes whose every observed value is zero. Worth stating that raising it rewrites
    /// one digit and nothing else, and that the key keeps the place the game gives it — between the
    /// skin variant and the attachment — rather than being appended as a new field would be.
    /// </remarks>
    [Fact]
    public void RaisingTheRulersEvolutionStageRewritesOneDigit()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        file.Designs[0].Ruler.EvolutionMask = 3;

        var text = file.Document.ToText();

        Assert.Equal(
            Sample.Replace("evolution_mask=0", "evolution_mask=3", StringComparison.Ordinal),
            text);

        Assert.InRange(
            text.IndexOf("evolution_mask", StringComparison.Ordinal),
            text.IndexOf("texture", StringComparison.Ordinal),
            text.IndexOf("attachment", StringComparison.Ordinal));
    }

    [Fact]
    public void ReplacingOneTraitRewritesOnlyThatLine()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        file.Designs[0].Species.SetTraits(["trait_intelligent", "trait_organic"]);

        Assert.Equal(
            Sample.Replace("\"trait_aquatic\"", "\"trait_intelligent\"", StringComparison.Ordinal),
            file.Document.ToText());
    }

    [Fact]
    public void AddingATraitInsertsOneLineBesideTheExistingOnes()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        file.Designs[0].Species.SetTraits(["trait_aquatic", "trait_organic", "trait_intelligent"]);

        Assert.Equal(
            Sample.Replace(
                "\t\ttrait=\"trait_organic\"\r\n",
                "\t\ttrait=\"trait_organic\"\r\n\t\ttrait=\"trait_intelligent\"\r\n",
                StringComparison.Ordinal),
            file.Document.ToText());
    }

    [Fact]
    public void RemovingATraitDeletesOneLine()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        file.Designs[0].Species.SetTraits(["trait_aquatic"]);

        Assert.Equal(
            Sample.Replace("\t\ttrait=\"trait_organic\"\r\n", string.Empty, StringComparison.Ordinal),
            file.Document.ToText());
    }

    [Fact]
    public void AddingAMissingFieldPutsItWhereTheGameWouldWriteIt()
    {
        var file = EmpireDesignsFile.LoadText(Sample);

        // planet_class exists and system_name does not; system_name belongs between them.
        file.Designs[0].SystemName.SetLiteral("Chimken");

        var text = file.Document.ToText();
        var afterPlanetClass = text.IndexOf("planet_class", StringComparison.Ordinal);
        var systemName = text.IndexOf("system_name", StringComparison.Ordinal);
        var initializer = text.IndexOf("initializer", StringComparison.Ordinal);

        Assert.True(afterPlanetClass < systemName, "system_name should follow planet_class.");
        Assert.True(systemName < initializer, "system_name should precede initializer.");
    }

    [Fact]
    public void RenamingUpdatesBothTheEntryKeyAndTheKeyField()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        file.Designs[0].Rename("Peacock Republic");

        var text = file.Document.ToText();

        Assert.StartsWith("\"Peacock Republic\"=", text, StringComparison.Ordinal);
        Assert.Contains("\tkey=\"Peacock Republic\"\r\n", text, StringComparison.Ordinal);

        // The displayed name is a separate field and is deliberately left alone.
        Assert.Contains("\t\tkey=\"Peacock Dynamics\"\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownFieldsSurviveAnEdit()
    {
        // A field from a future patch, or a mod, must not be dropped when saving.
        const string withUnknown = "\"Empire\"=\r\n{\r\n\tkey=\"Empire\"\r\n\tfuture_field=\"keep me\"\r\n}\r\n";

        var file = EmpireDesignsFile.LoadText(withUnknown);
        file.Designs[0].Authority = "auth_democratic";

        Assert.Contains("future_field=\"keep me\"", file.Document.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void CopyingADesignProducesAnIndependentEmpire()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        var copy = file.AddCopy(file.Designs[0], "Peacock Republic");

        copy.Authority = "auth_democratic";

        Assert.Equal(2, file.Designs.Count);
        Assert.Equal("auth_corporate", file.Designs[0].Authority);
        Assert.Equal("auth_democratic", copy.Authority);
        Assert.Equal("Peacock Republic", copy.Key);
    }

    [Fact]
    public void CopyingRefusesToReuseAnExistingName()
    {
        var file = EmpireDesignsFile.LoadText(Sample);

        Assert.Throws<ArgumentException>(() => file.AddCopy(file.Designs[0], "Peacock Dynamics"));
    }

    [Fact]
    public void RemovingADesignTakesItOutOfTheFile()
    {
        var file = EmpireDesignsFile.LoadText(Sample);

        Assert.True(file.Remove(file.Designs[0]));
        Assert.Empty(file.Designs);
        Assert.Equal("\r\n", file.Document.ToText());
    }

    [Fact]
    public void NewDesignsAreWrittenInTheGamesFormat()
    {
        var file = EmpireDesignsFile.CreateEmpty();
        var design = file.Add("New Empire");
        design.Authority = "auth_democratic";
        design.Origin = "origin_default";

        Assert.Equal(
            "\"New Empire\"=\r\n" +
            "{\r\n" +
            "\tkey=\"New Empire\"\r\n" +
            "\tauthority=\"auth_democratic\"\r\n" +
            "\torigin=\"origin_default\"\r\n" +
            "}\r\n",
            file.Document.ToText());
    }

    [Fact]
    public void TraitsAddedToAnEmptySpeciesKeepTheirOrder()
    {
        // Regression: inserting repeated keys used to place each new entry ahead of the previous
        // one, which silently reversed a species' traits when building a design from scratch.
        var file = EmpireDesignsFile.CreateEmpty();
        var species = file.Add("New Empire").Species;
        species.Class = "HUM";

        species.SetTraits(["trait_organic", "trait_adaptive", "trait_nomadic", "trait_wasteful"]);

        Assert.Equal(["trait_organic", "trait_adaptive", "trait_nomadic", "trait_wasteful"], species.Traits);
    }

    [Fact]
    public void EthicsAddedToAnEmptyDesignKeepTheirOrder()
    {
        var file = EmpireDesignsFile.CreateEmpty();
        var design = file.Add("New Empire");

        design.SetEthics(["ethic_fanatic_militarist", "ethic_xenophobe"]);

        Assert.Equal(["ethic_fanatic_militarist", "ethic_xenophobe"], design.Ethics);
    }

    [Fact]
    public void SettingFlagColoursPadsToFourSlots()
    {
        var file = EmpireDesignsFile.LoadText(Sample);
        file.Designs[0].Flag.SetColors(["blue", "white"]);

        Assert.Equal(["blue", "white", "null", "null"], file.Designs[0].Flag.Colors);
    }

    [Fact]
    public void SettingMoreThanFourFlagColoursIsRejected()
    {
        var file = EmpireDesignsFile.LoadText(Sample);

        Assert.Throws<ArgumentException>(
            () => file.Designs[0].Flag.SetColors(["a", "b", "c", "d", "e"]));
    }

    [Fact]
    public void ARulersBiographyIsWrittenWhereTheGameWritesIt()
    {
        var file = EmpireDesignsFile.LoadText(Sample);

        file.Designs[0].Ruler.GetOrAddCustomBiography().SetLiteral("Raised by machines.");

        var text = file.Document.ToText();
        var clothes = text.IndexOf("clothes", StringComparison.Ordinal);
        var biography = text.IndexOf("custom_biography", StringComparison.Ordinal);

        // The ruler's own trait by name, since the species above it has traits indented the same.
        var trait = text.IndexOf("trait_ruler_", StringComparison.Ordinal);

        Assert.True(clothes < biography, "custom_biography should follow clothes.");
        Assert.True(biography < trait, "custom_biography should precede the ruler's trait.");
    }

    [Fact]
    public void ARulersBiographyIsAName()
    {
        // The ruler's is a name block — key and literal — while the species' biography beside it is
        // a bare quoted string. The asymmetry is the game's, and writing the ruler's the species'
        // way would produce a file the game reads differently from the one it wrote.
        var file = EmpireDesignsFile.LoadText(Sample);

        file.Designs[0].Ruler.GetOrAddCustomBiography().SetLiteral("Raised by machines.");

        Assert.Contains("literal=yes", file.Document.ToText(), StringComparison.Ordinal);
        Assert.Equal("Raised by machines.", file.Designs[0].Ruler.CustomBiography?.Key);
    }

    /// <summary>Every way the designer has of putting a new empire into a file.</summary>
    public static TheoryData<string> WaysToAdd => new("blank", "copy-first", "copy-last", "template");

    [Theory]
    [MemberData(nameof(WaysToAdd))]
    public void AnAddedEmpireStartsOnItsOwnLine(string how)
    {
        // The game always writes an empire's closing brace and the next empire's name on separate
        // lines. An entry copied from the top of another file remembered having nothing in front of
        // it, so appending it ran the two together as }"New Empire"= — which is what the designer
        // wrote for every empire it created, since both the "New empire" button and duplicating the
        // first empire copy an entry that sat at the top of something.
        var file = EmpireDesignsFile.LoadText(Sample);
        var second = file.AddCopy(file.Designs[0], "Second Empire");

        _ = how switch
        {
            "blank" => file.Add("Added"),
            "copy-first" => file.AddCopy(file.Designs[0], "Added"),
            "copy-last" => file.AddCopy(second, "Added"),
            _ => file.AddFromTemplate(Sample, "Added"),
        };

        var lines = file.Document.ToText().Split("\r\n");

        Assert.DoesNotContain(lines, line => line.StartsWith("}\"", StringComparison.Ordinal));

        // And the entry really is there, rather than the check passing because nothing was added.
        Assert.Contains(lines, line => line == "\"Added\"=");
    }

    [Fact]
    public void AddingAnEmpireLeavesEveryOtherByteAlone()
    {
        // The strongest thing that can be said about an edit: the empires nobody touched come back
        // exactly as they were, and the new one is written after them.
        var original = EmpireDesignsFile.LoadText(Sample).Document.ToText();

        var file = EmpireDesignsFile.LoadText(Sample);
        file.Add("Added");

        Assert.StartsWith(original, file.Document.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnHeirTitleIsWrittenWhereTheGamesOwnEditorPutsIt()
    {
        var file = EmpireDesignsFile.LoadText(Sample);

        file.Designs[0].Ruler.GetOrAddTitle().SetLiteral("Surveyor");
        file.Designs[0].Ruler.GetOrAddHeirTitle().SetLiteral("Surveyor Apparent");
        file.Designs[0].Ruler.GetOrAddTitleFemale().SetLiteral("Matriarch");

        // Searched for with their line endings, since "ruler_title" is also the start of
        // "ruler_title_female" and would otherwise find whichever of them came first.
        var text = file.Document.ToText();
        var title = text.IndexOf("ruler_title=", StringComparison.Ordinal);
        var heir = text.IndexOf("heir_title=", StringComparison.Ordinal);
        var female = text.IndexOf("ruler_title_female=", StringComparison.Ordinal);

        // The game's editor shows a title, its heir, then the same pair again in their female forms.
        Assert.True(title < heir, "heir_title should follow ruler_title.");
        Assert.True(heir < female, "ruler_title_female should follow heir_title.");
        Assert.Equal("Surveyor Apparent", file.Designs[0].Ruler.HeirTitle?.Key);
        Assert.Equal("Matriarch", file.Designs[0].Ruler.TitleFemale?.Key);
    }

    [Fact]
    public void ADesignWithNoHeirTitleStaysAsItWas()
    {
        // No design of the player's carries one, and the field is rare enough in the game's own
        // empires that reading it must not be what puts it there.
        var file = EmpireDesignsFile.LoadText(Sample);

        Assert.Null(file.Designs[0].Ruler.HeirTitle);
        Assert.Null(file.Designs[0].Ruler.HeirTitleFemale);
        Assert.Equal(Sample, file.Document.ToText());
    }

    [Fact]
    public void ADesignWithNoRulerBiographyStaysAsItWas()
    {
        // Reading one that has none must not add an empty block, or every design opened would come
        // back changed.
        var file = EmpireDesignsFile.LoadText(Sample);

        Assert.Null(file.Designs[0].Ruler.CustomBiography);
        Assert.Equal(Sample, file.Document.ToText());
    }

    [Fact]
    public void AnEmpirePutBackFromASnapshotLeavesTheFileAsItFoundIt()
    {
        // What the designer's Revert has to amount to. Edited and then put back, the file must be
        // the file it was — down to the whitespace, since every other empire in it is untouched and
        // this one was only visited.
        var file = EmpireDesignsFile.LoadText(Sample);
        var design = file.Designs[0];
        var saved = design.Snapshot();

        design.Rename("Something Else");
        design.SetEthics(["ethic_fanatic_pacifist", "ethic_egalitarian"]);
        design.Species.SetTraits(["trait_intelligent"]);
        design.Ruler.Gender = "female";

        Assert.NotEqual(Sample, file.Document.ToText());

        design.Restore(saved);

        Assert.Equal(Sample, file.Document.ToText());
        Assert.Equal("Peacock Dynamics", design.Key);
    }

    [Fact]
    public void ASnapshotIsNotDisturbedByWhatHappensAfterIt()
    {
        // Taken as a copy, not as a view of the design. A snapshot that shared the design's own
        // nodes would quietly become a copy of the edits it was supposed to undo.
        var design = LoadSample();
        var saved = design.Snapshot();

        design.Species.SetTraits(["trait_intelligent", "trait_rapid_breeders"]);
        design.Restore(saved);

        Assert.Equal(["trait_aquatic", "trait_organic"], design.Species.Traits);
    }

    [Fact]
    public void PuttingAnEmpireBackTwiceIsTheSameAsPuttingItBackOnce()
    {
        // Revert is a button, and buttons get pressed twice.
        var file = EmpireDesignsFile.LoadText(Sample);
        var design = file.Designs[0];
        var saved = design.Snapshot();

        design.SetCivics(["civic_beastmasters"]);

        design.Restore(saved);
        design.Restore(saved);

        Assert.Equal(Sample, file.Document.ToText());
    }

    [Theory]
    [InlineData(new[] { "%ADJECTIVE%", "Astral_Fellowship" },
        "%ADJECTIVE%", "adjective=SPEC_Xeltek;1=Astral_Fellowship")]
    [InlineData(new[] { "Blessed", "%ADJECTIVE%", "Union" },
        "%ADJ%", "1=Blessed;1=%ADJECTIVE%;adjective=SPEC_Xeltek;1=Union")]
    [InlineData(new[] { "%ADJECTIVE%", "Irenic", "Kingdom" },
        "%ADJECTIVE%", "adjective=SPEC_Xeltek;1=%ADJ%;1=Irenic;1=Kingdom")]
    public void AGeneratedNameIsWrittenTheWayTheGameWritesOne(string[] words, string outermost, string trail)
    {
        // Taken from three real empires in the player's file: "Xeltek Astral Fellowship" leads with
        // the species adjective and needs no wrapper, "Blessed Oxanalytoran Union" leads with a
        // describing word and is wrapped in %ADJ%, and "Frubralav Irenic Kingdom" wraps only its
        // inner half. Getting this wrong still reads correctly, which is exactly why it is asserted:
        // the file would simply stop looking like one the game had written.
        var design = LoadSample();
        design.Name.SetFormat("placeholder", []);
        design.Name.SetNested(words, "SPEC_Xeltek");

        Assert.Equal(outermost, design.Name.Key);
        Assert.Equal(trail, Trail(design.Name));
    }

    /// <summary>Every key and variable down the chain, in order, so a shape can be stated in one line.</summary>
    private static string Trail(LocRef reference)
    {
        var parts = new List<string>();

        void Walk(LocRef at)
        {
            foreach (var variable in at.Variables)
            {
                parts.Add($"{variable.Key}={variable.Value?.Key}");

                if (variable.Value is { } value)
                {
                    Walk(value);
                }
            }
        }

        Walk(reference);
        return string.Join(';', parts);
    }

    /// <summary>
    /// An empire carrying none of the blocks the model knows about, which is what an older file, a
    /// mod's, or a hand-edited one can look like.
    /// </summary>
    private const string Bare =
        "\"Spare\"=\r\n" +
        "{\r\n" +
        "\tkey=\"Spare\"\r\n" +
        "\tauthority=\"auth_democratic\"\r\n" +
        "}\r\n";

    /// <summary>
    /// Reading has to leave the file alone. Eight properties made the block they read from, so an
    /// empire missing one gained an empty block from being looked at — and merely listing the
    /// empires touched every design that lacked one, which is the promise the whole model exists to
    /// keep.
    /// </summary>
    [Fact]
    public void ReadingAnEmpireThatHasNoneOfTheseBlocksAddsNoneOfThem()
    {
        var file = EmpireDesignsFile.LoadText(Bare);
        var design = file.Designs[0];

        // Every property that used to make its own block on the way past.
        _ = design.ShipPrefix.Key;
        _ = design.Name.Key;
        _ = design.Adjective.Key;
        _ = design.PlanetName.Key;
        _ = design.SystemName.Key;
        _ = design.Species.Class;
        _ = design.Species.Name.Key;
        _ = design.Species.Plural.Key;
        _ = design.Species.Adjective.Key;
        _ = design.Flag.Colors;
        _ = design.Flag.Icon.File;
        _ = design.Flag.Background.File;
        _ = design.Ruler.Gender;
        _ = design.Ruler.Name.FullNames;

        Assert.Equal(Bare, file.Document.ToText());
    }

    /// <summary>
    /// And writing still makes what it needs, in the place the game would have written it.
    /// </summary>
    [Fact]
    public void WritingToOneOfThoseBlocksStillMakesIt()
    {
        var file = EmpireDesignsFile.LoadText(Bare);
        var design = file.Designs[0];

        design.Name.SetLiteral("Spare Parts");

        Assert.Equal("Spare Parts", design.Name.Key);
        Assert.True(design.Name.IsLiteral);
        Assert.Contains("Spare Parts", file.Document.ToText(), StringComparison.Ordinal);

        // Read back through a fresh parse, so this is what the game would find.
        Assert.Equal(
            "Spare Parts",
            EmpireDesignsFile.LoadText(file.Document.ToText()).Designs[0].Name.Key);
    }
}
