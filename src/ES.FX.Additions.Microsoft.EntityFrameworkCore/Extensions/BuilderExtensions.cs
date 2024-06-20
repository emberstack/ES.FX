using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;

public static class BuilderExtensions
{
    /// <summary>
    ///     Runs the actions registered in the <see cref="ModelBuilderConfigureExtension" />
    ///     on the <see cref="ModelBuilder" />.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder" /></param>
    /// <param name="options">
    ///     The <see cref="DbContextOptions" /> to load the
    ///     <see cref="ModelBuilderConfigureExtension" />> from
    /// </param>
    public static void ConfigureFromExtension(this ModelBuilder modelBuilder,
        DbContextOptions options)
    {
        var extension = options.FindExtension<ModelBuilderConfigureExtension>();
        if (extension is null) return;
        foreach (var action in extension.ConfigureActions) action.Invoke(modelBuilder, options);
    }

    /// <summary>
    ///     Registers an <see cref="ModelBuilderConfigureExtension" /> to the
    ///     <see cref="DbContextOptionsBuilder" />.
    /// </summary>
    /// <param name="builder">The <see cref="DbContextOptionsBuilder" /></param>
    /// <param name="configureActions">Actions to run on the model builder</param>
    public static void WithConfigureModelBuilderExtension(this DbContextOptionsBuilder builder,
        params Action<ModelBuilder, DbContextOptions>[] configureActions)
    {
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            new ModelBuilderConfigureExtension(configureActions));
    }
}