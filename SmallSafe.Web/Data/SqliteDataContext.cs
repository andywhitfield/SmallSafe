using Microsoft.EntityFrameworkCore;

namespace SmallSafe.Web.Data;

public class SqliteDataContext : DbContext, ISqliteDataContext
{
    public SqliteDataContext(DbContextOptions<SqliteDataContext> options) : base(options) { }

    public void Migrate() => Database.Migrate();
}