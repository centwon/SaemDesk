using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaemDesk.Board.Models;

namespace SaemDesk.Views.Dialogs;

public partial class MemoEditDialog : Window
{
    private static readonly List<string> Categories =
        new() { "수업", "학급", "업무", "개인" };

    private Post? _post;

    public Post? SavedPost { get; private set; }

    public MemoEditDialog()
    {
        InitializeComponent();
        CBoxCategory.ItemsSource = Categories;
        CBoxCategory.SelectedIndex = 3;
    }

    // ── 초기화 ──────────────────────────────────────

    public void InitForNew(string category = "개인", string subject = "")
    {
        Title = "새 메모";
        _post = new Post
        {
            DateTime = DateTime.Now,
            User     = Settings.UserName.Value.Length > 0 ? Settings.UserName.Value : "익명",
            Subject  = subject,
        };
        SelectCategory(category);
    }

    public void InitForEdit(Post post)
    {
        _post = post;
        Title = "메모 수정";

        TxtTitle.Text = post.Title;
        SelectCategory(post.Category);

        if (!string.IsNullOrEmpty(post.Content))
            Editor.Text = post.Content;
    }

    private void SelectCategory(string category)
    {
        int idx = Categories.IndexOf(category);
        CBoxCategory.SelectedIndex = idx >= 0 ? idx : 3;
    }

    // ── 저장 / 취소 ────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_post is null) return;

        string title = TxtTitle.Text?.Trim() ?? "";
        string html  = await Editor.GetHtmlAsync();

        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(html))
            return;

        _post.Title    = title;
        _post.Content  = html;
        _post.DateTime = DateTime.Now;

        if (CBoxCategory.SelectedItem is string cat)
            _post.Category = cat;

        SavedPost = _post;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
