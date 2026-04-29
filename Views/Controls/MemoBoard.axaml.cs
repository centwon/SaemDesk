using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Views.Dialogs;

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
        ItemsView.PointerPressed += OnItemPointerPressed;
    }

    private async void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount < 2) return;
        if (e.Source is Control c && c.DataContext is Post p)
            await OpenEditAsync(p);
    }

    private async Task OpenEditAsync(Post? existing)
    {
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null) return;
        var dlg = new PostEditDialog(existing);
        await dlg.ShowDialog(owner);
        if (dlg.Saved) await ReloadAsync();
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
        // 카테고리 필터가 지정되어 있으면 신규 메모에 기본값으로 적용
        var seed = new Post
        {
            DateTime = DateTime.Now,
            User = Settings.UserName.Value,
            Category = string.IsNullOrEmpty(_currentCategory) ? "개인" : _currentCategory,
        };
        await OpenEditAsync(null);
        // 다이얼로그 측에서 직접 저장하므로 별도 처리는 ReloadAsync 에 위임
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
