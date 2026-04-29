using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Logging;

namespace SaemDesk.Board.Repositories;

/// <summary>
/// Board Repository 기반 클래스 — NewSchool Board BaseRepository 이식.
/// SaemDesk의 메인 BaseRepository 와 독립적으로 Board 전용으로 운영.
/// </summary>
public abstract class BoardBaseRepository : IDisposable
{
    protected readonly string _dbPath;
    protected readonly SqliteConnection Connection;
    protected SqliteTransaction? Transaction;
    private bool _disposed;

    public SqliteTransaction? GetTransaction() => Transaction;
    public SqliteConnection   GetConnection()  => Connection;

    protected BoardBaseRepository(string dbPath)
    {
        _dbPath = dbPath;
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode       = SqliteOpenMode.ReadWriteCreate,
            Cache      = SqliteCacheMode.Shared,
            Pooling    = true,
        }.ToString();

        Connection = new SqliteConnection(cs);
        Connection.Open();

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA cache_size=10000;";
        cmd.ExecuteNonQuery();

        LogDebug($"{GetType().Name} 연결 열림");
    }

    // ── 트랜잭션 ──────────────────────────────────────────

    public void SetTransaction(SqliteTransaction? tx) => Transaction = tx;

    public void BeginTransaction()
    {
        Transaction?.Dispose();
        Transaction = Connection.BeginTransaction();
    }

    public void Commit()
    {
        try   { Transaction?.Commit(); }
        catch { Rollback(); throw; }
        finally { Transaction?.Dispose(); Transaction = null; }
    }

    public void Rollback()
    {
        try   { Transaction?.Rollback(); }
        finally { Transaction?.Dispose(); Transaction = null; }
    }

    protected async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> op)
    {
        BeginTransaction();
        try   { var r = await op(); Commit(); return r; }
        catch { Rollback(); throw; }
    }

    // ── Command ───────────────────────────────────────────

    protected SqliteCommand CreateCommand(string sql)
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        if (Transaction is not null) cmd.Transaction = Transaction;
        return cmd;
    }

    protected async Task<int>     ExecuteNonQueryAsync(string sql)
    {
        using var cmd = CreateCommand(sql);
        return await cmd.ExecuteNonQueryAsync();
    }

    protected async Task<object?> ExecuteScalarAsync(string sql)
    {
        using var cmd = CreateCommand(sql);
        return await cmd.ExecuteScalarAsync();
    }

    // ── 컬럼 캐시 ─────────────────────────────────────────

    protected sealed class ReaderColumnCache
    {
        private readonly Dictionary<string, int> _ord = new(StringComparer.OrdinalIgnoreCase);

        public void Initialize(SqliteDataReader r)
        {
            _ord.Clear();
            for (int i = 0; i < r.FieldCount; i++) _ord[r.GetName(i)] = i;
        }

        public int  GetOrdinal(string col)                         => _ord[col];
        public bool TryGetOrdinal(string col, out int ord)         => _ord.TryGetValue(col, out ord);
    }

    protected async Task<List<T>> ExecuteListAsync<T>(
        SqliteCommand cmd,
        Func<SqliteDataReader, ReaderColumnCache, T> mapper)
    {
        var list  = new List<T>();
        var cache = new ReaderColumnCache();
        using var reader = await cmd.ExecuteReaderAsync();
        cache.Initialize(reader);
        while (await reader.ReadAsync()) list.Add(mapper(reader, cache));
        return list;
    }

    // ── 로깅 ──────────────────────────────────────────────

    [Conditional("DEBUG")]
    protected void LogDebug(string msg) => Debug.WriteLine($"[Board DEBUG] {msg}");

    [Conditional("DEBUG")]
    protected void LogInfo(string msg)  => Debug.WriteLine($"[Board INFO] {msg}");

    protected void LogWarning(string msg) => Debug.WriteLine($"[Board WARNING] {msg}");

    protected void LogError(string msg, Exception? ex = null)
    {
        Debug.WriteLine($"[Board ERROR] {msg}");
        if (ex is not null) Debug.WriteLine($"  {ex.Message}");
        FileLogger.Instance.Error($"[Board.{GetType().Name}] {msg}", ex);
    }

    // ── Dispose ───────────────────────────────────────────

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            Transaction?.Dispose();
            if (Connection.State == System.Data.ConnectionState.Open) Connection.Close();
            Connection.Dispose();
        }
        _disposed = true;
    }
}
