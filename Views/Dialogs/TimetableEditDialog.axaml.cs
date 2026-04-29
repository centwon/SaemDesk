using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 시간표(한 칸) 추가/수정 다이얼로그.
/// existing이 null이면 신규, 있으면 수정 모드.
/// </summary>
public partial class TimetableEditDialog : Window
{
    /// <summary>저장된 시간표 항목. 취소·오류 시 null.</summary>
    public ClassTimetable? Result { get; private set; }

    private readonly ClassTimetable _slot;
    private readonly bool _isNew;
    private readonly int _originalDay;
    private readonly int _originalPeriod;
    private bool _isSaving;

    public TimetableEditDialog() : this(null, defaultDay: 1) { }

    /// <param name="existing">수정할 기존 슬롯 (null이면 신규).</param>
    /// <param name="defaultDay">신규 시 기본 요일 (1=월~5=금).</param>
    /// <param name="defaultPeriod">신규 시 기본 교시.</param>
    public TimetableEditDialog(ClassTimetable? existing, int defaultDay = 1, int defaultPeriod = 1)
    {
        InitializeComponent();

        _isNew = existing is null;

        _slot = existing is not null
            ? new ClassTimetable
            {
                No          = existing.No,
                SchoolCode  = existing.SchoolCode,
                Year        = existing.Year,
                Semester    = existing.Semester,
                Grade       = existing.Grade,
                Class       = existing.Class,
                DayOfWeek   = existing.DayOfWeek,
                Period      = existing.Period,
                SubjectName = existing.SubjectName,
                TeacherName = existing.TeacherName,
                Room        = existing.Room
            }
            : new ClassTimetable
            {
                SchoolCode  = Settings.SchoolCode.Value,
                Year        = Settings.WorkYear.Value,
                Semester    = Settings.WorkSemester.Value,
                Grade       = Settings.HomeGrade.Value,
                Class       = Settings.HomeRoom.Value,
                DayOfWeek   = Math.Clamp(defaultDay, 1, 5),
                Period      = Math.Clamp(defaultPeriod, 1, 10)
            };

        _originalDay    = _slot.DayOfWeek;
        _originalPeriod = _slot.Period;

        // 헤더
        TitleText.Text = _isNew ? "시간표 추가" : "시간표 수정";
        SubTitleText.Text =
            $"{Settings.HomeGrade}학년 {Settings.HomeRoom}반 · " +
            $"{Settings.WorkYear}학년도 {Settings.WorkSemester}학기";

        // 폼 초기값
        DayCombo.SelectedIndex = Math.Clamp(_slot.DayOfWeek - 1, 0, 4);
        PeriodBox.Value        = _slot.Period;
        SubjectBox.Text        = _slot.SubjectName;
        TeacherBox.Text        = _slot.TeacherName;
        RoomBox.Text           = _slot.Room;
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;

        // 유효성 검사
        int day    = DayCombo.SelectedIndex + 1;     // 1=월
        int period = (int)(PeriodBox.Value ?? 1);
        string subject = (SubjectBox.Text ?? string.Empty).Trim();
        string teacher = (TeacherBox.Text ?? string.Empty).Trim();
        string room    = (RoomBox.Text    ?? string.Empty).Trim();

        if (day < 1 || day > 5)
        {
            ShowError("요일을 선택해주세요.");
            return;
        }
        if (period < 1 || period > 10)
        {
            ShowError("교시는 1~10 사이여야 합니다.");
            return;
        }
        if (string.IsNullOrWhiteSpace(subject))
        {
            ShowError("과목명을 입력해주세요.");
            return;
        }

        _isSaving = true;
        SaveButton.IsEnabled = false;

        try
        {
            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);

            // 요일/교시가 바뀐 경우(또는 신규) 중복 체크
            bool slotChanged = _isNew || day != _originalDay || period != _originalPeriod;
            if (slotChanged)
            {
                bool dup = await repo.IsDuplicateAsync(
                    _slot.SchoolCode, _slot.Year, _slot.Semester,
                    _slot.Grade, _slot.Class, day, period);
                if (dup)
                {
                    ShowError($"이미 {DayName(day)}요일 {period}교시 시간표가 있습니다.");
                    SaveButton.IsEnabled = true;
                    _isSaving = false;
                    return;
                }
            }

            _slot.DayOfWeek   = day;
            _slot.Period      = period;
            _slot.SubjectName = subject;
            _slot.TeacherName = teacher;
            _slot.Room        = room;

            if (_isNew)
                await repo.CreateAsync(_slot);
            else
                await repo.UpdateAsync(_slot);

            Result = _slot;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"저장 실패: {ex.Message}");
            SaveButton.IsEnabled = true;
            _isSaving = false;
            System.Diagnostics.Debug.WriteLine($"[TimetableEditDialog] {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void ShowError(string message)
    {
        ErrorText.Text      = message;
        ErrorText.IsVisible = true;
    }

    private static string DayName(int day) => day switch
    {
        1 => "월", 2 => "화", 3 => "수", 4 => "목", 5 => "금",
        _ => "?"
    };
}
