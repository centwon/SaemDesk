using System;
using Avalonia.Controls;
using SaemDesk.Board.Views.Pages;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 게시글 상세 보기 다이얼로그. 기존 PostDetailPage / PostEditPage 를 한 창 안에서
/// 교체해 상세↔수정을 처리한다 (BoardPage 와 동일 패턴 — 다이얼로그를 새로 띄우지 않음).
/// </summary>
public partial class PostDetailDialog : Window
{
    private readonly int _postNo;

    public PostDetailDialog() : this(0) { }

    public PostDetailDialog(int postNo)
    {
        InitializeComponent();
        _postNo = postNo;

        DetailPage.BackRequested += (_, _) => Close();
        DetailPage.EditRequested += OnEditRequested;
        EditPage.Saved           += OnEditSaved;
        EditPage.Cancelled       += (_, _) => ShowOnly(DetailPage);

        Opened += async (_, _) =>
        {
            ShowOnly(DetailPage);
            await DetailPage.LoadAsync(_postNo);
        };
    }

    private async void OnEditRequested(object? sender, PostEditPageParameter e)
    {
        ShowOnly(EditPage);
        await EditPage.InitAsync(e);
    }

    private async void OnEditSaved(object? sender, EventArgs e)
    {
        ShowOnly(DetailPage);
        await DetailPage.LoadAsync(_postNo);
    }

    private void ShowOnly(Control target)
    {
        DetailPage.IsVisible = target == DetailPage;
        EditPage.IsVisible   = target == EditPage;
    }
}
