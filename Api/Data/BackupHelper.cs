using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PokeChat.Data;

public static class BackupHelper
{
    public static string GetBackupPath(string dbPath) => dbPath + ".bak";

    public static void Backup(string dbPath)
    {
        if (!File.Exists(dbPath)) return;

        var bakPath = GetBackupPath(dbPath);
        File.Copy(dbPath, bakPath, overwrite: true);
    }

    public static bool Restore(string dbPath)
    {
        var bakPath = GetBackupPath(dbPath);
        if (!File.Exists(bakPath)) return false;

        if (File.Exists(dbPath))
            File.Delete(dbPath);

        File.Copy(bakPath, dbPath);
        return true;
    }

    public static void CopyData(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath)) return;

        using var sourceConn = new SqliteConnection($"Data Source={sourcePath}");
        sourceConn.Open();

        using var targetConn = new SqliteConnection($"Data Source={targetPath}");
        targetConn.Open();

        var tables = GetTables(targetConn);
        foreach (var table in tables)
        {
            var columns = GetColumns(sourceConn, table).Intersect(GetColumns(targetConn, table)).ToList();
            if (columns.Count == 0) continue;

            var colList = string.Join(", ", columns.Select(c => $"\"{c}\""));
            using var cmd = targetConn.CreateCommand();
            cmd.CommandText = $"ATTACH DATABASE @src AS old_db";
            cmd.Parameters.AddWithValue("@src", sourcePath);
            cmd.ExecuteNonQuery();

            cmd.CommandText = $"INSERT OR IGNORE INTO \"{table}\" ({colList}) SELECT {colList} FROM old_db.\"{table}\"";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "DETACH DATABASE old_db";
            cmd.ExecuteNonQuery();
        }
    }

    private static List<string> GetTables(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory'";
        var tables = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private static List<string> GetColumns(SqliteConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }
}
