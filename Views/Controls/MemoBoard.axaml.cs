using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 메모보드 컨트롤 — 카테고리 필터 + 게시판 메모 카테고리 게시글 목록.
/// PostRepository.GetByCategoryAsync 위임 (게시판 DB).
/// </summary>
public partial class MemoBoard : UserControl
{
    private readonly ObservableCollection<Post> _items = new();
    private string _currentCategory = string.Empty;

    public MemoBoard()
    {
        InitializeComponent();
        ItemsView.ItemsSource = _items;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilter?.SelectedItem is ComboBoxItem ci && ci.Tag is string tag)
        {
            _currentCategory = tag ?? string.Empty;
            await ReloadAsync();
        }
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var repo = new PostRepository(BoardDatabase.DbPath);
            var post = new Post
            {
                Title = "(새 메모)",
                Content = string.Empty,
                Category = string.IsNullOrEmpty(_currentCategory) ? "개인" : _currentCategory,
                User = Settings.UserName.Value,
                DateTime = DateTime.Now,
            };
            post.No = await repo.CreateAsync(post);
            await ReloadAsync();
        }
        catch { /* 무시 — 컨트롤 단위 */ }
    }

    private async Task ReloadAsync()
    {
        try
        {
            LoadingBar.IsVisible = true;
            using var repo = new PostRepository(BoardDatabase.DbPath);
            var rows = string.IsNullOrEmpty(_currentCategory)
                ? await repo.GetAllAsync()
                : await repo.GetByCategoryAsync(_currentCategory);

            _items.Clear();
            foreach (var p in rows.OrderByDescending(x => x.DateTime))
                _items.Add(p);

            EmptyState.IsVisible = _items.Count == 0;
        }
        catch
        {
            EmptyState.IsVisible = true;
        }
        finally
        {
            LoadingBar.IsVisible = false;
        }
    }
}
