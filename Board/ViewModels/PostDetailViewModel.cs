using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SaemDesk.Board.Models;
using SaemDesk.Board.Services;
using SaemDesk.Collections;

namespace SaemDesk.Board.ViewModels;

/// <summary>Post 상세 ViewModel — NewSchool PostDetailViewModel 이식.</summary>
public class PostDetailViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly BoardService _service;
    public event PropertyChangedEventHandler? PropertyChanged;

    private Post?    _post;
    private bool     _isLoading;
    private string   _newCommentContent = "";
    private Comment? _editingComment;

    private OptimizedObservableCollection<Comment>  _comments = new();
    private OptimizedObservableCollection<PostFile> _files    = new();

    public Post?    Post             { get => _post;   set { _post  = value; Notify(); Notify(nameof(SubjectVisible)); } }
    public bool     IsLoading        { get => _isLoading;           set { _isLoading = value; Notify(); } }
    public string   NewCommentContent { get => _newCommentContent;  set { _newCommentContent = value; Notify(); } }
    public Comment? EditingComment   { get => _editingComment;      set { _editingComment = value; Notify(); Notify(nameof(IsEditing)); } }
    public bool     IsEditing        => EditingComment is not null;
    public bool     SubjectVisible   => Post is not null && !string.IsNullOrEmpty(Post.Subject);

    public OptimizedObservableCollection<Comment>  Comments { get => _comments; set { _comments = value; Notify(); } }
    public OptimizedObservableCollection<PostFile> Files    { get => _files;    set { _files    = value; Notify(); } }

    public PostDetailViewModel()
    {
        _service = BoardService.Create();
    }

    public async Task LoadPostAsync(int postNo)
    {
        try
        {
            IsLoading = true;
            Post = await _service.GetPostAsync(postNo, incrementReadCount: true);
            if (Post is not null)
            {
                await LoadCommentsAsync(postNo);
                await LoadFilesAsync(postNo);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[PostDetailViewModel] Load: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task LoadCommentsAsync(int postNo)
    {
        var list = await _service.GetCommentsByPostAsync(postNo);
        Comments.ReplaceAll(list);
    }

    private async Task LoadFilesAsync(int postNo)
    {
        var list = await _service.GetPostFilesByPostAsync(postNo);
        Files.ReplaceAll(list);
    }

    public async Task AddCommentAsync(string? attachedFilePath = null)
    {
        if (Post is null || string.IsNullOrWhiteSpace(NewCommentContent)) return;
        try
        {
            var comment = new Comment
            {
                Post     = Post.No,
                User     = Settings.UserName.Value.Length > 0 ? Settings.UserName.Value : "익명",
                Content  = NewCommentContent,
                DateTime = DateTime.Now,
            };

            if (!string.IsNullOrEmpty(attachedFilePath) && File.Exists(attachedFilePath))
            {
                string saved = await SaveCommentFileAsync(attachedFilePath, Post.Category);
                if (!string.IsNullOrEmpty(saved))
                {
                    comment.HasFile  = true;
                    comment.FileName = saved;
                    comment.FileSize = (int)new FileInfo(attachedFilePath).Length;
                }
            }

            int id = await _service.CreateCommentAsync(comment);
            if (id > 0)
            {
                comment.No = id;
                Comments.Insert(0, comment);
                NewCommentContent = "";
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[PostDetailViewModel] AddComment: {ex.Message}"); }
    }

    public async Task UpdateCommentAsync()
    {
        if (EditingComment is null || string.IsNullOrWhiteSpace(NewCommentContent)) return;
        try
        {
            EditingComment.Content = NewCommentContent;
            await _service.UpdateCommentAsync(EditingComment);
            if (Post is not null) await LoadCommentsAsync(Post.No);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostDetailViewModel] UpdateComment: {ex.Message}"); }
        finally { CancelEdit(); }
    }

    public async Task DeleteCommentAsync(Comment? comment)
    {
        if (comment is null || Post is null) return;
        bool ok = await _service.DeleteCommentAsync(comment.No, Post.Category);
        if (ok) Comments.Remove(comment);
    }

    public void StartEdit(Comment comment)
    {
        EditingComment    = comment;
        NewCommentContent = comment.Content;
    }

    public void CancelEdit()
    {
        EditingComment    = null;
        NewCommentContent = "";
    }

    private static async Task<string> SaveCommentFileAsync(string srcPath, string category)
    {
        try
        {
            BoardDatabase.EnsureCategoryDirectory(category);
            var ts   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var ext  = Path.GetExtension(srcPath);
            var name = $"comment_{ts}{ext}";
            var dst  = BoardDatabase.GetFilePath(name, category);
            await Task.Run(() => File.Copy(srcPath, dst, true));
            return name;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PostDetailViewModel] SaveFile: {ex.Message}");
            return string.Empty;
        }
    }

    public void Dispose() => _service.Dispose();

    protected void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
