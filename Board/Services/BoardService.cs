using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SaemDesk.Board.Models;
using SaemDesk.Board.Repositories;

namespace SaemDesk.Board.Services;

/// <summary>
/// Board 서비스 — NewSchool BoardService 이식 (WinUI3 의존성 제거).
/// Post/Comment/PostFile CRUD + 물리 파일 관리.
/// </summary>
public sealed class BoardService : IDisposable
{
    private readonly string _dbPath;
    private bool _disposed;

    public BoardService(string dbPath) { _dbPath = dbPath; }

    // ── 정적 팩토리 ──────────────────────────────────────
    public static BoardService Create() => new(BoardDatabase.DbPath);

    // ── Post ─────────────────────────────────────────────

    public async Task<int> SavePostAsync(Post post)
    {
        using var uow = new BoardUnitOfWork(_dbPath);
        return await uow.ExecuteInTransactionAsync(async () =>
        {
            if (post.No <= 0) return await uow.Posts.CreateAsync(post);
            await uow.Posts.UpdateAsync(post);
            return post.No;
        });
    }

    public async Task<Post?> GetPostAsync(int no, bool incrementReadCount = true)
    {
        using var repo = new PostRepository(_dbPath);
        var post = await repo.GetByIdAsync(no);
        if (post is not null && incrementReadCount)
            await repo.IncrementReadCountAsync(no);
        return post;
    }

    public async Task<PagedResult<Post>> GetPostsPagedAsync(
        int pageNumber, int pageSize,
        string category = "", string subject = "",
        bool searchTitle = false, bool searchContent = false, string searchText = "")
    {
        using var repo = new PostRepository(_dbPath);
        int offset = (pageNumber - 1) * pageSize;
        var (posts, total) = await repo.GetListWithCountAsync(
            pageSize, offset, category, subject, searchTitle, searchContent, searchText);
        return new PagedResult<Post>(posts, total, pageSize, pageNumber);
    }

    public async Task<bool> DeletePostAsync(int postNo, string category)
    {
        // 댓글/첨부파일 물리 파일 삭제 먼저
        using (var cr = new CommentRepository(_dbPath))
        {
            var comments = await cr.GetByPostAsync(postNo);
            foreach (var c in comments)
                if (c.HasFile && !string.IsNullOrEmpty(c.FileName))
                    DeleteFile(c.FileName, category);
        }
        using (var fr = new PostFileRepository(_dbPath))
        {
            var files = await fr.GetByPostAsync(postNo);
            foreach (var f in files) DeleteFile(f.FileName, category);
        }
        using var pr = new PostRepository(_dbPath);
        return await pr.DeleteAsync(postNo);
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        using var r = new PostRepository(_dbPath);
        return await r.GetCategoriesAsync();
    }

    public async Task<List<string>> GetSubjectsAsync(string category = "")
    {
        using var r = new PostRepository(_dbPath);
        return await r.GetSubjectsAsync(category);
    }

    // ── Comment ───────────────────────────────────────────

    public async Task<int> CreateCommentAsync(Comment comment)
    {
        using var cr = new CommentRepository(_dbPath);
        using var pr = new PostRepository(_dbPath);
        int id = await cr.CreateAsync(comment);
        if (id > 0) await pr.UpdateHasCommentAsync(comment.Post, true);
        return id;
    }

    public async Task<bool> UpdateCommentAsync(Comment comment)
    {
        using var cr = new CommentRepository(_dbPath);
        return await cr.UpdateAsync(comment);
    }

    public async Task<bool> DeleteCommentAsync(int commentNo, string category)
    {
        Comment? comment;
        int postNo;
        using (var cr = new CommentRepository(_dbPath))
        {
            comment = await cr.GetByIdAsync(commentNo);
            if (comment is null) return false;
            postNo = comment.Post;
            if (comment.HasFile && !string.IsNullOrEmpty(comment.FileName))
                DeleteFile(comment.FileName, category);
            await cr.DeleteAsync(commentNo);
        }
        using var cr2 = new CommentRepository(_dbPath);
        using var pr  = new PostRepository(_dbPath);
        int remain = await cr2.GetCountByPostAsync(postNo);
        if (remain == 0) await pr.UpdateHasCommentAsync(postNo, false);
        return true;
    }

    public async Task<List<Comment>> GetCommentsByPostAsync(int postNo)
    {
        using var cr = new CommentRepository(_dbPath);
        return await cr.GetByPostAsync(postNo);
    }

    // ── PostFile ──────────────────────────────────────────

    public async Task<int> AddPostFileAsync(PostFile pf)
    {
        using var fr = new PostFileRepository(_dbPath);
        using var pr = new PostRepository(_dbPath);
        int id = await fr.CreateAsync(pf);
        if (id > 0) await pr.UpdateHasFileAsync(pf.Post, true);
        return id;
    }

    public async Task<bool> DeletePostFileAsync(int fileNo, string category)
    {
        using var fr = new PostFileRepository(_dbPath);
        using var pr = new PostRepository(_dbPath);
        var pf = await fr.GetByIdAsync(fileNo);
        if (pf is null) return false;
        DeleteFile(pf.FileName, category);
        bool deleted = await fr.DeleteAsync(fileNo);
        if (deleted)
        {
            int remain = await fr.GetCountByPostAsync(pf.Post);
            if (remain == 0) await pr.UpdateHasFileAsync(pf.Post, false);
        }
        return deleted;
    }

    public async Task<List<PostFile>> GetPostFilesByPostAsync(int postNo)
    {
        using var fr = new PostFileRepository(_dbPath);
        return await fr.GetByPostAsync(postNo);
    }

    // ── IsCompleted ───────────────────────────────────────

    public async Task<bool> UpdatePostIsCompletedAsync(int postNo, bool v)
    {
        using var pr = new PostRepository(_dbPath);
        return await pr.UpdateIsCompletedAsync(postNo, v);
    }

    // ── 물리 파일 ─────────────────────────────────────────

    private static void DeleteFile(string fileName, string category)
    {
        try
        {
            string path = BoardDatabase.GetFilePath(fileName, category);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BoardService] 파일 삭제 실패: {fileName} {ex.Message}");
        }
    }

    // ── Dispose ───────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>페이징 결과 — NewSchool PagedResult<T> 동등.</summary>
public record PagedResult<T>(List<T> Items, int TotalCount, int PageSize, int PageNumber)
{
    public int  TotalPages      => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage     => PageNumber < TotalPages;
}
