using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Board.Views.Pages;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

/// <summary>
/// Board 컨테이너 페이지 — PostListPage / PostDetailPage / PostEditPage 를
/// 이벤트 기반으로 전환. NewSchool 의 Frame.Navigate 패턴 대체.
/// </summary>
public partial class BoardPage : UserControl
{
    private BoardPageParameter? _param;

    public BoardPage()
    {
        InitializeComponent();
        DataContext = new BoardPageVM();

        // ListPage 이벤트
        ListPage.PostSelected     += OnPostSelected;
        ListPage.NewPostRequested += OnNewPostRequested;

        // DetailPage 이벤트
        DetailPage.BackRequested += OnDetailBack;
        DetailPage.EditRequested += OnEditPostRequested;

        // EditPage 이벤트
        EditPage.Saved     += OnEditSaved;
        EditPage.Cancelled += OnEditCancelled;

        Loaded += OnLoaded;
    }

    // ── 파라미터 적용 (MainWindow에서 호출) ────────────────

    public async Task ApplyParameterAsync(BoardPageParameter? param)
    {
        _param = param;
        Board.Views.Pages.PostListPageParameter? listParam = null;
        if (param is not null)
        {
            listParam = new Board.Views.Pages.PostListPageParameter
            {
                Title               = param.Title ?? "",
                AllowCategoryChange = param.AllowCategoryChange,
                ShowSubjectFilter   = param.ShowSubjectFilter,
                Category            = param.FixedCategory ?? "",
            };
        }
        await ListPage.InitAsync(listParam);
    }

    // ── Loaded ────────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as BoardPageVM;
        if (vm is null) return;

        // BoardPageVM의 파라미터 → PostListPage 파라미터로 변환
        Board.Views.Pages.PostListPageParameter? listParam = null;
        if (vm.Parameter is not null)
        {
            var p = vm.Parameter;
            listParam = new Board.Views.Pages.PostListPageParameter
            {
                Title               = p.Title ?? "",
                AllowCategoryChange = p.AllowCategoryChange,
                ShowSubjectFilter   = p.ShowSubjectFilter,
                Category            = p.FixedCategory ?? "",
            };
        }
        await ListPage.InitAsync(listParam);
    }

    // ── 네비게이션 ────────────────────────────────────────

    private async void OnPostSelected(object? sender, int postNo)
    {
        ShowOnly(DetailPage);
        await DetailPage.LoadAsync(postNo, GetBoardListParam());
    }

    private async void OnNewPostRequested(object? sender, Board.Views.Pages.PostEditPageParameter e)
    {
        ShowOnly(EditPage);
        await EditPage.InitAsync(e);
    }

    private async void OnEditPostRequested(object? sender, Board.Views.Pages.PostEditPageParameter e)
    {
        ShowOnly(EditPage);
        await EditPage.InitAsync(e);
    }

    private void OnDetailBack(object? sender, EventArgs e)
    {
        ShowOnly(ListPage);
        _ = ListPage.RefreshAsync();
    }

    private async void OnEditSaved(object? sender, EventArgs e)
    {
        ShowOnly(ListPage);
        await ListPage.RefreshAsync();
    }

    private void OnEditCancelled(object? sender, EventArgs e)
    {
        // Detail에서 왔으면 Detail로, 아니면 List로
        bool fromDetail = !DetailPage.IsVisible == false;
        if (fromDetail) ShowOnly(DetailPage);
        else            ShowOnly(ListPage);
    }

    // ── 헬퍼 ─────────────────────────────────────────────

    private void ShowOnly(UserControl target)
    {
        ListPage.IsVisible   = target == ListPage;
        DetailPage.IsVisible = target == DetailPage;
        EditPage.IsVisible   = target == EditPage;
    }

    private Board.Views.Pages.PostListPageParameter? GetBoardListParam()
    {
        var vm = DataContext as BoardPageVM;
        if (vm?.Parameter is null) return null;
        var p = vm.Parameter;
        return new Board.Views.Pages.PostListPageParameter
        {
            Title               = p.Title ?? "",
            AllowCategoryChange = p.AllowCategoryChange,
            ShowSubjectFilter   = p.ShowSubjectFilter,
            Category            = p.FixedCategory ?? "",
        };
    }
}
