using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Board.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Board.Repositories;

public class CommentRepository : BaseRepository
{
    public CommentRepository(string dbPath) : base(dbPath) { }

    public async Task<int> CreateAsync(Comment c)
    {
        const string sql = @"
            INSERT INTO Comment (Post,User,DateTime,ReplyOrder,Content,HasFile,FileName,FileSize)
            VALUES (@Post,@User,@DateTime,@ReplyOrder,@Content,@HasFile,@FileName,@FileSize)";
        using var cmd = CreateCommand(sql);
        AddParams(cmd, c);
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = "SELECT last_insert_rowid()";
        c.No = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
        return c.No;
    }

    public async Task<Comment?> GetByIdAsync(int no)
    {
        using var cmd = CreateCommand("SELECT * FROM Comment WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = no;
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync()) return Map(r);
        return null;
    }

    public async Task<List<Comment>> GetByPostAsync(int postNo)
    {
        using var cmd = CreateCommand("SELECT * FROM Comment WHERE Post=@Post ORDER BY DateTime DESC");
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = postNo;
        return await ExecuteListAsync(cmd, (r, c) => Map(r, c));
    }

    public async Task<int> GetCountByPostAsync(int postNo)
    {
        using var cmd = CreateCommand("SELECT COUNT(*) FROM Comment WHERE Post=@Post");
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = postNo;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
    }

    public async Task<Dictionary<int, int>> GetCountsByPostsAsync(List<int> postNos)
    {
        var result = new Dictionary<int, int>();
        if (postNos.Count == 0) return result;

        var placeholders = new string[postNos.Count];
        using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        for (int i = 0; i < postNos.Count; i++)
        {
            placeholders[i] = $"@p{i}";
            cmd.Parameters.Add($"@p{i}", SqliteType.Integer).Value = postNos[i];
        }
        cmd.CommandText = $"SELECT Post, COUNT(*) FROM Comment WHERE Post IN ({string.Join(",", placeholders)}) GROUP BY Post";

        using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await r.ReadAsync().ConfigureAwait(false))
            result[r.GetInt32(0)] = r.GetInt32(1);
        return result;
    }

    public async Task<bool> UpdateAsync(Comment c)
    {
        const string sql = "UPDATE Comment SET Content=@Content,HasFile=@HasFile,FileName=@FileName,FileSize=@FileSize WHERE No=@No";
        using var cmd = CreateCommand(sql);
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = c.No;
        cmd.Parameters.AddWithValue("@Content",  c.Content);
        cmd.Parameters.Add("@HasFile", SqliteType.Integer).Value = c.HasFile ? 1 : 0;
        cmd.Parameters.AddWithValue("@FileName", c.FileName);
        cmd.Parameters.Add("@FileSize", SqliteType.Integer).Value = c.FileSize;
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int no)
    {
        using var cmd = CreateCommand("DELETE FROM Comment WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = no;
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<int> DeleteByPostAsync(int postNo)
    {
        using var cmd = CreateCommand("DELETE FROM Comment WHERE Post=@Post");
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = postNo;
        return await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParams(SqliteCommand cmd, Comment c)
    {
        cmd.Parameters.Add("@Post", SqliteType.Integer).Value = c.Post;
        cmd.Parameters.AddWithValue("@User",     c.User);
        cmd.Parameters.AddWithValue("@DateTime", DateTimeHelper.ToStandardString(c.DateTime));
        cmd.Parameters.Add("@ReplyOrder", SqliteType.Integer).Value = c.ReplyOrder;
        cmd.Parameters.AddWithValue("@Content",  c.Content);
        cmd.Parameters.Add("@HasFile", SqliteType.Integer).Value = c.HasFile ? 1 : 0;
        cmd.Parameters.AddWithValue("@FileName", c.FileName);
        cmd.Parameters.Add("@FileSize", SqliteType.Integer).Value = c.FileSize;
    }

    private static Comment Map(SqliteDataReader r, ReaderColumnCache? cache = null)
    {
        if (cache is null) { cache = new ReaderColumnCache(); cache.Initialize(r); }
        return new Comment
        {
            No         = r.GetInt32(cache.GetOrdinal("No")),
            Post       = r.GetInt32(cache.GetOrdinal("Post")),
            User       = r.IsDBNull(cache.GetOrdinal("User"))    ? "" : r.GetString(cache.GetOrdinal("User")),
            DateTime   = DateTimeHelper.FromDateString(r.GetString(cache.GetOrdinal("DateTime"))),
            ReplyOrder = r.GetInt32(cache.GetOrdinal("ReplyOrder")),
            Content    = r.IsDBNull(cache.GetOrdinal("Content"))  ? "" : r.GetString(cache.GetOrdinal("Content")),
            HasFile    = r.GetInt32(cache.GetOrdinal("HasFile"))  == 1,
            FileName   = r.IsDBNull(cache.GetOrdinal("FileName")) ? "" : r.GetString(cache.GetOrdinal("FileName")),
            FileSize   = r.GetInt32(cache.GetOrdinal("FileSize")),
        };
    }
}
