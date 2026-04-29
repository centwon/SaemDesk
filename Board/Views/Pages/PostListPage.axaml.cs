using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.Board.Models;
using SaemDesk.Board.Services;
using SaemDesk.Board.ViewModels;

namespace SaemDesk.Board.Views.Pages;

/// <summary>
/// 게시글 목록 페이지 — NewSchool PostListPage 이식 (Frame 네비게이션 → 이벤트 기반).
/// 표/카드/갤러리 뷰 모드 지원, 카테고리/주제 필터, 페이징.
/// </summary>
public partial class PostListPage : UserControl
{
    public PostListViewModel ViewModel { get; }

    private PostListPageParameter? _param;
    private BoardViewMode _viewMode = BoardViewMode.Table;

    // ── 이벤트 (Frame 대신 이벤트로 네비게이션) ──────────
    public event EventHandler<int>?                    PostSelected;   // PostNo
    public event EventHandler<PostEditPageParameter>?  NewPostRequested;

    // ── 기본 카테고리 / 주제 제안 ────────────────────────
    private static readonly List<string> DefaultCategories = new()
        { "업무", "수업", "학급", "동아리", "개인", "기타" };

    private static readonly Dictionary<string, List<string>> DefaultTopics = new()
    {
        ["학급"] = new() { "통계", "학급 자료", "학생 자료", "학급 안내" },
        ["수업"] = new() { "통계", "수업 자료", "과제" },
        ["동아리"] = new() { "통계", "동아리 자료", "활동 안내" },
    };

    public PostListPage()
    {
        InitializeComponent();
        ViewModel = new PostListViewModel();
        DataContext = ViewModel;

        PostsRepeater.ItemsSource   = ViewModel.Posts;
        CardViewRepeater.ItemsSource = ViewModel.Posts;

        Loaded += OnLoaded;
    }

    // ── 초기화 ────────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await InitAsync();
    }

    public async Task InitAsync(PostListPageParameter? param = null)
    {
        _param = param;
        ApplyParameter();
        await InitCategoriesAsync();
        await ViewModel.LoadPostsAsync();
    }

    private void ApplyParameter()
    {
        if (_param is null) return;

        // 제목
        if (!string.IsNullOrEmpty(_param.Title))
            TitleText.Text = _param.Title;

        TitlePanel.IsVisible = !_param.IsEmbedded;

        // 카테고리/주제 고정
        ViewModel.SelectedCategory = _param.Category ?? "";
        ViewModel.SelectedSubject  = _param.Subject  ?? "";

        // 카테고리 콤보 표시
        CBoxCategory.IsVisible = _param.AllowCategoryChange;

        // 주제 필터
        if (_param.ShowSubjectFilter)
        {
            CBoxSubject.IsVisible = true;
            _ = InitSubjectFilterAsync();
        }

        // 뷰 모드
        if (_param.ViewMode != BoardViewMode.Default)
            ApplyViewMode(_param.ViewMode);

        BtnViewMode.IsVisible = _param.AllowViewModeChange;
    }

    private async Task InitCategoriesAsync()
    {
        CBoxCategory.Items.Clear();
        CBoxCategory.Items.Add("전체");

        try
        {
            using var svc = BoardService.Create();
            var cats = await svc.GetCategoriesAsync();
            foreach (var c in cats) CBoxCategory.Items.Add(c);
            foreach (var d in DefaultCategories)
                if (!CBoxCategory.Items.Contains(d)) CBoxCategory.Items.Add(d);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostListPage] 카테고리: {ex.Message}"); }

        CBoxCategory.SelectedIndex = 0;
    }

    private async Task InitSubjectFilterAsync()
    {
        CBoxSubject.Items.Clear();
        CBoxSubject.Items.Add("전체");

        try
        {
            var cat = _param?.Category ?? "";
            if (DefaultTopics.TryGetValue(cat, out var defaults))
                foreach (var t in defaults) CBoxSubject.Items.Add(t);

            using var svc = BoardService.Create();
            var subjects = await svc.GetSubjectsAsync(cat);
            foreach (var s in subjects)
                if (!CBoxSubject.Items.Contains(s)) CBoxSubject.Items.Add(s);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostListPage] 주제: {ex.Message}"); }

        CBoxSubject.SelectedIndex = 0;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxCategory.SelectedItem is not string cat) return;
        ViewModel.SelectedCategory = (cat == "전체") ? "" : cat;
        ViewModel.CurrentPage = 1;

        // 주제 필터 갱신
        if (!string.IsNullOrEmpty(ViewModel.SelectedCategory))
        {
            if (_param is null) _param = new PostListPageParameter { Category = ViewModel.SelectedCategory };
            else _param.Category = ViewModel.SelectedCategory;
            CBoxSubject.IsVisible = true;
            await InitSubjectFilterAsync();
        }
        else
        {
            CBoxSubject.IsVisible = false;
            ViewModel.SelectedSubject = "";
        }

        await ViewModel.LoadPostsAsync();
    }

    private async void OnSubjectChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxSubject.SelectedItem is not string sub) return;
        ViewModel.SelectedSubject = (sub == "전체") ? "" : sub;
        await ViewModel.LoadPostsAsync();
    }

    private async void BtnSearch_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.SearchPostsAsync();

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.RefreshAsync();

    private async void BtnPrev_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.PreviousPageAsync();

    private async void BtnNext_Click(object? sender, RoutedEventArgs e)
        => await ViewModel.NextPageAsync();

    private void BtnNewPost_Click(object? sender, RoutedEventArgs e)
    {
        NewPostRequested?.Invoke(this, new PostEditPageParameter
        {
            DefaultCategory   = _param?.Category ?? "",
            DefaultSubject    = _param?.Subject  ?? "",
            AllowCategoryChange = _param?.AllowCategoryChange ?? true,
        });
    }

    private void OnPostTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid g && g.DataContext is PostItemViewModel item)
            PostSelected?.Invoke(this, item.No);
    }

    // ── 뷰 모드 ───────────────────────────────────────────

    private void BtnViewMode_Click(object? sender, RoutedEventArgs e)
    {
        var next = _viewMode switch
        {
            BoardViewMode.Table   => BoardViewMode.Card,
            BoardViewMode.Card    => BoardViewMode.Table,
            _                     => BoardViewMode.Table,
        };
        ApplyViewMode(next);
    }

    private void ApplyViewMode(BoardViewMode mode)
    {
        _viewMode = mode;
        TableViewContainer.IsVisible = mode == BoardViewMode.Table;
        CardViewContainer.IsVisible  = mode == BoardViewMode.Card;

        BtnViewMode.Content = mode switch
        {
            BoardViewMode.Table => "⊞",
            BoardViewMode.Card  => "☰",
            _                   => "⊞",
        };
    }

    // ── 외부 호출 (새로고침) ─────────────────────────────

    public async Task RefreshAsync() => await ViewModel.RefreshAsync();
}
