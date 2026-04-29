using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Pages;

public partial class LessonHomePage : UserControl
{
    public LessonHomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            await LessonAgenda.LoadPendingAndFutureAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LessonHomePage] 어젠다 로드 오류: {ex.Message}");
        }
    }
}
