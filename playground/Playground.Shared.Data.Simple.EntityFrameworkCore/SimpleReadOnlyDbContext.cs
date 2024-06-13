using Microsoft.EntityFrameworkCore;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore;


public class SimpleReadOnlyDbContext(DbContextOptions<SimpleDbContext> dbContextOptions) :
    SimpleDbContext(dbContextOptions);