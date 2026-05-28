using System;
using System.Threading.Tasks;
using Avalonia.Controls;
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

        // ListPage 이벤트
        ListPage.PostSelected     += OnPostSelected;
        ListPage.NewPostRequested += OnNewPostRequested;

        // DetailPage 이벤트
        DetailPage.BackRequested += OnDetailBack;
        DetailPage.EditRequested += OnEditPostRequested;

        // EditPage 이벤트
        EditPage.Saved     += OnEditSaved;
        EditPage.Cancelled += OnEditCancelled;

        // DataContext 변경 감지 — ViewLocator가 ViewModel을 나중에 주입하므로
        // Loaded 대신 DataContextChanged 를 사용해 파라미터를 안정적으로 전달.
        DataContextChanged += OnDataContextChanged;
    }

    // ── DataContext 변경 시 PostListPage 초기화 ───────────

    private async void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not BoardPageVM vm) return;

        _param = vm.Parameter;

        Board.Views.Pages.PostListPageParameter? listParam = null;
        if (_param is not null)
        {
            listParam = new Board.Views.Pages.PostListPageParameter
            {
                Title               = _param.Title ?? "",
                AllowCategoryChange = _param.AllowCategoryChange,
                ShowSubjectFilter   = _param.ShowSubjectFilter,
                Category            = _param.FixedCategory ?? "",
            };
        }

        ShowOnly(ListPage);
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
