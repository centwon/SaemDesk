using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SaemDesk;

/// <summary>
/// Board 데이터베이스(board.db) 초기화 — 게시글 테이블만 관리.
/// SchoolDatabase / Scheduler.cs 와 동일 패턴.
/// </summary>
public static class BoardDatabase
{
    /// <summary>전체 DB 경로.</summary>
    public static string DbPath
    {
        get
        {
            string dataDir = Settings.UserDataPath;
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);
            return Path.Combine(dataDir, Settings.Board_DB.Value);
        }
    }

    public static async Task InitAsync()
    {
        try
        {
            Debug.WriteLine($"[BoardDB] DB 경로: {DbPath}");

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            await connection.OpenAsync();

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = @"
                    PRAGMA journal_mode=WAL;
                    PRAGMA synchronous=NORMAL;
                    PRAGMA busy_timeout=5000;
                    PRAGMA foreign_keys=ON;";
                await pragma.ExecuteNonQueryAsync();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Post (
                        No INTEGER PRIMARY KEY AUTOINCREMENT,
                        User TEXT NOT NULL DEFAULT '',
                        DateTime TEXT NOT NULL DEFAULT '',
                        Category TEXT NOT NULL DEFAULT '',
                        Title TEXT NOT NULL DEFAULT '',
                        Content TEXT NOT NULL DEFAULT '',
                        ReadCount INTEGER NOT NULL DEFAULT 0,
                        IsCompleted INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE INDEX IF NOT EXISTS idx_post_category ON Post(Category);
                    CREATE INDEX IF NOT EXISTS idx_post_datetime ON Post(DateTime DESC);";
                await cmd.ExecuteNonQueryAsync();
            }

            Settings.Board_Inited.Set(true);
            Debug.WriteLine("[BoardDB] 초기화 완료");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BoardDB] 초기화 실패: {ex.Message}");
        }
    }
}
