using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>게시판 페이지 파라미터 — NewSchool PostListPageParameter 과 동등.</summary>
public sealed class BoardPageParameter
{
    /// <summary>상단에 표시할 제목 (null 이면 기본 "게시판").</summary>
    public string? Title { get; init; }

    /// <summary>카테고리 변경 허용 여부. 아카이브는 true, 학급/수업 게시판은 false.</summary>
    public bool AllowCategoryChange { get; init; } = true;

    /// <summary>주제(Subject) 필터 표시 여부. 아카이브는 true.</summary>
    public bool ShowSubjectFilter { get; init; }

    /// <summary>특정 카테고리로 고정 (AllowCategoryChange=false 일 때 사용).</summary>
    public string? FixedCategory { get; init; }
}

/// <summary>게시판 페이지 — 목록 + 카테고리 필터 + 검색.</summary>
public partial class BoardPageVM : ViewModelBase
{
    private readonly List<Post> _allPosts = [];
    private bool _suppressFilter;

    /// <summary>현재 적용된 파라미터 — BoardPage.axaml.cs 에서 참조.</summary>
    public BoardPageParameter? Parameter { get; private set; }

    public ObservableCollection<Post>   Posts      { get; } = [];
    public ObservableCollection<string> Categories { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPosts))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Post? _selectedPost;

    [ObservableProperty] private string _searchText        = string.Empty;
    [ObservableProperty] private string _selectedCategory  = "전체";
    [ObservableProperty] private string _statusText        = string.Empty;
    [ObservableProperty] private string _errorText         = string.Empty;

    public bool HasPosts    => Posts.Count > 0;
    public bool IsEmpty     => !IsLoading && !HasPosts && string.IsNullOrEmpty(ErrorText);
    public bool HasSelection => SelectedPost is not null;

    // ── 파라미터 프로퍼티 ──────────────────────────────
    public string  PageTitle            { get; private set; } = "게시판";
    public bool    AllowCategoryChange  { get; private set; } = true;
    public bool    ShowSubjectFilter    { get; private set; }
    private string? _fixedCategory;

    // ── 기본 생성자 ────────────────────────────────────
    public BoardPageVM() : this(null) { }

    // ── 파라미터 생성자 (NewSchool WorkFrame.Navigate 파라미터 대응) ──
    public BoardPageVM(BoardPageParameter? param)
    {
        if (param is not null)
        {
            PageTitle           = param.Title ?? "게시판";
            AllowCategoryChange = param.AllowCategoryChange;
            ShowSubjectFilter   = param.ShowSubjectFilter;
            _fixedCategory      = param.FixedCategory;
        }

        Categories.Add("전체");
        _ = LoadPostsAsync();
    }

    [RelayCommand]
    private async Task LoadPostsAsync()
    {
        IsLoading = true;
        ErrorText = string.Empty;
        StatusText = "불러오는 중…";

        try
        {
            using var repo = new PostRepository(BoardDatabase.DbPath);
            var posts = await repo.GetAllAsync();
            var cats  = await repo.GetCategoriesAsync();

            _allPosts.Clear();
            _allPosts.AddRange(posts);

            // 카테고리 콤보 갱신 — 재빌드 중 OnSelectedCategoryChanged 가 중간 단계마다
            // 필터링을 호출하지 않도록 가드
            string remember = _fixedCategory ?? SelectedCategory;
            _suppressFilter = true;
            try
            {
                Categories.Clear();
                Categories.Add("전체");
                foreach (var c in cats) Categories.Add(c);

                // 고정 카테고리가 있으면 해당으로 고정, 없으면 이전 선택 복원
                SelectedCategory = (_fixedCategory is not null && Categories.Contains(_fixedCategory))
                    ? _fixedCategory
                    : Categories.Contains(remember) ? remember : "전체";
            }
            finally
            {
                _suppressFilter = false;
            }

            ApplyFilter();
            StatusText = $"총 {_allPosts.Count}건";
        }
        catch (Exception ex)
        {
            StatusText = string.Empty;
            ErrorText  = "게시글을 불러오지 못했습니다.";
            System.Diagnostics.Debug.WriteLine($"[BoardPageVM] {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasPosts));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task AddPostAsync()
    {
        bool saved = await DialogService.ShowPostEditAsync(null);
        if (saved) await LoadPostsAsync();
    }

    [RelayCommand]
    private async Task EditSelectedPostAsync()
    {
        if (SelectedPost is null) return;
        bool saved = await DialogService.ShowPostEditAsync(SelectedPost);
        if (saved) await LoadPostsAsync();
    }

    [RelayCommand]
    private async Task ViewSelectedPostAsync()
    {
        if (SelectedPost is null) return;
        await DialogService.ShowPostDetailAsync(SelectedPost.No);
        await LoadPostsAsync(); // 조회수 증가 반영
    }

    [RelayCommand]
    private async Task DeleteSelectedPostAsync()
    {
        if (SelectedPost is null) return;

        bool ok = await DialogService.ShowConfirmAsync(
            "게시글 삭제",
            $"'{SelectedPost.Title}' 게시글을 삭제하시겠습니까?\n삭제된 글은 복구할 수 없습니다.");
        if (!ok) return;

        try
        {
            using var repo = new PostRepository(BoardDatabase.DbPath);
            await repo.DeleteAsync(SelectedPost.No);
            SelectedPost = null;
            await LoadPostsAsync();
        }
        catch (Exception ex)
        {
            ErrorText = $"삭제 실패: {ex.Message}";
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (!_suppressFilter) ApplyFilter();
    }
    partial void OnSelectedCategoryChanged(string value)
    {
        if (!_suppressFilter) ApplyFilter();
    }

    private void ApplyFilter()
    {
        Posts.Clear();
        string keyword = SearchText ?? string.Empty;
        string cat = SelectedCategory ?? "전체";

        foreach (var p in _allPosts)
        {
            if (cat != "전체" && p.Category != cat) continue;
            if (!string.IsNullOrWhiteSpace(keyword)
                && !(p.Title.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
                     || p.Content.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)
                     || p.User.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)))
                continue;
            Posts.Add(p);
        }

        OnPropertyChanged(nameof(HasPosts));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
