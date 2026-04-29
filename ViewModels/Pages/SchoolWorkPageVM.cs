using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 업무 페이지 — 좌측: 업무 메모 + 우측: 업무 카테고리 게시글 목록.
/// (KAgendaControl 임베드는 차후 — 우선 게시판/메모 통합)
/// </summary>
public partial class SchoolWorkPageVM : ViewModelBase
{
    public ObservableCollection<Post> WorkPosts { get; } = new();

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public string TodayText => $"{DateTime.Today:yyyy년 M월 d일 dddd}";

    public SchoolWorkPageVM() { _ = ReloadAsync(); }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            using var repo = new PostRepository(BoardDatabase.DbPath);
            var rows = await repo.GetByCategoryAsync("업무");
            WorkPosts.Clear();
            foreach (var p in rows.OrderByDescending(x => x.DateTime))
                WorkPosts.Add(p);
            StatusText = $"업무 게시글 {WorkPosts.Count}건";
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsBusy = false; }
    }
}
