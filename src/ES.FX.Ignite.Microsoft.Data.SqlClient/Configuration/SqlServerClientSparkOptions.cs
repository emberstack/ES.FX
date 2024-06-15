using Microsoft.Data.SqlClient;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;

/// <summary>
///     Provides the options for connecting to a SQL Server database using a <see cref="SqlConnection" />
/// </summary>
public class SqlServerClientSparkOptions
{
    /// <summary>
    ///     The connection string of the SQL server database to connect to.
    /// </summary>
    public string? ConnectionString { get; set; }
}