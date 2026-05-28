using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Board.Models;
using SaemDesk.Board.Repositories;
using BoardDb = SaemDesk.Board.BoardDatabase;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 게시글 작성/수정 다이얼로그.
/// 본문은 Jodit 에디터(WebView)로 HTML 입력.
/// </summary>
public partial class PostEditDialog : Window
{
    private readonly Post _post;
    private readonly bool _isNew;

    /// <summary>저장 완료 여부.</summary>
    public bool Saved { get; private set; }

    /// <summary>
    /// 신규 작성 시 카테고리 초기값 (MemoBoard.OnAddClick 에서 주입).
    /// 생성자 실행 후, ShowDialog 호출 전에 설정해야 적용됨.
    /// </summary>
    public string PresetCategory
    {
        set { if (_isNew) CategoryBox.Text = value; }
    }

    public PostEditDialog() : this(null) { }

    public PostEditDialog(Post? existing)
    {
        InitializeComponent();

        _isNew = existing is null;

        _post = existing is null
            ? new Post
            {
                DateTime = DateTime.Now,
                User     = Environment.UserName ?? string.Empty,
            }
            : new Post
            {
                No          = existing.No,
                User        = existing.User,
                DateTime    = existing.DateTime,
                Category    = existing.Category,
                Title       = existing.Title,
                Content     = existing.Content,
                ReadCount   = existing.ReadCount,
                IsCompleted = existing.IsCompleted,
            };

        TitleText.Text = _isNew ? "새 게시글 작성" : "게시글 수정";
        SubTitleText.Text = _isNew
            ? "제목·카테고리·본문을 입력하세요."
            : $"작성: {_post.DateTimeDisplay}";

        CategoryBox.Text = _post.Category;
        TitleBox.Text    = _post.Title;
        UserBox.Text     = _post.User;
        Editor.Text      = _post.Content;
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            string title    = TitleBox.Text?.Trim() ?? string.Empty;
            string category = CategoryBox.Text?.Trim() ?? string.Empty;
            string user     = UserBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(title))
            {
                StatusText.Text = "제목을 입력하세요.";
                TitleBox.Focus();
                return;
            }

            // 에디터에서 최신 HTML 가져오기
            string html = await Editor.GetHtmlAsync();
            if (string.IsNullOrEmpty(html))
                html = _post.Content; // 에디터 미초기화 시 기존값 유지

            _post.Title    = title;
            _post.Category = category;
            _post.User     = user;
            _post.Content  = html;
            _post.DateTime = _isNew ? DateTime.Now : _post.DateTime;

            using var repo = new PostRepository(BoardDb.DbPath);
            if (_isNew)
                await repo.CreateAsync(_post);
            else
                await repo.UpdateAsync(_post);

            Saved = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"저장 실패: {ex.Message}";
            Debug.WriteLine($"[PostEditDialog] {ex}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
