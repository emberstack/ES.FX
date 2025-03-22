using JetBrains.Annotations;
using Microsoft.Data.SqlClient;

namespace ES.FX.Extensions.Microsoft.Data.SqlClient;

[PublicAPI]
public static class SqlConnectionStringBuilderExtensions
{
    /// <summary>
    ///     Creates a new instance of <see cref="SqlConnectionStringBuilder" /> with
    ///     <see cref="SqlConnectionStringBuilder.InitialCatalog" /> set to "master"
    /// </summary>
    /// <param name="builder"></param>
    /// <returns>The cloned <see cref="SqlConnectionStringBuilder" /></returns>
    public static SqlConnectionStringBuilder CloneForMaster(this SqlConnectionStringBuilder builder) =>
        new SqlConnectionStringBuilder(builder.ConnectionString).SetInitialCatalogToMaster();


    /// <summary>
    ///     Sets the InitialCatalog to
    ///     <param name="database"></param>
    /// </summary>
    public static SqlConnectionStringBuilder SetInitialCatalog(this SqlConnectionStringBuilder builder,
        string database)
    {
        builder.InitialCatalog = database;
        return builder;
    }

    /// <summary>
    ///     Changes the InitialCatalog to the "master" database
    /// </summary>
    public static SqlConnectionStringBuilder SetInitialCatalogToMaster(this SqlConnectionStringBuilder builder) =>
        builder.SetInitialCatalog("master");
}