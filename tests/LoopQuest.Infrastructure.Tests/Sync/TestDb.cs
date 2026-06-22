using LoopQuest.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LoopQuest.Infrastructure.Tests.Sync;

public static class TestDb
{
    public static (AppDbContext Db, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();   // the database lives exactly as long as this connection stays open

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();             // builds the schema straight from the model
        return (db, connection);
    }
}
