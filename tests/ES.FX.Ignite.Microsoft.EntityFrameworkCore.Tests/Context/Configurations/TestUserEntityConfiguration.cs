using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context.Configurations;

public class TestUserEntityConfiguration : IEntityTypeConfiguration<TestUser>
{
    public void Configure(EntityTypeBuilder<TestUser> builder)
    {
        builder.HasKey(p => p.Id);
    }
}