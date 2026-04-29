using System;
using System.Threading.Tasks;
using SaemDesk.Board.Repositories;

namespace SaemDesk.Board.Services;

/// <summary>Board UnitOfWork — PostRepository/CommentRepository/PostFileRepository를 같은 트랜잭션으로 묶음.</summary>
public sealed class BoardUnitOfWork : IDisposable
{
    private readonly string _dbPath;

    public PostRepository     Posts     { get; }
    public CommentRepository  Comments  { get; }
    public PostFileRepository PostFiles { get; }

    public BoardUnitOfWork(string dbPath)
    {
        _dbPath   = dbPath;
        Posts     = new PostRepository(dbPath);
        Comments  = new CommentRepository(dbPath);
        PostFiles = new PostFileRepository(dbPath);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> op)
    {
        Posts.BeginTransaction();
        try
        {
            var result = await op();
            Posts.Commit();
            return result;
        }
        catch
        {
            Posts.Rollback();
            throw;
        }
    }

    public void Dispose()
    {
        Posts.Dispose();
        Comments.Dispose();
        PostFiles.Dispose();
    }
}
