using Microsoft.EntityFrameworkCore;

namespace ApiServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}