using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using SaemDesk.Board.Models;
using SaemDesk.Board.Services;
using SaemDesk.Collections;

namespace SaemDesk.Board.ViewModels;

/// <summary>
/// Post 목록 ViewModel — NewSchool PostListViewModel 이식 (WinUI3 제거).
/// </summary>
public class PostListViewModel : INotifyPropertyChanged
{
    private readonly BoardService _service;
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── 컬렉션 ───────────────────────────────────────────

    private OptimizedObservableCollection<PostItemViewModel> _posts = new();
    public  OptimizedObservableCollection<PostItemViewModel> Posts
    {
        get => _posts;
        set { _posts = value; Notify(); }
    }

    // ── 필터 ─────────────────────────────────────────────

    private string _selectedCategory = "";
    public  string SelectedCategory
    {
        get => _selectedCategory;
        set { if (_selectedCategory == value) return; _selectedCategory = value; Notify(); }
    }

    private string _selectedSubject = "";
    public  string SelectedSubject
    {
        get => _selectedSubject;
        set { if (_selectedSubject == value) return; _selectedSubject = value; Notify(); }
    }

    private string _searchText = "";
    public  string SearchText  { get => _searchText;  set { _searchText = value;  Notify(); } }
    public  bool   SearchInTitle   { get; set; } = true;
    public  bool   SearchInContent { get; set; }

    // ── 상태 ─────────────────────────────────────────────

    private bool _isLoading;
    public  bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; Notify(); Notify(nameof(IsEmpty)); Notify(nameof(HasPosts)); }
    }

    public bool HasPosts => Posts.Count > 0 && !IsLoading;
    public bool IsEmpty  => Posts.Count == 0 && !IsLoading;

    // ── 페이징 ───────────────────────────────────────────

    private int _currentPage  = 1;
    private int _pageSize     = 20;
    private int _totalPages;
    private int _totalCount;

    public int CurrentPage  { get => _currentPage;  set { _currentPage  = value; Notify(); Notify(nameof(PageInfo)); } }
    public int PageSize     { get => _pageSize;     set { _pageSize     = value; Notify(); } }
    public int TotalPages   { get => _totalPages;   set { _totalPages   = value; Notify(); Notify(nameof(PageInfo)); Notify(nameof(HasPreviousPage)); Notify(nameof(HasNextPage)); } }
    public int TotalCount   { get => _totalCount;   set { _totalCount   = value; Notify(); Notify(nameof(PageInfo)); } }

    public string PageInfo        => $"{CurrentPage} / {TotalPages} 페이지 (전체 {TotalCount}개)";
    public bool   HasPreviousPage => CurrentPage > 1;
    public bool   HasNextPage     => CurrentPage < TotalPages;

    // ── 생성자 ────────────────────────────────────────────

    public PostListViewModel()
    {
        _service = BoardService.Create();
        Posts.CollectionChanged += (_, _) => { Notify(nameof(IsEmpty)); Notify(nameof(HasPosts)); };
    }

    // ── 메서드 ────────────────────────────────────────────

    public async Task LoadPostsAsync()
    {
        try
        {
            IsLoading = true;
            var result = await _service.GetPostsPagedAsync(
                CurrentPage, PageSize, SelectedCategory, SelectedSubject);

            var items = new List<PostItemViewModel>();
            foreach (var p in result.Items)
            {
                var comments = await _service.GetCommentsByPostAsync(p.No);
                items.Add(new PostItemViewModel(p, comments.Count));
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Posts.ReplaceAll(items);
                TotalPages = result.TotalPages;
                TotalCount = result.TotalCount;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PostListViewModel] LoadPosts: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SearchPostsAsync()
    {
        CurrentPage = 1;
        try
        {
            IsLoading = true;
            var result = await _service.GetPostsPagedAsync(
                CurrentPage, PageSize, SelectedCategory, SelectedSubject,
                SearchInTitle, SearchInContent, SearchText);

            var items = new List<PostItemViewModel>();
            foreach (var p in result.Items)
            {
                var comments = await _service.GetCommentsByPostAsync(p.No);
                items.Add(new PostItemViewModel(p, comments.Count));
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Posts.ReplaceAll(items);
                TotalPages = result.TotalPages;
                TotalCount = result.TotalCount;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PostListViewModel] Search: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RefreshAsync()
    {
        CurrentPage = 1;
        await LoadPostsAsync();
    }

    public async Task PreviousPageAsync()
    {
        if (!HasPreviousPage) return;
        CurrentPage--;
        await LoadPostsAsync();
    }

    public async Task NextPageAsync()
    {
        if (!HasNextPage) return;
        CurrentPage++;
        await LoadPostsAsync();
    }

    public async Task DeletePostAsync(PostItemViewModel? item)
    {
        if (item is null) return;
        await _service.DeletePostAsync(item.No, item.Category);
        await RefreshAsync();
    }

    protected void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Post UI 래퍼 — NewSchool PostItemViewModel 동등.</summary>
public class PostItemViewModel : INotifyPropertyChanged
{
    private readonly Post _post;
    private int _commentCount;
    public event PropertyChangedEventHandler? PropertyChanged;

    public PostItemViewModel(Post post, int commentCount = 0)
    {
        _post          = post;
        _commentCount  = commentCount;
    }

    public int      No              => _post.No;
    public string   User            => _post.User;
    public DateTime DateTime        => _post.DateTime;
    public string   Category        => _post.Category;
    public string   Subject         => _post.Subject;
    public string   Title           => _post.Title;
    public string   Content         => _post.Content;
    public int      ReadCount        => _post.ReadCount;
    public bool     HasFile          => _post.HasFile;
    public bool     HasComment       => _post.HasComment;
    public bool     IsCompleted      => _post.IsCompleted;
    public string   DateTimeDisplay  => _post.DateTimeDisplay;
    public Post     Post             => _post;

    public int CommentCount
    {
        get => _commentCount;
        set { _commentCount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CommentCount))); }
    }
}
