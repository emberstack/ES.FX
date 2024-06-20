﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Playground.Shared.Data.Simple.EntityFrameworkCore.Entities;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore.Configurations;

public class SimpleUserEntityConfiguration : IEntityTypeConfiguration<SimpleUser>
{
    public void Configure(EntityTypeBuilder<SimpleUser> builder)
    {
        builder.HasKey(p => p.Id);
    }
}