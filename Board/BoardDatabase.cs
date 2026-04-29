using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Board.Services;

namespace SaemDesk.Board;

/// <summary>
/// Board 정적 클래스 — NewSchool Board.cs 동등.
/// DB 경로, 파일 경로, 초기화, 팩토리.
/// </summary>
public static class BoardDatabase
{
    /// <summary>첨부파일 저장 루트</summary>
    public static string DataDir { get; } =
        Path.Combine(Settings.UserDataPath, "BoardFiles");

    /// <summary>Board DB 경로</summary>
    public static string DbPath { get; } =
        Path.Combine(Settings.UserDataPath, "board.db");

    public static BoardService CreateService()        => new(DbPath);

    // ── 초기화 ───────────────────────────────────────────

    public static async Task InitAsync()
    {
        try
        {
            Directory.CreateDirectory(DataDir);

            using var conn = new SqliteConnection($"Data Source={DbPath}");
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
            await cmd.ExecuteNonQueryAsync();

            await CreateTablesAsync(conn);
            await CreateIndexesAsync(conn);
            await MigrateAsync(conn);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BoardDatabase] 초기화 실패: {ex.Message}");
        }
    }

    private static async Task CreateTablesAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Post (
                No          INTEGER PRIMARY KEY AUTOINCREMENT,
                User        TEXT NOT NULL DEFAULT '',
                DateTime    TEXT NOT NULL DEFAULT '',
                Category    TEXT DEFAULT '',
                Subject     TEXT DEFAULT '',
                Title       TEXT NOT NULL DEFAULT '',
                Content     TEXT DEFAULT '',
                RefNo       INTEGER DEFAULT 0,
                ReplyOrder  INTEGER DEFAULT 0,
                Depth       INTEGER DEFAULT 0,
                ReadCount   INTEGER DEFAULT 0,
                HasFile     INTEGER DEFAULT 0,
                HasComment  INTEGER DEFAULT 0,
                IsCompleted INTEGER DEFAULT 0
            )";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Comment (
                No          INTEGER PRIMARY KEY AUTOINCREMENT,
                Post        INTEGER NOT NULL,
                User        TEXT NOT NULL DEFAULT '',
                DateTime    TEXT NOT NULL DEFAULT '',
                ReplyOrder  INTEGER DEFAULT 0,
                Content     TEXT DEFAULT '',
                HasFile     INTEGER DEFAULT 0,
                FileName    TEXT DEFAULT '',
                FileSize    INTEGER DEFAULT 0,
                FOREIGN KEY (Post) REFERENCES Post(No) ON DELETE CASCADE
            )";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS PostFile (
                No       INTEGER PRIMARY KEY AUTOINCREMENT,
                Post     INTEGER NOT NULL,
                DateTime TEXT NOT NULL DEFAULT '',
                FileName TEXT NOT NULL DEFAULT '',
                FileSize INTEGER DEFAULT 0,
                FOREIGN KEY (Post) REFERENCES Post(No) ON DELETE CASCADE
            )";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateIndexesAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_post_category ON Post(Category)";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_post_subject ON Post(Subject)";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_post_datetime ON Post(DateTime DESC)";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_comment_post ON Comment(Post)";
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_postfile_post ON PostFile(Post)";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task MigrateAsync(SqliteConnection conn)
    {
        // IsCompleted 컬럼 추가 (기존 DB 호환)
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE Post ADD COLUMN IsCompleted INTEGER DEFAULT 0";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException) { /* 이미 존재 */ }
    }

    // ── 파일 경로 헬퍼 ──────────────────────────────────────

    public static string GetFilePath(string fileName, string category)
        => Path.Combine(DataDir, category, fileName);

    public static void EnsureCategoryDirectory(string category)
    {
        string path = Path.Combine(DataDir, category);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }
}
