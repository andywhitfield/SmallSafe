using Microsoft.EntityFrameworkCore;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Data;

public class SqliteDataContext(DbContextOptions<SqliteDataContext> options) : DbContext(options), ISqliteDataContext
{
    public DbSet<UserAccount>? UserAccounts { get; set; }
    public DbSet<UserAccountCredential>? UserAccountCredentials { get; set; }

    public void Migrate() => Database.Migrate();
}