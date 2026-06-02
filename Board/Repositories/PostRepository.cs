using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Board.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Board.Repositories;

/// <summary>Post 리포지토리 — NewSchool Board PostRepository 이식.</summary>
public class PostRepository : BaseRepository
{
    public PostRepository(string dbPath) : base(dbPath) { }

    // ── Create ────────────────────────────────────────────

    public async Task<int> CreateAsync(Post post)
    {
        const string sql = @"
            INSERT INTO Post (User,DateTime,Category,Subject,Title,Content,ContentArdx,
                              RefNo,ReplyOrder,Depth,ReadCount,HasFile,HasComment,IsCompleted)
            VALUES (@User,@DateTime,@Category,@Subject,@Title,@Content,@ContentArdx,
                   @RefNo,@ReplyOrder,@Depth,@ReadCount,@HasFile,@HasComment,@IsCompleted);
            SELECT last_insert_rowid();";
        using var cmd = CreateCommand(sql);
        AddParams(cmd, post);
        var r = await cmd.ExecuteScalarAsync();
        post.No = Convert.ToInt32(r ?? 0);
        LogInfo($"Post 생성: No={post.No}");
        return post.No;
    }

    // ── Read ──────────────────────────────────────────────

    public async Task<Post?> GetByIdAsync(int no)
    {
        using var cmd = CreateCommand("SELECT * FROM Post WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = no;
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return Map(reader);
        return null;
    }

    public async Task<(List<Post> Posts, int TotalCount)> GetListWithCountAsync(
        int limit, int offset,
        string category = "", string subject = "",
        bool searchTitle = false, bool searchContent = false, string searchText = "")
    {
        var posts = new List<Post>();
        int total = 0;

        string sql = "SELECT No,User,DateTime,Category,Subject,Title,Content,RefNo,ReplyOrder,Depth,ReadCount,HasFile,HasComment,IsCompleted,COUNT(*) OVER() AS TotalCount FROM Post WHERE 1=1";
        using var cmd = CreateCommand(sql);

        if (!string.IsNullOrEmpty(category))   { sql += " AND Category=@Category"; cmd.Parameters.AddWithValue("@Category", category); }
        if (!string.IsNullOrEmpty(subject))    { sql += " AND Subject=@Subject";   cmd.Parameters.AddWithValue("@Subject",  subject);  }
        if (!string.IsNullOrEmpty(searchText))
        {
            if (searchTitle && searchContent)  sql += " AND (Title LIKE @S OR Content LIKE @S)";
            else if (searchTitle)              sql += " AND Title LIKE @S";
            else if (searchContent)            sql += " AND Content LIKE @S";
            if (searchTitle || searchContent)  cmd.Parameters.AddWithValue("@S", $"%{searchText}%");
        }

        sql += " ORDER BY No DESC";
        if (limit > 0)   { sql += " LIMIT @L";  cmd.Parameters.Add("@L", SqliteType.Integer).Value = limit;  }
        if (offset > 0)  { sql += " OFFSET @O"; cmd.Parameters.Add("@O", SqliteType.Integer).Value = offset; }
        cmd.CommandText = sql;

        var cache = new ReaderColumnCache();
        using var reader = await cmd.ExecuteReaderAsync();
        cache.Initialize(reader);
        while (await reader.ReadAsync())
        {
            posts.Add(Map(reader, cache));
            if (total == 0) total = reader.GetInt32(cache.GetOrdinal("TotalCount"));
        }
        return (posts, total);
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        using var cmd = CreateCommand("SELECT DISTINCT Category FROM Post WHERE Category IS NOT NULL AND Category!='' ORDER BY Category");
        var list = new List<string>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    public async Task<List<string>> GetSubjectsAsync(string category = "")
    {
        string sql = "SELECT DISTINCT Subject FROM Post WHERE Subject IS NOT NULL AND Subject!=''";
        using var cmd = CreateCommand(sql);
        if (!string.IsNullOrEmpty(category)) { sql += " AND Category=@C"; cmd.Parameters.AddWithValue("@C", category); }
        cmd.CommandText = sql + " ORDER BY Subject";
        var list = new List<string>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    public async Task<List<Post>> GetAllAsync()
    {
        using var cmd = CreateCommand(
            "SELECT * FROM Post ORDER BY DateTime DESC, No DESC");
        return await ExecuteListAsync(cmd, (r, c) => Map(r, c));
    }

    public async Task<List<Post>> GetByCategoryAsync(string category)
    {
        using var cmd = CreateCommand(
            "SELECT * FROM Post WHERE Category=@C ORDER BY DateTime DESC, No DESC");
        cmd.Parameters.AddWithValue("@C", category);
        return await ExecuteListAsync(cmd, (r, c) => Map(r, c));
    }

    // ── Update ────────────────────────────────────────────

    public async Task<bool> UpdateAsync(Post post)
    {
        const string sql = @"UPDATE Post SET User=@User,DateTime=@DateTime,Category=@Category,
            Subject=@Subject,Title=@Title,Content=@Content,ContentArdx=@ContentArdx,RefNo=@RefNo,ReplyOrder=@ReplyOrder,
            Depth=@Depth,ReadCount=@ReadCount,HasFile=@HasFile,HasComment=@HasComment,
            IsCompleted=@IsCompleted WHERE No=@No";
        using var cmd = CreateCommand(sql);
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = post.No;
        AddParams(cmd, post);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdateHasFileAsync(int postNo, bool v)
    {
        using var cmd = CreateCommand("UPDATE Post SET HasFile=@V WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = postNo;
        cmd.Parameters.Add("@V",  SqliteType.Integer).Value = v ? 1 : 0;
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdateHasCommentAsync(int postNo, bool v)
    {
        using var cmd = CreateCommand("UPDATE Post SET HasComment=@V WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = postNo;
        cmd.Parameters.Add("@V",  SqliteType.Integer).Value = v ? 1 : 0;
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdateIsCompletedAsync(int postNo, bool v)
    {
        using var cmd = CreateCommand("UPDATE Post SET IsCompleted=@V WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = postNo;
        cmd.Parameters.Add("@V",  SqliteType.Integer).Value = v ? 1 : 0;
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> IncrementReadCountAsync(int postNo)
    {
        using var cmd = CreateCommand("UPDATE Post SET ReadCount=ReadCount+1 WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = postNo;
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Delete ────────────────────────────────────────────

    public async Task<List<Post>> GetForMemoAsync(string category = "", string subject = "")
    {
        string sql = "SELECT * FROM Post WHERE 1=1";
        using var cmd = CreateCommand(sql);
        if (!string.IsNullOrEmpty(category))
        {
            cmd.CommandText += " AND Category=@Category";
            cmd.Parameters.AddWithValue("@Category", category);
        }
        if (!string.IsNullOrEmpty(subject))
        {
            cmd.CommandText += " AND Subject=@Subject";
            cmd.Parameters.AddWithValue("@Subject", subject);
        }
        cmd.CommandText += " ORDER BY DateTime DESC";
        return await ExecuteListAsync(cmd, (r, c) => Map(r, c));
    }

    public async Task<bool> DeleteAsync(int postNo)
    {
        using var cmd = CreateCommand("DELETE FROM Post WHERE No=@No");
        cmd.Parameters.Add("@No", SqliteType.Integer).Value = postNo;
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── Helpers ───────────────────────────────────────────

    private static void AddParams(SqliteCommand cmd, Post p)
    {
        cmd.Parameters.AddWithValue("@User",     p.User);
        cmd.Parameters.AddWithValue("@DateTime", DateTimeHelper.ToStandardString(p.DateTime));
        cmd.Parameters.AddWithValue("@Category", p.Category);
        cmd.Parameters.AddWithValue("@Subject",  p.Subject);
        cmd.Parameters.AddWithValue("@Title",    p.Title);
        cmd.Parameters.AddWithValue("@Content",  p.Content);
        cmd.Parameters.Add("@ContentArdx", SqliteType.Blob).Value = (object?)p.ContentArdx ?? DBNull.Value;
        cmd.Parameters.Add("@RefNo",       SqliteType.Integer).Value = p.RefNo;
        cmd.Parameters.Add("@ReplyOrder",  SqliteType.Integer).Value = p.ReplyOrder;
        cmd.Parameters.Add("@Depth",       SqliteType.Integer).Value = p.Depth;
        cmd.Parameters.Add("@ReadCount",   SqliteType.Integer).Value = p.ReadCount;
        cmd.Parameters.Add("@HasFile",     SqliteType.Integer).Value = p.HasFile     ? 1 : 0;
        cmd.Parameters.Add("@HasComment",  SqliteType.Integer).Value = p.HasComment  ? 1 : 0;
        cmd.Parameters.Add("@IsCompleted", SqliteType.Integer).Value = p.IsCompleted ? 1 : 0;
    }

    private static Post Map(SqliteDataReader r, ReaderColumnCache? c = null)
    {
        if (c is null) { c = new ReaderColumnCache(); c.Initialize(r); }
        var p = new Post
        {
            No         = r.GetInt32(c.GetOrdinal("No")),
            User       = r.GetString(c.GetOrdinal("User")),
            DateTime   = DateTimeHelper.FromDateString(r.GetString(c.GetOrdinal("DateTime"))),
            Category   = r.IsDBNull(c.GetOrdinal("Category"))  ? "" : r.GetString(c.GetOrdinal("Category")),
            Subject    = r.IsDBNull(c.GetOrdinal("Subject"))   ? "" : r.GetString(c.GetOrdinal("Subject")),
            Title      = r.GetString(c.GetOrdinal("Title")),
            Content    = r.IsDBNull(c.GetOrdinal("Content"))   ? "" : r.GetString(c.GetOrdinal("Content")),
            RefNo      = r.GetInt32(c.GetOrdinal("RefNo")),
            ReplyOrder = r.GetInt32(c.GetOrdinal("ReplyOrder")),
            Depth      = r.GetInt32(c.GetOrdinal("Depth")),
            ReadCount  = r.GetInt32(c.GetOrdinal("ReadCount")),
            HasFile    = r.GetInt32(c.GetOrdinal("HasFile"))    == 1,
            HasComment = r.GetInt32(c.GetOrdinal("HasComment")) == 1,
        };
        if (c.TryGetOrdinal("IsCompleted", out int co) && !r.IsDBNull(co))
            p.IsCompleted = r.GetInt32(co) == 1;
        // ContentArdx 는 SELECT * 경로(단일 글 조회 등)에서만 채워진다. 목록 쿼리는 컬럼 미선택.
        if (c.TryGetOrdinal("ContentArdx", out int ao) && !r.IsDBNull(ao))
            p.ContentArdx = r.GetFieldValue<byte[]>(ao);
        return p;
    }
}
