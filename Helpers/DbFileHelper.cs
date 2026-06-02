using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SaemDesk.Helpers
{
    /// <summary>
    /// SQLite 파일(본체 + WAL/SHM 사이드카) 안전 처리 헬퍼.
    /// 백업/복원 시 -wal/-shm 불일치로 인한 'database disk image is malformed' 재발 방지.
    /// </summary>
    public static class DbFileHelper
    {
        /// <summary>
        /// 백업 직전 호출: WAL 내용을 본체(.db)로 병합하고 -wal 을 비운다.
        /// 이후 .db 단일 파일만 복사해도 완전한 스냅샷이 된다.
        /// 체크포인트가 실패해도(다른 연결이 잡고 있는 등) 백업 자체는 진행하도록 예외를 삼킨다.
        /// </summary>
        public static void Checkpoint(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath)) return;
            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadWrite,
                }.ToString();

                using var con = new SqliteConnection(cs);
                con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // best-effort — 체크포인트 불가 시 무시
            }
        }

        /// <summary>
        /// 복원 직후 호출: 대상 .db 옆에 남아 있을 수 있는 옛 -wal/-shm 을 제거한다.
        /// 새로 덮어쓴 본체와 맞지 않는 고아 WAL 이 재생되어 손상으로 보고되는 것을 막는다.
        /// </summary>
        public static void DeleteSidecars(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath)) return;
            foreach (var ext in new[] { "-wal", "-shm" })
            {
                try
                {
                    string p = dbPath + ext;
                    if (File.Exists(p)) File.Delete(p);
                }
                catch
                {
                    // 삭제 실패(잠김 등) 시 무시 — 다음 시작에서 SQLite 가 정리
                }
            }
        }
    }
}
