using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.Views.Controls;

/// <summary>수업 기록 리스트 — Avalonia 12 이식. 학급/단원 표시 지원.</summary>
public partial class LessonLogList : UserControl, IDisposable
{
    private bool _disposed;
    private LessonLogService? _service;
    private string? _currentSubject;
    private string? _currentRoom;
    private int? _currentGrade;
    private int? _currentClass;
    private int? _courseNo;

    public ObservableCollection<LessonLog> LessonLogs { get; } = new();
    public LessonLog? SelectedLog => LvLessonLogs.SelectedItem as LessonLog;

    public int? CourseNo
    {
        get => _courseNo;
        set => _courseNo = value;
    }

    public event EventHandler<LessonLog>? LessonSelected;
    public event EventHandler? AddRequested;

    public LessonLogList()
    {
        InitializeComponent();
        _service = new LessonLogService();
        LvLessonLogs.ItemsSource = LessonLogs;
        LvLessonLogs.SelectionChanged += OnSelectionChanged;
        Unloaded += (_, _) => Dispose();
    }

    public async Task LoadAsync(string? subject = null, string? room = null)
    {
        _currentSubject = subject;
        _currentRoom = room;
        _currentGrade = null;
        _currentClass = null;
        await RefreshAsync();
    }

    public async Task LoadByClassAsync(string? subject = null, int? grade = null, int? classNum = null)
    {
        _currentSubject = subject;
        _currentRoom = null;
        _currentGrade = grade;
        _currentClass = classNum;
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_service == null) return;

        try
        {
            ShowLoading(true);
            LvLessonLogs.ItemsSource = null;
            LessonLogs.Clear();

            List<LessonLog> logs = (_currentGrade.HasValue || _currentClass.HasValue)
                ? await _service.GetBySubjectAndClassAsync(_currentSubject, _currentGrade, _currentClass)
                : await _service.GetBySubjectAndRoomAsync(_currentSubject, _currentRoom);

            foreach (var log in logs) LessonLogs.Add(log);

            LvLessonLogs.ItemsSource = LessonLogs;
            UpdateEmptyState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LessonLogList] RefreshAsync 오류: {ex.Message}");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    public void SelectLog(int no)
    {
        foreach (var log in LessonLogs)
        {
            if (log.No == no) { LvLessonLogs.SelectedItem = log; break; }
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LvLessonLogs.SelectedItem is LessonLog log)
            LessonSelected?.Invoke(this, log);
    }

    private void BtnAdd_Click(object? sender, RoutedEventArgs e)
        => AddRequested?.Invoke(this, EventArgs.Empty);

    private async void BtnRefresh_Click(object? sender, RoutedEventArgs e)
        => await RefreshAsync();

    private void ShowLoading(bool isLoading)
    {
        LoadingRing.IsVisible = isLoading;
        LvLessonLogs.IsVisible = !isLoading;
    }

    private void UpdateEmptyState()
    {
        TxtEmpty.IsVisible = LessonLogs.Count == 0;
        LvLessonLogs.IsVisible = LessonLogs.Count > 0;
        TxtCount.Text = LessonLogs.Count > 0 ? $"({LessonLogs.Count}건)" : "";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service?.Dispose();
        _service = null;
        GC.SuppressFinalize(this);
    }
}

/// <summary>DateTime → "M/d(ddd)" 단축 표시.</summary>
public sealed class DateToShortConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime d ? d.ToString("M/d(ddd)") : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>비어있지 않은 문자열 → true (가시성 토글용).</summary>
public sealed class NotEmptyToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
