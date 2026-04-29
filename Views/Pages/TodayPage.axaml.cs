using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Pages;

public partial class TodayPage : UserControl
{
    public TodayPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            // 할 일 + 일정 통합 어젠다 로드 (오늘 + 미래 60일)
            await AgendaList.LoadPendingAndFutureAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TodayPage] 어젠다 로드 오류: {ex.Message}");
        }
    }
}
