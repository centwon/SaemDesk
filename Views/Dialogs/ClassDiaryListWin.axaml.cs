using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.Services;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Dialogs;

/// <summary>학급일지 목록 조회 창 — Avalonia 12 이식 (NewSchool ClassDiaryListWin).</summary>
public partial class ClassDiaryListWin : Window, IDisposable
{
    private bool _disposed;
    private readonly ClassDiaryService _diaryService;
    private readonly int _year;
    private readonly int _semester;
    private readonly int _grade;
    private readonly int _classNumber;
    private readonly ObservableCollection<ClassDiaryViewModel> _diaries = new();

    public event EventHandler<ClassDiaryViewModel>? DiarySelected;

    public ClassDiaryListWin() : this(0, 0, 0, 0) { }

    public ClassDiaryListWin(int year, int semester, int grade, int classNumber)
    {
        InitializeComponent();

        _diaryService = new ClassDiaryService(SchoolDatabase.DbPath);
        _year = year;
        _semester = semester;
        _grade = grade;
        _classNumber = classNumber;

        Title = $"{year}학년도 {semester}학기 {grade}학년 {classNumber}반 학급일지";
        DiaryItemsRepeater.ItemsSource = _diaries;

        Closed += (_, _) => Dispose();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var today = DateTime.Today;
        DatePickerStart.SelectedDate = new DateTime(today.Year, today.Month, 1);
        DatePickerEnd.SelectedDate = today;
        await LoadDiariesAsync();
    }

    private async Task LoadDiariesAsync()
    {
        try
        {
            if (DatePickerStart.SelectedDate is not DateTime startDate ||
                DatePickerEnd.SelectedDate is not DateTime endDate)
            {
                Debug.WriteLine("[ClassDiaryListWin] 날짜 누락"); return;
            }
            if (startDate > endDate)
            {
                Debug.WriteLine("[ClassDiaryListWin] 시작일이 종료일보다 늦음"); return;
            }

            var diaries = await _diaryService.GetDateRangeDiariesAsync(
                Settings.SchoolCode, _year, _semester, _grade, _classNumber, startDate, endDate);

            DiaryItemsRepeater.ItemsSource = null;
            _diaries.Clear();
            foreach (var d in diaries.OrderByDescending(x => x.Date))
                _diaries.Add(new ClassDiaryViewModel(d));
            DiaryItemsRepeater.ItemsSource = _diaries;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClassDiaryListWin] 로드 실패: {ex.Message}");
        }
    }

    private async void BtnSearch_Click(object? sender, RoutedEventArgs e) => await LoadDiariesAsync();

    private void DiaryItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border b && b.Tag is ClassDiaryViewModel vm)
        {
            DiarySelected?.Invoke(this, vm);
            Close();
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _diaryService?.Dispose();
        GC.SuppressFinalize(this);
    }
}
