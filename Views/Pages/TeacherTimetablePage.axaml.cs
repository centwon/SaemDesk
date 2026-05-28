using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Services;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class TeacherTimetablePage : UserControl
{
    private int _year = Settings.WorkYear;
    private int _semester = Math.Max(1, Settings.WorkSemester);
    private bool _loaded;

    public TeacherTimetablePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        await LoadAsync();
    }

    private void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        _year = e.Year;
        _semester = e.Semester;
        _ = LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var teacherId = Settings.User.Value;
        if (string.IsNullOrWhiteSpace(teacherId)) return;

        BusyBar.IsVisible = true;
        try
        {
            await TimetableCtrl.LoadTeacherScheduleAsync(teacherId, _year, _semester);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TeacherTimetablePage] 시간표 로드 실패: {ex.Message}");
        }
        finally
        {
            BusyBar.IsVisible = false;
        }
    }
}
