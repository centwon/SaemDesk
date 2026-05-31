using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Models;

namespace SaemDesk.Repositories;

/// <summary>
/// 수업기록 첨부파일 메타 저장소 (school.db). 실제 파일은 디스크에 저장하고 여기엔 메타만.
/// </summary>
public class LessonLogFileRepository : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public LessonLogFileRepository(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        EnsureTableExists();
    }

    private void EnsureTableExists()
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS LessonLogFile (
                No INTEGER PRIMARY KEY AUTOINCREMENT,
                LessonLog INTEGER NOT NULL,
                FileName TEXT NOT NULL,
                FileSize INTEGER DEFAULT 0,
                CreatedAt TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_lessonlogfile_lessonlog ON LessonLogFile(LessonLog);
        ";
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.ExecuteNonQuery();
    }

    // ── 저장 경로 헬퍼 ───────────────────────────────────

    public static string FilesRoot => Path.Combine(Settings.UserDataPath, "LessonLogFiles");

    public static string GetDir(int lessonLogNo) => Path.Combine(FilesRoot, lessonLogNo.ToString());

    public static string GetFilePath(int lessonLogNo, string fileName)
        => Path.Combine(GetDir(lessonLogNo), fileName);

    public static void EnsureDir(int lessonLogNo)
    {
        var d = GetDir(lessonLogNo);
        if (!Directory.Exists(d)) Directory.CreateDirectory(d);
    }

    // ── CRUD ─────────────────────────────────────────────

    public async Task<int> CreateAsync(LessonLogFile f)
    {
        const string sql = @"
            INSERT INTO LessonLogFile (LessonLog, FileName, FileSize, CreatedAt)
            VALUES (@LessonLog, @FileName, @FileSize, @CreatedAt);
            SELECT last_insert_rowid();
        ";
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@LessonLog", f.LessonLog);
        cmd.Parameters.AddWithValue("@FileName", f.FileName);
        cmd.Parameters.AddWithValue("@FileSize", f.FileSize);
        cmd.Parameters.AddWithValue("@CreatedAt", f.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        f.No = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return f.No;
    }

    public async Task<List<LessonLogFile>> GetByLessonLogAsync(int lessonLogNo)
    {
        const string sql = "SELECT * FROM LessonLogFile WHERE LessonLog = @LessonLog ORDER BY No ASC";
        using var cmd = new SqliteCommand(sql, _connection);
        cmd.Parameters.AddWithValue("@LessonLog", lessonLogNo);

        var list = new List<LessonLogFile>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new LessonLogFile
            {
                No        = r.GetInt32(r.GetOrdinal("No")),
                LessonLog = r.GetInt32(r.GetOrdinal("LessonLog")),
                FileName  = r.GetString(r.GetOrdinal("FileName")),
                FileSize  = r.GetInt64(r.GetOrdinal("FileSize")),
            });
        }
        return list;
    }

    public async Task<int> DeleteAsync(int no)
    {
        using var cmd = new SqliteCommand("DELETE FROM LessonLogFile WHERE No = @No", _connection);
        cmd.Parameters.AddWithValue("@No", no);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteByLessonLogAsync(int lessonLogNo)
    {
        using var cmd = new SqliteCommand("DELETE FROM LessonLogFile WHERE LessonLog = @LessonLog", _connection);
        cmd.Parameters.AddWithValue("@LessonLog", lessonLogNo);
        return await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
