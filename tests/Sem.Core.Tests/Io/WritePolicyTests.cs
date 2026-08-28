using Sem.Io;

namespace Sem.Core.Tests.Io;

/// <summary>
/// These tests encode the project's central safety rule: development must not be able to write
/// to the Stellaris installation or the player's real empire designs. A failure here is not a
/// style problem, it is a correctness problem with the user's data.
/// </summary>
public sealed class WritePolicyTests
{
    [Fact]
    public void DenyAll_RefusesEverything()
    {
        using var temp = new TempDirectory();

        Assert.False(WritePolicy.DenyAll.IsWritable(temp.Combine("anything.txt")));
        Assert.Throws<ForbiddenWriteException>(() => WritePolicy.DenyAll.EnsureWritable(temp.Path));
    }

    [Fact]
    public void AllowedRoot_PermitsItselfAndItsDescendants()
    {
        using var temp = new TempDirectory();
        var policy = WritePolicy.DenyAll.Allowing(temp.Path);

        Assert.True(policy.IsWritable(temp.Path));
        Assert.True(policy.IsWritable(temp.Combine("file.txt")));
        Assert.True(policy.IsWritable(temp.Combine("nested", "deeper", "file.txt")));
    }

    [Fact]
    public void PathOutsideEveryAllowedRoot_IsRefused()
    {
        using var allowed = new TempDirectory();
        using var outside = new TempDirectory();
        var policy = WritePolicy.DenyAll.Allowing(allowed.Path);

        var error = Assert.Throws<ForbiddenWriteException>(
            () => policy.EnsureWritable(outside.Combine("designs.txt")));

        Assert.Contains("outside every permitted write location", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ForbiddenRoot_BeatsAnAllowedParent()
    {
        using var temp = new TempDirectory();
        var protectedChild = temp.Combine("stellaris-install");
        Directory.CreateDirectory(protectedChild);

        var policy = WritePolicy.DenyAll.Allowing(temp.Path).Forbidding(protectedChild);

        Assert.True(policy.IsWritable(temp.Combine("scratch.txt")));
        Assert.False(policy.IsWritable(protectedChild));
        Assert.False(policy.IsWritable(Path.Combine(protectedChild, "common", "traits", "00_traits.txt")));
    }

    [Fact]
    public void SiblingWithSharedPrefix_IsNotTreatedAsInsideTheRoot()
    {
        using var temp = new TempDirectory();
        var allowed = temp.Combine("data");
        var sibling = temp.Combine("data-backup");
        Directory.CreateDirectory(allowed);
        Directory.CreateDirectory(sibling);

        var policy = WritePolicy.DenyAll.Allowing(allowed);

        Assert.True(policy.IsWritable(Path.Combine(allowed, "a.txt")));
        Assert.False(policy.IsWritable(Path.Combine(sibling, "a.txt")));
    }

    [Fact]
    public void RelativeTraversalOutOfTheAllowedRoot_IsRefused()
    {
        using var allowed = new TempDirectory();
        using var outside = new TempDirectory();
        var policy = WritePolicy.DenyAll.Allowing(allowed.Path);

        var escaped = Path.Combine(allowed.Path, "..", Path.GetFileName(outside.Path), "designs.txt");

        Assert.False(policy.IsWritable(escaped));
    }

    [Fact]
    public void TraversalThatReturnsInsideTheRoot_IsPermitted()
    {
        using var temp = new TempDirectory();
        var policy = WritePolicy.DenyAll.Allowing(temp.Path);

        var windingButInside = Path.Combine(temp.Path, "nested", "..", "file.txt");

        Assert.True(policy.IsWritable(windingButInside));
    }

    [Fact]
    public void PolicyIsImmutable_AllowingReturnsANewInstance()
    {
        using var temp = new TempDirectory();
        var original = WritePolicy.DenyAll;
        var extended = original.Allowing(temp.Path);

        Assert.NotSame(original, extended);
        Assert.False(original.IsWritable(temp.Path));
        Assert.True(extended.IsWritable(temp.Path));
    }

    [Fact]
    public void RefusalMessage_NamesThePathAndThePolicy()
    {
        using var outside = new TempDirectory();
        var policy = WritePolicy.DenyAll.Named("development (sandbox only)");

        var error = Assert.Throws<ForbiddenWriteException>(() => policy.EnsureWritable(outside.Path));

        Assert.Contains(outside.Path, error.Message, StringComparison.Ordinal);
        Assert.Contains("development (sandbox only)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyPath_IsRefusedRatherThanThrowingSomethingElse()
    {
        using var temp = new TempDirectory();
        var policy = WritePolicy.DenyAll.Allowing(temp.Path);

        Assert.False(policy.IsWritable(string.Empty));
        Assert.False(policy.IsWritable("   "));
    }

    [SkippableFact]
    [Trait("Category", "RealData")]
    public void DevelopmentPolicy_ProtectsTheRealInstallAndGameDataFolder()
    {
        var installRoot = StellarisLocator.FindInstallRoot();
        Skip.If(installRoot is null, "Stellaris is not installed on this machine.");

        var sandbox = SandboxLayout.Discover(AppContext.BaseDirectory);
        var policy = sandbox.CreateDevelopmentPolicy();

        Assert.False(policy.IsWritable(installRoot!));
        Assert.False(policy.IsWritable(Path.Combine(installRoot!, "common", "traits", "04_species_traits.txt")));
        Assert.False(policy.IsWritable(Path.Combine(installRoot!, "prescripted_countries", "00_top_countries.txt")));

        var userData = StellarisLocator.FindUserDataRoot(installRoot);
        if (userData is not null)
        {
            Assert.False(policy.IsWritable(userData));
            Assert.False(policy.IsWritable(Path.Combine(userData, "user_empire_designs_v3.4.txt")));
        }

        // The sandbox itself must remain writable, or development cannot proceed at all.
        Assert.True(policy.IsWritable(Path.Combine(sandbox.UserData, "user_empire_designs_v3.4.txt")));
    }
}
