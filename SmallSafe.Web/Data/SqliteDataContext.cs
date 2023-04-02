using Microsoft.EntityFrameworkCore;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Data;

public class SqliteDataContext : DbContext, ISqliteDataContext
{
    public SqliteDataContext(DbContextOptions<SqliteDataContext> options) : base(options) { }

    public DbSet<UserAccount>? UserAccounts { get; set; }

    public void Migrate() => Database.Migrate();
}