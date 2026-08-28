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
}
