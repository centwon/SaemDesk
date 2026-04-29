using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaemDesk.Board.Models;
using SaemDesk.Board.Services;
using SaemDesk.Board.ViewModels;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Board.Views.Pages;

/// <summary>
/// 게시글 상세 페이지 — NewSchool PostDetailPage 이식 (Frame → 이벤트).
/// JoditEditor(ReadOnly) + 첨부파일 + 댓글.
/// </summary>
public partial class PostDetailPage : UserControl
{
    public PostDetailViewModel ViewModel { get; }

    private int                  _postNo;
    private PostListPageParameter? _boardParam;
    private string?              _attachedFilePath;

    // ── 이벤트 ────────────────────────────────────────────
    public event EventHandler?                        BackRequested;
    public event EventHandler<PostEditPageParameter>? EditRequested;

    public PostDetailPage()
    {
        InitializeComponent();
        ViewModel = new PostDetailViewModel();
        DataContext = ViewModel;
    }

    // ── 공개 로드 메서드 ─────────────────────────────────

    public async Task LoadAsync(int postNo, PostListPageParameter? boardParam = null)
    {
        _postNo     = postNo;
        _boardParam = boardParam;

        await ViewModel.LoadPostAsync(postNo);

        if (ViewModel.Post is not null)
        {
            // JoditEditor에 내용 설정 (Text 프로퍼티로 설정)
            ContentViewer.Mode = SaemDesk.Views.Controls.JoditEditor.EditorMode.ReadOnly;
            ContentViewer.Text = ViewModel.Post.Content;

            // 첨부파일 목록 로드
            using var svc   = BoardService.Create();
            var files = await svc.GetPostFilesByPostAsync(postNo);
            if (files.Count > 0)
            {
                FileListBox.LoadFiles(files, ViewModel.Post.Category, readOnly: true);
                FileListBox.IsVisible = true;
            }
        }
    }

    // ── 버튼 핸들러 ───────────────────────────────────────

    private void BtnBack_Click(object? sender, RoutedEventArgs e)
        => BackRequested?.Invoke(this, EventArgs.Empty);

    private void BtnEdit_Click(object? sender, RoutedEventArgs e)
    {
        EditRequested?.Invoke(this, new PostEditPageParameter
        {
            PostNo            = _postNo,
            DefaultCategory   = _boardParam?.Category ?? "",
            AllowCategoryChange = _boardParam?.AllowCategoryChange ?? true,
        });
    }

    private async void BtnDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Post is null) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new ConfirmDialog("게시글 삭제", "정말로 이 게시글을 삭제하시겠습니까?");
        bool ok = await dlg.ShowDialogAsync(owner);
        if (!ok) return;

        using var svc = BoardService.Create();
        await svc.DeletePostAsync(_postNo, ViewModel.Post.Category);
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void BtnPrint_Click(object? sender, RoutedEventArgs e)
    {
        try { await ContentViewer.PrintAsync(); }
        catch (Exception ex) { Debug.WriteLine($"[PostDetailPage] 인쇄: {ex.Message}"); }
    }

    // ── 댓글 파일 열기 ────────────────────────────────────

    private void BtnOpenCommentFile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is Comment c && ViewModel.Post is not null)
        {
            try
            {
                string path = BoardDatabase.GetFilePath(c.FileName, ViewModel.Post.Category);
                if (File.Exists(path))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                        { UseShellExecute = true });
            }
            catch (Exception ex) { Debug.WriteLine($"[PostDetailPage] 파일: {ex.Message}"); }
        }
    }

    // ── 댓글 수정/삭제 ────────────────────────────────────

    private void BtnEditComment_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is Comment c)
        {
            ViewModel.StartEdit(c);
            BtnSaveComment.Content    = "수정 완료";
            BtnCancelEdit.IsVisible   = true;
            CommentEditTitle.Text     = "댓글 수정";
        }
    }

    private void BtnCancelEdit_Click(object? sender, RoutedEventArgs e)
    {
        ViewModel.CancelEdit();
        ResetCommentUI();
    }

    private async void BtnDeleteComment_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is Comment c)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;
            var dlg = new ConfirmDialog("댓글 삭제", "정말로 이 댓글을 삭제하시겠습니까?");
            bool ok = await dlg.ShowDialogAsync(owner);
            if (ok) await ViewModel.DeleteCommentAsync(c);
        }
    }

    // ── 댓글 파일 첨부 ────────────────────────────────────

    private async void BtnAttachFile_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { AllowMultiple = false });

        if (files.Count > 0)
        {
            _attachedFilePath   = files[0].Path.LocalPath;
            AttachedFileName.Text = files[0].Name;
            BtnRemoveAttach.IsVisible = true;
        }
    }

    private void BtnRemoveAttach_Click(object? sender, RoutedEventArgs e)
    {
        _attachedFilePath  = null;
        AttachedFileName.Text = "";
        BtnRemoveAttach.IsVisible = false;
    }

    // ── 댓글 저장 ────────────────────────────────────────

    private async void BtnSaveComment_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEditing)
        {
            await ViewModel.UpdateCommentAsync();
            ResetCommentUI();
        }
        else
        {
            await ViewModel.AddCommentAsync(_attachedFilePath);
            _attachedFilePath = null;
            AttachedFileName.Text = "";
            BtnRemoveAttach.IsVisible = false;
        }
    }

    private void ResetCommentUI()
    {
        BtnSaveComment.Content   = "댓글 작성";
        BtnCancelEdit.IsVisible  = false;
        CommentEditTitle.Text    = "댓글 작성";
        _attachedFilePath        = null;
        AttachedFileName.Text    = "";
        BtnRemoveAttach.IsVisible = false;
    }
}
