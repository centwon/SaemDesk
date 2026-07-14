using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SaemDesk.Helpers
{
    /// <summary>
    /// 시작 시 SQLite 파일 무결성 점검. 손상('malformed')을 조용한 크래시 대신
    /// 복원 안내로 전환하기 위한 헬퍼.
    /// </summary>
    public static class DbIntegrity
    {
        /// <summary>
        /// 주어진 DB 경로들에 PRAGMA quick_check 를 실행하고 손상된 파일명 목록을 반환한다.
        /// 존재하지 않는 파일은 건너뛰고(신규 설치), 열기 자체가 실패하면 손상으로 간주한다.
        /// </summary>
        public static List<string> FindCorrupt(IEnumerable<string> dbPaths)
        {
            var corrupt = new List<string>();
            foreach (var path in dbPaths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                try
                {
                    var cs = new SqliteConnectionStringBuilder
                    {
                        DataSource = path,
                        Mode = SqliteOpenMode.ReadWrite,
                    }.ToString();

                    using var con = new SqliteConnection(cs);
                    con.Open();
                    using var cmd = con.CreateCommand();
                    cmd.CommandText = "PRAGMA quick_check;";
                    var result = cmd.ExecuteScalar() as string;
                    if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                        corrupt.Add(Path.GetFileName(path));
                }
                catch
                {
                    // 열기/검사 실패(NOTADB·CORRUPT 등) → 손상으로 처리
                    corrupt.Add(Path.GetFileName(path));
                }
            }
            return corrupt;
        }
    }
}
