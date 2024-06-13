﻿using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;


public class TestDbContext(
    DbContextOptions<TestDbContext> dbContextOptions) :
    DbContext(dbContextOptions)
{
    public DbSet<TestUser> TestUsers { get; set; }


    public DbContextOptions<TestDbContext> Options => dbContextOptions;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(typeof(TestDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}