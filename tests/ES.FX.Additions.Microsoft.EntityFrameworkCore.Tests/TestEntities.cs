using Microsoft.EntityFrameworkCore;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.Tests;

/// <summary>An entity that is NOT mapped by convention (no DbSet). It only becomes part of the model when a
/// registered configure action maps it explicitly, letting tests observe whether callbacks ran.</summary>
public class Widget
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class Gadget
{
    public int Id { get; set; }
    public string? Label { get; set; }
}

/// <summary>
///     Base test context that captures the concrete <see cref="DbContextOptions" /> passed in and replays the
///     registered configure actions during <c>OnModelCreating</c>. No DbSets are declared, so the only way an
///     entity ends up in the model is through a registered configure action.
/// </summary>
public abstract class ConfigurableContextBase(DbContextOptions options) : DbContext(options)
{
    private readonly DbContextOptions _options = options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Extensions.BuilderExtensions.ConfigureFromExtension(modelBuilder, _options);
    }
}

/// <summary>A DbContext that does NOT call ConfigureFromExtension, to prove the extension is inert unless replayed.</summary>
public class PlainDbContext(DbContextOptions options) : DbContext(options);

/// <summary>
///     EF Core caches the built model per closed <c>DbContext</c> type (the configure-extension reports a
///     provider hash of 0, so options-level differences do NOT get their own cache entry). To keep each test's
///     model isolated we key the context on a unique marker type, producing a distinct closed generic type — and
///     therefore a distinct cache entry — per test.
/// </summary>
/// <typeparam name="TMarker">A per-test marker type, used only to make the closed context type unique.</typeparam>
public class IsolatedDbContext<TMarker>(DbContextOptions options) : ConfigurableContextBase(options);
