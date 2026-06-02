using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Board.Services;
using SaemDesk.Helpers;

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
                    ContentArdx BLOB,
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

            // 기존 DB: ContentArdx BLOB 컬럼이 없으면 추가 (구조 마이그레이션)
            if (!await ColumnExistsAsync(conn, "Post", "ContentArdx"))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = "ALTER TABLE Post ADD COLUMN ContentArdx BLOB";
                await alter.ExecuteNonQueryAsync();
                Debug.WriteLine("[BoardDB] ContentArdx 컬럼 추가");
            }

            Settings.Board_Inited.Set(true);
            Debug.WriteLine("[BoardDB] 초기화 완료");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BoardDB] 초기화 실패: {ex.Message}");
        }
    }

    // ── ardx 마이그레이션 (1회성) ────────────────────────

    /// <summary>
    /// 구버전 게시글의 Content(HTML)을 ardx BLOB + 검색용 plaintext 로 1회 변환한다.
    /// 플래그로 게이트하며, 변환 전 board.db 를 백업한다.
    /// 이미지 디코딩 세션이 필요하므로 변환·기록은 UI 스레드에서 일괄 수행한다.
    /// 반드시 <see cref="InitAsync"/>(컬럼 추가) 이후, 앱 프레임워크 초기화 완료 후 호출.
    /// </summary>
    public static async Task MigrateContentToArdxAsync()
    {
        try
        {
            if (Settings.Board_ArdxMigrated.Value) return;

            // 1. 변환 대상 수집 (ardx 없음 + Content 있음)
            var pending = new System.Collections.Generic.List<(int No, string Html)>();
            using (var conn = new SqliteConnection($"Data Source={DbPath}"))
            {
                await conn.OpenAsync();
                using var sel = conn.CreateCommand();
                sel.CommandText = "SELECT No, Content FROM Post WHERE ContentArdx IS NULL AND Content IS NOT NULL AND length(Content) > 0";
                using var r = sel.ExecuteReader();
                while (r.Read()) pending.Add((r.GetInt32(0), r.GetString(1)));
            }

            if (pending.Count == 0) { Settings.Board_ArdxMigrated.Set(true); return; }

            // 2. 백업 (체크포인트 후 .db 단일 스냅샷 복사)
            DbFileHelper.Checkpoint(DbPath);
            string backupDir = Path.Combine(Settings.UserDataPath, "Backups", $"pre_ardx_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupDir);
            File.Copy(DbPath, Path.Combine(backupDir, Path.GetFileName(DbPath)), overwrite: true);
            Debug.WriteLine($"[BoardDB] 마이그레이션 백업: {backupDir}");

            // 3. 변환 + 기록 — UI 스레드에서 동기 일괄 (이미지 디코딩 세션 필요)
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var conn = new SqliteConnection($"Data Source={DbPath}");
                conn.Open();
                using var tx = conn.BeginTransaction();
                using var upd = conn.CreateCommand();
                upd.CommandText = "UPDATE Post SET ContentArdx=@a, Content=@c WHERE No=@no";
                var pA  = upd.Parameters.Add("@a",  SqliteType.Blob);
                var pC  = upd.Parameters.Add("@c",  SqliteType.Text);
                var pNo = upd.Parameters.Add("@no", SqliteType.Integer);
                foreach (var (no, html) in pending)
                {
                    var (ardx, plain) = RichContent.FromHtml(html);
                    pA.Value  = ardx;
                    pC.Value  = plain;
                    pNo.Value = no;
                    upd.ExecuteNonQuery();
                }
                tx.Commit();
            });

            Settings.Board_ArdxMigrated.Set(true);
            Debug.WriteLine($"[BoardDB] ardx 마이그레이션 완료: {pending.Count}건");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BoardDB] ardx 마이그레이션 실패: {ex.Message}");
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
