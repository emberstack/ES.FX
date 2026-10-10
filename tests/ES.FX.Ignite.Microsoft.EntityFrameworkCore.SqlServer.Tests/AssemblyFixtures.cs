using ES.FX.Shared.SqlServer.Tests.Fixtures;

// One SQL Server container shared by every test class in this assembly. Tests that need a pristine database
// create their own on it via SqlServerContainerFixture.CreateDatabaseAsync.
[assembly: AssemblyFixture(typeof(SqlServerContainerFixture))]
