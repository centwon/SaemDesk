using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Models;

namespace SaemDesk.Repositories;

/// <summary>
/// 게시판 Post CRUD. board.db 전용.
/// </summary>
public sealed class PostRepository : BaseRepository
{
    public PostRepository(string dbPath) : base(dbPath) { }

    private const string SelectColumns =
        "No, User, DateTime, Category, Title, Content, ReadCount, IsCompleted";

    private static Post Map(SqliteDataReader r, ReaderColumnCache c) => new()
    {
        No          = r.GetInt32(c.GetOrdinal("No")),
        User        = r.IsDBNull(c.GetOrdinal("User"))     ? string.Empty : r.GetString(c.GetOrdinal("User")),
        DateTime    = ParseDate(r, c, "DateTime"),
        Category    = r.IsDBNull(c.GetOrdinal("Category")) ? string.Empty : r.GetString(c.GetOrdinal("Category")),
        Title       = r.IsDBNull(c.GetOrdinal("Title"))    ? string.Empty : r.GetString(c.GetOrdinal("Title")),
        Content     = r.IsDBNull(c.GetOrdinal("Content"))  ? string.Empty : r.GetString(c.GetOrdinal("Content")),
        ReadCount   = r.GetInt32(c.GetOrdinal("ReadCount")),
        IsCompleted = r.GetInt32(c.GetOrdinal("IsCompleted")) != 0,
    };

    private static DateTime ParseDate(SqliteDataReader r, ReaderColumnCache c, string col)
    {
        int idx = c.GetOrdinal(col);
        if (r.IsDBNull(idx)) return DateTime.Now;
        var s = r.GetString(idx);
        return DateTime.TryParse(s, out var dt) ? dt : DateTime.Now;
    }

    public async Task<List<Post>> GetAllAsync()
    {
        using var cmd = CreateCommand(
            $"SELECT {SelectColumns} FROM Post ORDER BY DateTime DESC, No DESC");
        return await ExecuteListAsync(cmd, Map);
    }

    public async Task<List<Post>> GetByCategoryAsync(string category)
    {
        using var cmd = CreateCommand(
            $"SELECT {SelectColumns} FROM Post WHERE Category = @c ORDER BY DateTime DESC, No DESC");
        cmd.Parameters.AddWithValue("@c", category);
        return await ExecuteListAsync(cmd, Map);
    }

    public async Task<Post?> GetByIdAsync(int no)
    {
        using var cmd = CreateCommand(
            $"SELECT {SelectColumns} FROM Post WHERE No = @no");
        cmd.Parameters.AddWithValue("@no", no);

        var cache = new ReaderColumnCache();
        using var reader = await cmd.ExecuteReaderAsync();
        cache.Initialize(reader);
        if (await reader.ReadAsync()) return Map(reader, cache);
        return null;
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        using var cmd = CreateCommand(
            "SELECT DISTINCT Category FROM Post WHERE Category != '' ORDER BY Category");
        var list = new List<string>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(r.IsDBNull(0) ? string.Empty : r.GetString(0));
        return list;
    }

    public async Task<int> CreateAsync(Post p)
    {
        using var cmd = CreateCommand(@"
            INSERT INTO Post (User, DateTime, Category, Title, Content, ReadCount, IsCompleted)
            VALUES (@user, @dt, @cat, @title, @content, @rc, @done);
            SELECT last_insert_rowid();");
        cmd.Parameters.AddWithValue("@user",    p.User    ?? string.Empty);
        cmd.Parameters.AddWithValue("@dt",      p.DateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@cat",     p.Category ?? string.Empty);
        cmd.Parameters.AddWithValue("@title",   p.Title    ?? string.Empty);
        cmd.Parameters.AddWithValue("@content", p.Content  ?? string.Empty);
        cmd.Parameters.AddWithValue("@rc",      p.ReadCount);
        cmd.Parameters.AddWithValue("@done",    p.IsCompleted ? 1 : 0);

        var result = await cmd.ExecuteScalarAsync();
        int newId = Convert.ToInt32(result);
        p.No = newId;
        return newId;
    }

    public async Task<bool> UpdateAsync(Post p)
    {
        using var cmd = CreateCommand(@"
            UPDATE Post SET
                User = @user,
                DateTime = @dt,
                Category = @cat,
                Title = @title,
                Content = @content,
                ReadCount = @rc,
                IsCompleted = @done
            WHERE No = @no");
        cmd.Parameters.AddWithValue("@no",      p.No);
        cmd.Parameters.AddWithValue("@user",    p.User    ?? string.Empty);
        cmd.Parameters.AddWithValue("@dt",      p.DateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@cat",     p.Category ?? string.Empty);
        cmd.Parameters.AddWithValue("@title",   p.Title    ?? string.Empty);
        cmd.Parameters.AddWithValue("@content", p.Content  ?? string.Empty);
        cmd.Parameters.AddWithValue("@rc",      p.ReadCount);
        cmd.Parameters.AddWithValue("@done",    p.IsCompleted ? 1 : 0);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int no)
    {
        using var cmd = CreateCommand("DELETE FROM Post WHERE No = @no");
        cmd.Parameters.AddWithValue("@no", no);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task IncrementReadCountAsync(int no)
    {
        using var cmd = CreateCommand("UPDATE Post SET ReadCount = ReadCount + 1 WHERE No = @no");
        cmd.Parameters.AddWithValue("@no", no);
        await cmd.ExecuteNonQueryAsync();
    }
}
