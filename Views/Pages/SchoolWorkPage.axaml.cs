using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.Board.Models;
using SaemDesk.Scheduler;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

public partial class SchoolWorkPage : UserControl
{
    public SchoolWorkPage()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SchedulerEvents.ItemChanged += OnSchedulerItemChanged;
        await AgendaControl.LoadPendingAndFutureAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
        => SchedulerEvents.ItemChanged -= OnSchedulerItemChanged;

    // 어젠다에서 할 일/일정이 바뀌면 요약 칩만 재집계.
    private void OnSchedulerItemChanged(object? sender, EventArgs e)
    {
        if (DataContext is SchoolWorkPageVM vm)
            _ = vm.RefreshSummaryAsync();
    }

    // 업무 게시글 더블클릭 → 상세 보기.
    private void OnPostDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SchoolWorkPageVM vm && WorkPostsList.SelectedItem is Post post)
            vm.OpenPostCommand.Execute(post);
    }
}
