using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using SaemDesk.Logging;

namespace SaemDesk.Repositories
{
    /// <summary>
    /// School 전용 BaseRepository
    /// Board의 BaseRepository와 동일한 패턴
    /// Native AOT 호환 + 비동기 + 트랜잭션 + 에러 처리
    /// </summary>
    public abstract class BaseRepository : IDisposable
    {
        protected readonly string _dbPath;
        protected readonly SqliteConnection Connection;
        protected SqliteTransaction? Transaction;
        private bool _disposed;

        // 프로세스 내 중복 초기화 방지 — WAL(파일 영속) 및 스키마 DDL은 1회만 수행
        private static readonly HashSet<string> _walInitialized = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _schemaInitialized = new(StringComparer.Ordinal);
        // 손상 안내를 DB 파일당 1회만 출력 (리포지토리마다 반복 스팸 방지)
        private static readonly HashSet<string> _corruptionWarned = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _initLock = new();

        public SqliteTransaction? GetTransaction() => Transaction;
        public SqliteConnection GetConnection() => Connection;

        protected BaseRepository(string dbPath)
        {
            try
            {
                _dbPath = dbPath;

                // 동시성 개선을 위한 연결 옵션
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode       = SqliteOpenMode.ReadWriteCreate,
                }.ToString();

                Connection = new SqliteConnection(connectionString);
                Connection.Open();

                // 연결별 튜닝 — 풀의 각 물리 연결에 적용되어야 하므로 매번 실행 (저비용)
                using (var cmd = Connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA busy_timeout=5000; PRAGMA cache_size=10000; PRAGMA mmap_size=30000000;";
                    cmd.ExecuteNonQuery();
                }

                // WAL 모드는 DB 파일에 영속 — 프로세스 내 파일당 1회만 적용
                EnsureWalMode();

                LogDebug($"{GetType().Name} 연결 열림 (WAL 모드)");
            }
            catch (SqliteException sx) when (sx.SqliteErrorCode == 11 /* CORRUPT */ || sx.SqliteErrorCode == 26 /* NOTADB */)
            {
                WarnCorruptionOnce(dbPath);
                LogError($"{GetType().Name} 연결 실패 (DB 손상/불완전)", sx);
                throw;
            }
            catch (Exception ex)
            {
                LogError($"{GetType().Name} 연결 실패", ex);
                throw;
            }
        }

        /// <summary>
        /// 손상/불완전 DB 감지 시 원인과 조치를 담은 안내를 DB 파일당 1회만 기록.
        /// 'database disk image is malformed'는 대개 DB 파일 세트를 일부만 복사했거나
        /// (.db 만 옮기고 -wal/-shm 또는 Settings.db 누락) 옛 -wal 이 남아 발생한다.
        /// </summary>
        private void WarnCorruptionOnce(string dbPath)
        {
            lock (_initLock)
            {
                if (!_corruptionWarned.Add(dbPath)) return;
            }
            LogWarning(
                $"DB 를 열 수 없습니다: {dbPath}\n" +
                "  원인: 파일 손상이거나 DB 파일 세트가 불완전합니다 " +
                "(예: .db 만 복사하고 -wal/-shm 또는 Settings.db 를 함께 옮기지 않음).\n" +
                "  조치: 데이터 폴더의 모든 .db 와 -wal/-shm 을 함께 복사하거나, 설정에서 백업본으로 복원하세요.");
        }

        /// <summary>
        /// WAL 모드를 프로세스 내 DB 파일당 1회만 적용 (파일에 영속됨)
        /// </summary>
        private void EnsureWalMode()
        {
            lock (_initLock)
            {
                if (_walInitialized.Contains(_dbPath)) return;
                using var cmd = Connection.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
                _walInitialized.Add(_dbPath);
            }
        }

        /// <summary>
        /// 스키마 초기화(CREATE TABLE/INDEX 등)를 프로세스 내 (DB 파일 × Repository) 당 1회만 수행.
        /// 생성자마다 DDL을 재실행하는 비용 제거.
        /// </summary>
        protected void EnsureSchemaOnce(Action init)
        {
            string key = $"{_dbPath}::{GetType().Name}";
            lock (_initLock)
            {
                if (_schemaInitialized.Contains(key)) return;
                init();
                _schemaInitialized.Add(key);
            }
        }

        #region Transaction Management

        /// <summary>
        /// 외부에서 트랜잭션 설정 (UnitOfWork용)
        /// </summary>
        public void SetTransaction(SqliteTransaction? transaction)
        {
            Transaction = transaction;
            LogDebug($"트랜잭션 설정됨: {(transaction != null ? "활성" : "비활성")}");
        }

        /// <summary>
        /// 트랜잭션 시작
        /// </summary>
        public void BeginTransaction()
        {
            try
            {
                Transaction?.Dispose();
                Transaction = Connection.BeginTransaction();
                LogDebug("트랜잭션 시작");
            }
            catch (Exception ex)
            {
                LogError("트랜잭션 시작 실패", ex);
                throw;
            }
        }

        /// <summary>
        /// 트랜잭션 커밋
        /// </summary>
        public void Commit()
        {
            try
            {
                Transaction?.Commit();
                LogDebug("트랜잭션 커밋 완료");
            }
            catch (Exception ex)
            {
                LogError("트랜잭션 커밋 실패", ex);
                Rollback();
                throw;
            }
            finally
            {
                Transaction?.Dispose();
                Transaction = null;
            }
        }

