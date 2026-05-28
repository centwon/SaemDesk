using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Board.Repositories;
using BoardDb = SaemDesk.Board.BoardDatabase;

namespace SaemDesk.Views.Dialogs;

/// <summary>게시글 상세 보기(읽기 전용). Jodit 에디터를 ReadOnly 모드로 사용.</summary>
public partial class PostDetailDialog : Window
{
    private readonly int _postNo;

    public PostDetailDialog() : this(0) { }

    public PostDetailDialog(int postNo)
    {
        InitializeComponent();
        _postNo = postNo;

        Opened += async (_, _) => await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (_postNo <= 0) return;
        try
        {
            using var repo = new PostRepository(BoardDb.DbPath);
            var post = await repo.GetByIdAsync(_postNo);
            if (post is null)
            {
                TitleText.Text  = "(삭제된 게시글)";
                MetaText.Text   = string.Empty;
                CategoryText.Text = "?";
                return;
            }

            // 조회수 증가
            await repo.IncrementReadCountAsync(_postNo);

            CategoryText.Text = string.IsNullOrWhiteSpace(post.Category) ? "기본" : post.Category;
            TitleText.Text    = post.Title;
            MetaText.Text     = $"{post.User} · {post.DateTimeDisplay} · 조회 {post.ReadCount + 1}";
            Viewer.Text       = post.Content;
        }
        catch (Exception ex)
        {
            TitleText.Text = $"불러오기 실패: {ex.Message}";
            Debug.WriteLine($"[PostDetailDialog] {ex}");
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
