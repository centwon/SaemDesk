using System;
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

    // ── Ctrl+F 단축키 ────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            // XAML의 TextBox (Grid.Column=2) 에 포커스
            var searchBox = this.FindControl<TextBox>("SearchBox");
            searchBox?.Focus();
            searchBox?.SelectAll();
            e.Handled = true;
        }
    }

    // ── 이벤트 (Frame 대신 이벤트로 네비게이션) ──────────
    public event EventHandler<int>?                    PostSelected;   // PostNo
    public event EventHandler<PostEditPageParameter>?  NewPostRequested;

    // 기본 카테고리 / 카테고리별 기본 주제는 BoardDefaults 공용 출처 사용

    // CheckBox -> ToggleButton 전환으로 x:Name 제거 (XAML이 바인딩 직접 처리)

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
        // 이미 BoardPage 가 파라미터로 초기화했다면 그 파라미터를 재사용한다.
        // (null 로 재초기화하면 고정 카테고리 보드의 설정이 풀린다.)
        await InitAsync(_param);
    }

    public async Task InitAsync(PostListPageParameter? param = null)
    {
        _param = param;
        ApplyParameter();
        // 카테고리 고정(AllowCategoryChange=false) 게시판은 콤보를 숨기므로 채우지 않는다.
        // InitCategoriesAsync 가 "전체"를 선택하면 OnCategoryChanged 가 고정 카테고리를 덮어쓴다.
        if (_param?.AllowCategoryChange ?? true)
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
            foreach (var d in BoardDefaults.Categories)
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
            if (BoardDefaults.Topics.TryGetValue(cat, out var defaults))
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

    private async void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ViewModel.SearchPostsAsync();
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
            BoardViewMode.Card    => BoardViewMode.Gallery,
            BoardViewMode.Gallery => BoardViewMode.Table,
            _                     => BoardViewMode.Table,
        };
        ApplyViewMode(next);
    }

    private void ApplyViewMode(BoardViewMode mode)
    {
        _viewMode = mode;
        TableViewContainer.IsVisible  = mode == BoardViewMode.Table;
        CardViewContainer.IsVisible   = mode == BoardViewMode.Card;
        GalleryViewContainer.IsVisible = mode == BoardViewMode.Gallery;

        if (mode == BoardViewMode.Card)
            CardViewRepeater.ItemsSource = ViewModel.Posts;
        else if (mode == BoardViewMode.Gallery)
            GalleryViewRepeater.ItemsSource = ViewModel.Posts;

        BtnViewMode.Content = mode switch
        {
            BoardViewMode.Table   => "⊞",
            BoardViewMode.Card    => "☰",
            BoardViewMode.Gallery => "▦",
            _                     => "⊞",
        };
        ToolTip.SetTip(BtnViewMode, mode switch
        {
            BoardViewMode.Table   => "뷰 전환 (표 → 카드 → 갤러리)",
            BoardViewMode.Card    => "뷰 전환 (카드 → 갤러리 → 표)",
            BoardViewMode.Gallery => "뷰 전환 (갤러리 → 표 → 카드)",
            _                     => "뷰 전환",
        });
    }

    // ── 외부 호출 (새로고침) ─────────────────────────────

    public async Task RefreshAsync() => await ViewModel.RefreshAsync();
}
