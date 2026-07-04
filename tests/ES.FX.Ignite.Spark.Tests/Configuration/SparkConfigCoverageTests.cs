using ES.FX.Ignite.Spark.Configuration;
using Microsoft.Extensions.Configuration;

namespace ES.FX.Ignite.Spark.Tests.Configuration;

/// <summary>
///     Additional coverage for <see cref="SparkConfig" /> branches not exercised by
///     <see cref="SparkConfigTests" />: the configureSettings delegate, the bind-into-existing-instance
///     overload, whitespace/trim handling in <see cref="SparkConfig.Name" /> and
///     <see cref="SparkConfig.Path" />, and the argument guard clauses.
/// </summary>
public class SparkConfigCoverageTests
{
    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] pairs)
    {
        var dictionary = pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dictionary.ToList())
            .Build();
    }

    // ---- GetSettings<T>: configureSettings delegate ----

    [Fact]
    public void GetSettings_InvokesConfigureSettingsDelegate_AndMutationsSurvive()
    {
        var configuration = BuildConfiguration(
            ("Nested:Settings:Key1", "bound1"),
            ("Nested:Settings:Key2", "bound2"));

        var invoked = false;
        var settings = SparkConfig.GetSettings<NestedSettings>(configuration, "Nested", s =>
        {
            invoked = true;
            // Patch a value post-bind and confirm the override survives on the returned instance.
            s.Key1 = "overridden";
        });

        Assert.True(invoked);
        Assert.Equal("overridden", settings.Key1);
        // The delegate only touched Key1; the bound Key2 must remain untouched.
        Assert.Equal("bound2", settings.Key2);
    }

    [Fact]
    public void GetSettings_ConfigureSettings_RunsAfterBind_SeeingBoundValues()
    {
        var configuration = BuildConfiguration(
            ("Nested:Settings:Key1", "bound1"),
            ("Nested:Settings:Key2", "bound2"));

        string? seenDuringConfigure = null;
        SparkConfig.GetSettings<NestedSettings>(configuration, "Nested",
            s => seenDuringConfigure = s.Key1);

        // If configureSettings ran before Bind, this would be the default (empty) value.
        Assert.Equal("bound1", seenDuringConfigure);
    }

    [Fact]
    public void GetSettings_NullConfigureSettings_DoesNotThrow()
    {
        var configuration = BuildConfiguration(("Nested:Settings:Key1", "bound1"));

        var settings = SparkConfig.GetSettings<NestedSettings>(configuration, "Nested");

        Assert.Equal("bound1", settings.Key1);
    }

    // ---- GetSettings<T>(T settings, ...): bind-into-existing-instance overload ----

    [Fact]
    public void GetSettings_IntoExistingInstance_ReturnsSameInstance_AndPreservesUnboundFields()
    {
        var configuration = BuildConfiguration(("Nested:Settings:Key1", "bound1"));

        var existing = new NestedSettings
        {
            Key1 = "preexisting1",
            Key2 = "preserved-because-not-in-config"
        };

        var returned = SparkConfig.GetSettings(existing, configuration, "Nested");

        // Same object identity is part of the contract of this overload.
        Assert.Same(existing, returned);
        // Key1 exists in config, so it is overwritten by the bind.
        Assert.Equal("bound1", returned.Key1);
        // Key2 is absent from config, so the pre-populated value survives.
        Assert.Equal("preserved-because-not-in-config", returned.Key2);
    }

    [Fact]
    public void GetSettings_IntoExistingInstance_AppliesConfigureSettingsDelegate()
    {
        var configuration = BuildConfiguration(("Nested:Settings:Key1", "bound1"));

        var existing = new NestedSettings();
        var returned = SparkConfig.GetSettings(existing, configuration, "Nested",
            s => s.Key2 = "set-by-delegate");

        Assert.Same(existing, returned);
        Assert.Equal("bound1", returned.Key1);
        Assert.Equal("set-by-delegate", returned.Key2);
    }

    // ---- GetSettings guard clauses ----

    [Fact]
    public void GetSettings_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SparkConfig.GetSettings<NestedSettings>(null!, "Nested"));
    }

    [Fact]
    public void GetSettings_NullConfigurationPath_Throws()
    {
        var configuration = BuildConfiguration(("Nested:Settings:Key1", "bound1"));

        Assert.Throws<ArgumentNullException>(() =>
            SparkConfig.GetSettings<NestedSettings>(configuration, null!));
    }

    [Fact]
    public void GetSettings_EmptyConfigurationPath_Throws()
    {
        var configuration = BuildConfiguration(("Nested:Settings:Key1", "bound1"));

        Assert.Throws<ArgumentException>(() =>
            SparkConfig.GetSettings<NestedSettings>(configuration, string.Empty));
    }

    [Fact]
    public void GetSettings_IntoExistingInstance_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SparkConfig.GetSettings(new NestedSettings(), null!, "Nested"));
    }

    [Fact]
    public void GetSettings_IntoExistingInstance_EmptyConfigurationPath_Throws()
    {
        var configuration = BuildConfiguration(("Nested:Settings:Key1", "bound1"));

        Assert.Throws<ArgumentException>(() =>
            SparkConfig.GetSettings(new NestedSettings(), configuration, string.Empty));
    }

    // ---- Name: whitespace-to-default + trimming ----

    [Fact]
    public void Name_UsesDefaultWhenNameIsWhitespace()
    {
        var name = SparkConfig.Name("   ", "default");

        Assert.Equal("default", name);
    }

    [Fact]
    public void Name_UsesDefaultWhenNameIsEmpty()
    {
        var name = SparkConfig.Name(string.Empty, "default");

        Assert.Equal("default", name);
    }

    [Fact]
    public void Name_TrimsProvidedName()
    {
        var name = SparkConfig.Name("  padded-name  ", "default");

        Assert.Equal("padded-name", name);
    }

    [Fact]
    public void Name_TrimsDefaultNameWhenUsed()
    {
        var name = SparkConfig.Name(null, "  padded-default  ");

        Assert.Equal("padded-default", name);
    }

    [Fact]
    public void Name_NullDefaultName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SparkConfig.Name("name", null!));
    }

    [Fact]
    public void Name_EmptyDefaultName_Throws()
    {
        Assert.Throws<ArgumentException>(() => SparkConfig.Name("name", string.Empty));
    }

    // ---- Path: null/whitespace serviceName + trimming ----

    [Fact]
    public void Path_ReturnsTrimmedSectionPath_WhenServiceNameIsNull()
    {
        var configPath = SparkConfig.Path(null, "  section  ");

        Assert.Equal("section", configPath);
    }

    [Fact]
    public void Path_ReturnsTrimmedSectionPath_WhenServiceNameIsWhitespace()
    {
        var configPath = SparkConfig.Path("   ", "  section  ");

        Assert.Equal("section", configPath);
    }

    [Fact]
    public void Path_TrimsBothServiceNameAndSectionPath_WhenCombining()
    {
        var configPath = SparkConfig.Path("  name  ", "  section  ");

        Assert.Equal("section:name", configPath);
    }

    [Fact]
    public void Path_ServiceNameOnly_WhenSectionPathIsEmptyAfterTrim()
    {
        var configPath = SparkConfig.Path("  name  ", "   ");

        Assert.Equal("name", configPath);
    }

    [Fact]
    public void Path_NullSectionPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SparkConfig.Path("name", null!));
    }

    internal class NestedSettings
    {
        public string Key1 { get; set; } = string.Empty;
        public string Key2 { get; set; } = string.Empty;
    }
}