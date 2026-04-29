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

/// <summary>게시판 페이지 — 목록 + 카테고리 필터 + 검색.</summary>
public partial class BoardPageVM : ViewModelBase
{
    private readonly List<Post> _allPosts = [];
    private bool _suppressFilter;

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

    public BoardPageVM()
    {
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
            string remember = SelectedCategory;
            _suppressFilter = true;
            try
            {
                Categories.Clear();
                Categories.Add("전체");
                foreach (var c in cats) Categories.Add(c);
                SelectedCategory = Categories.Contains(remember) ? remember : "전체";
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
