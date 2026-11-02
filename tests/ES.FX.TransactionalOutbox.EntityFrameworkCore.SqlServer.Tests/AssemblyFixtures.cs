using ES.FX.Shared.SqlServer.Tests.Fixtures;

// One SQL Server container shared by every test class in this assembly. Each test gets its own database on it
// via SqlServerContainerFixture.CreateDatabaseAsync.
[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]
