using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmallSafe.Web.Data;

public class SqliteDataContextFactory : IDesignTimeDbContextFactory<SqliteDataContext>
{
    public SqliteDataContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteDataContext>();
        optionsBuilder.UseSqlite("Data Source=SmallSafe.Web/smallsafe.db");
        return new SqliteDataContext(optionsBuilder.Options);
    }
}