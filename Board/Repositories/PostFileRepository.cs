using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Board.Models;

namespace SaemDesk.Board.Repositories;

public class PostFileRepository : BoardBaseRepository
{
    public PostFileRepository(string dbPath) : base(dbPath) { }

    public async Task<int> CreateAsync(PostFile pf)
    {
        const string sql = "INSERT INTO PostFile (Post,DateTime,FileName,FileSize) VALUES (@Post,@DateTime,@FileName,@FileSize)";
        using var cmd = CreateCommand(sql);
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = pf.Post;
        cmd.Parameters.AddWithValue("@DateTime", DateTimeHelper.ToStandardString(pf.DateTime));
        cmd.Parameters.AddWithValue("@FileName", pf.FileName);
        cmd.Parameters.AddWithValue("@FileSize", pf.FileSize);
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "SELECT last_insert_rowid()";
        pf.No = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return pf.No;
    }

    public async Task<PostFile?> GetByIdAsync(int no)
    {
        using var cmd = CreateCommand("SELECT * FROM PostFile WHERE No=@No");
        cmd.Parameters.AddWithValue("@No", no);
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync()) return Map(r);
        return null;
    }

    public async Task<List<PostFile>> GetByPostAsync(int postNo)
    {
        using var cmd = CreateCommand("SELECT * FROM PostFile WHERE Post=@Post ORDER BY No ASC");
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = postNo;
        return await ExecuteListAsync(cmd, (r, c) => Map(r, c));
    }

    public async Task<int> GetCountByPostAsync(int postNo)
    {
        using var cmd = CreateCommand("SELECT COUNT(*) FROM PostFile WHERE Post=@Post");
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = postNo;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> DeleteAsync(int no)
    {
        using var cmd = CreateCommand("DELETE FROM PostFile WHERE No=@No");
        cmd.Parameters.AddWithValue("@No", no);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<int> DeleteByPostAsync(int postNo)
    {
        using var cmd = CreateCommand("DELETE FROM PostFile WHERE Post=@Post");
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = postNo;
        return await cmd.ExecuteNonQueryAsync();
    }

    private static PostFile Map(SqliteDataReader r, ReaderColumnCache? c = null)
    {
        if (c is null) { c = new ReaderColumnCache(); c.Initialize(r); }
        return new PostFile
        {
            No       = r.GetInt32(c.GetOrdinal("No")),
            Post     = r.GetInt32(c.GetOrdinal("Post")),
            DateTime = DateTimeHelper.FromDateString(r.GetString(c.GetOrdinal("DateTime"))),
            FileName = r.GetString(c.GetOrdinal("FileName")),
            FileSize = r.GetInt32(c.GetOrdinal("FileSize")),
        };
    }
}
