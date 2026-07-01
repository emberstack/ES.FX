using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;

/// <summary>
///     Extensions for registering and running <see cref="ModelBuilderConfigureExtension" /> actions
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    ///     Runs the actions registered in the <see cref="ModelBuilderConfigureExtension" />
    ///     on the <see cref="ModelBuilder" />.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder" /></param>
    /// <param name="options">
    ///     The <see cref="DbContextOptions" /> to load the
    ///     <see cref="ModelBuilderConfigureExtension" /> from
    /// </param>
    public static void ConfigureFromExtension(this ModelBuilder modelBuilder,
        DbContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(options);

        var extension = options.FindExtension<ModelBuilderConfigureExtension>();
        if (extension is null) return;
        foreach (var action in extension.ConfigureActions) action.Invoke(modelBuilder, options);
    }

    /// <summary>
    ///     Registers an <see cref="ModelBuilderConfigureExtension" /> to the
    ///     <see cref="DbContextOptionsBuilder" />.
    ///     Subsequent calls on the same builder append to the previously registered actions.
    /// </summary>
    /// <param name="builder">The <see cref="DbContextOptionsBuilder" /></param>
    /// <param name="configureActions">Actions to run on the model builder</param>
    public static void WithConfigureModelBuilderExtension(this DbContextOptionsBuilder builder,
        params Action<ModelBuilder, DbContextOptions>[] configureActions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureActions);

        var existingExtension = builder.Options.FindExtension<ModelBuilderConfigureExtension>();
        var actions = existingExtension is null
            ? configureActions
            : existingExtension.ConfigureActions.Concat(configureActions).ToArray();
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            new ModelBuilderConfigureExtension(actions));
    }
}