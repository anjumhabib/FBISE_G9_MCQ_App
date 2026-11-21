using McqExam.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace McqExam.WebAPI;

public sealed class ExamDbFactory : IDesignTimeDbContextFactory<ExamDb>
{
    public ExamDb CreateDbContext(string[] args)
    {
        var root = Directory.GetCurrentDirectory();
        var project = Path.Combine(root, "src", "McqExam.WebAPI");
        if (Directory.Exists(project)) root = project;
        var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
            .AddEnvironmentVariables().Build();
        var options = new DbContextOptionsBuilder<ExamDb>();
        DatabaseSetup.Configure(options, root, config);
        return new ExamDb(options.Options);
    }
}

public static class DatabaseSetup
{
    public static void Configure(DbContextOptionsBuilder options, string root, IConfiguration config)
    {
        var path = Path.GetFullPath(config["Database:Path"] ?? "App_Data/mcq_bank.db", root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = path, ForeignKeys = true, DefaultTimeout = 5
        };
        options.UseSqlite(connection.ToString()).AddInterceptors(new SqliteConnectionSettings());
    }

    public static async Task Initialize(ExamDb db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            if (!string.Equals(await command.ExecuteScalarAsync() as string, "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQLite could not enable WAL mode.");
            await db.Database.MigrateAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
