using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Board.Services;

namespace SaemDesk.Board;

/// <summary>
/// Board 시스템 — DB 경로, 스키마 초기화, 파일 경로 헬퍼, 서비스 팩토리.
/// </summary>
public static class BoardDatabase
{
    /// <summary>첨부파일 저장 루트</summary>
    public static string DataDir { get; } =
        Path.Combine(Settings.UserDataPath, "BoardFiles");

    /// <summary>Board DB 경로</summary>
    public static string DbPath { get; } =
        Path.Combine(Settings.UserDataPath, Settings.Board_DB.Value);

    public static BoardService CreateService() => new(DbPath);

    // ── 파일 경로 헬퍼 ───────────────────────────────────

    public static string GetFilePath(string fileName, string category)
        => Path.Combine(DataDir, category, fileName);

    public static void EnsureCategoryDirectory(string category)
    {
        string path = Path.Combine(DataDir, category);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    // ── DB 스키마 초기화 ─────────────────────────────────

    public static async Task InitAsync()
    {
        try
        {
            string dataDir = Path.GetDirectoryName(DbPath)!;
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            Debug.WriteLine($"[BoardDB] DB 경로: {DbPath}");

            using var conn = new SqliteConnection($"Data Source={DbPath}");
            await conn.OpenAsync();

            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";
            await pragma.ExecuteNonQueryAsync();

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

            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_post_category ON Post(Category)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_post_subject  ON Post(Subject)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_post_datetime ON Post(DateTime DESC)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_comment_post  ON Comment(Post)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_postfile_post ON PostFile(Post)";
            await cmd.ExecuteNonQueryAsync();

            Settings.Board_Inited.Set(true);
            Debug.WriteLine("[BoardDB] 초기화 완료");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BoardDB] 초기화 실패: {ex.Message}");
        }
    }
}