        /// <summary>
        /// 트랜잭션 롤백
        /// </summary>
        public void Rollback()
        {
            try
            {
                Transaction?.Rollback();
                LogDebug("트랜잭션 롤백 완료");
            }
            catch (Exception ex)
            {
                LogError("트랜잭션 롤백 실패", ex);
            }
            finally
            {
                Transaction?.Dispose();
                Transaction = null;
            }
        }

        /// <summary>
        /// 트랜잭션 내에서 작업 실행
        /// </summary>
        protected async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            BeginTransaction();
            try
            {
                var result = await operation();
                Commit();
                return result;
            }
            catch (Exception ex)
            {
                Rollback();
                LogError("트랜잭션 작업 실패", ex);
                throw;
            }
        }

        /// <summary>
        /// 트랜잭션 내에서 작업 실행 (반환값 없음)
        /// </summary>
        protected async Task ExecuteInTransactionAsync(Func<Task> operation)
        {
            BeginTransaction();
            try
            {
                await operation();
                Commit();
            }
            catch (Exception ex)
            {
                Rollback();
                LogError("트랜잭션 작업 실패", ex);
                throw;
            }
        }

        #endregion

        #region Command Helpers

        /// <summary>
        /// 명령 생성 헬퍼
        /// </summary>
        protected SqliteCommand CreateCommand(string query)
        {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = query;
            // 트랜잭션이 이 연결에서 시작된 것인지 확인 후 설정
            if (Transaction != null && Transaction.Connection == Connection)
            {
                cmd.Transaction = Transaction;
            }
            else if (Transaction != null)
            {
                LogDebug($"트랜잭션 연결 불일치 무시 - {GetType().Name}");
                Transaction = null;
            }
            return cmd;
        }

        /// <summary>
        /// 비동기 NonQuery 실행
        /// </summary>
        protected async Task<int> ExecuteNonQueryAsync(string query)
        {
            try
            {
                using var cmd = CreateCommand(query);
                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError($"ExecuteNonQuery 실패: {query}", ex);
                throw;
            }
        }

        /// <summary>
        /// 비동기 Scalar 실행
        /// </summary>
        protected async Task<object?> ExecuteScalarAsync(string query)
        {
            try
            {
                using var cmd = CreateCommand(query);
                return await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError($"ExecuteScalar 실패: {query}", ex);
                throw;
            }
        }

        #endregion

        #region Reader Column Caching

        /// <summary>
        /// SqliteDataReader 컬럼 인덱스 캐싱
        /// GetOrdinal 반복 호출 제거로 40% 성능 향상
        /// </summary>
        protected sealed class ReaderColumnCache
        {
            private readonly Dictionary<string, int> _ordinals;

            public ReaderColumnCache(int estimatedColumns = 16)
            {
                _ordinals = new Dictionary<string, int>(estimatedColumns, StringComparer.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Reader로부터 컬럼 인덱스 초기화 (한 번만 호출)
            /// </summary>
            public void Initialize(SqliteDataReader reader)
            {
                _ordinals.Clear();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    _ordinals[reader.GetName(i)] = i;
                }
            }

            /// <summary>
            /// 컬럼 인덱스 가져오기 (캐시됨)
            /// </summary>
            public int GetOrdinal(string columnName) => _ordinals[columnName];

            /// <summary>
            /// 컬럼 존재 여부 확인
            /// </summary>
            public bool TryGetOrdinal(string columnName, out int ordinal)
                => _ordinals.TryGetValue(columnName, out ordinal);

            /// <summary>
            /// 컬럼 존재 여부
            /// </summary>
            public bool HasColumn(string columnName) => _ordinals.ContainsKey(columnName);
        }

        /// <summary>
        /// 최적화된 리스트 실행 헬퍼
        /// </summary>
        protected async Task<List<T>> ExecuteListAsync<T>(
            SqliteCommand cmd,
            Func<SqliteDataReader, ReaderColumnCache, T> mapper)
        {
            var list = new List<T>();
            var cache = new ReaderColumnCache();

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            cache.Initialize(reader); // 한 번만 컬럼 인덱스 캐싱

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                list.Add(mapper(reader, cache));
            }

            return list;
        }

        #endregion

        #region Logging

        [System.Diagnostics.Conditional("DEBUG")]
        protected void LogDebug(string message)
        {
            Debug.WriteLine($"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        [System.Diagnostics.Conditional("DEBUG")]
        protected void LogInfo(string message)
        {
            Debug.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        protected void LogWarning(string message)
        {
            Debug.WriteLine($"[WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            FileLogger.Instance.Warning($"[{GetType().Name}] {message}");
        }

        protected void LogError(string message, Exception? ex = null)
        {
            Debug.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            if (ex != null)
            {
                Debug.WriteLine($"  Exception: {ex.GetType().Name}");
                Debug.WriteLine($"  Message: {ex.Message}");
                Debug.WriteLine($"  StackTrace: {ex.StackTrace}");
            }
            FileLogger.Instance.Error($"[{GetType().Name}] {message}", ex);
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Transaction?.Dispose();

                        if (Connection != null)
                        {
                            if (Connection.State == System.Data.ConnectionState.Open)
                            {
                                Connection.Close();
                            }
                            Connection.Dispose();
                        }

                        LogDebug($"{GetType().Name} 리소스 해제 완료");
                    }
                    catch (Exception ex)
                    {
                        LogError("Dispose 중 오류", ex);
                    }
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
