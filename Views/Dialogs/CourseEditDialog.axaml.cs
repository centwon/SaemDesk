using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 수업(Course) 추가/편집 다이얼로그 — NewSchool CourseEditDialog MVP 포팅.
/// </summary>
public partial class CourseEditDialog : Window
{
    private Course? _existing;
    private readonly string _schoolCode;
    private readonly string _teacherId;
    private readonly int    _year;
    private readonly int    _semester;

    /// <summary>저장된 Course (성공 시 채워짐).</summary>
    public Course? Result { get; private set; }

    public CourseEditDialog() : this("", "", DateTime.Today.Year, 1) { }

    /// <summary>새 수업.</summary>
    public CourseEditDialog(string schoolCode, string teacherId, int year, int semester)
    {
        InitializeComponent();
        _schoolCode = schoolCode;
        _teacherId  = teacherId;
        _year       = year;
        _semester   = semester;
        HeaderText.Text = "수업 추가";
        Title = "수업 추가";
    }

    /// <summary>기존 수업 수정.</summary>
    public CourseEditDialog(Course existing) : this()
    {
        _existing   = existing;
        _schoolCode = existing.SchoolCode;
        _teacherId  = existing.TeacherID;
        _year       = existing.Year;
        _semester   = existing.Semester;
        HeaderText.Text = "수업 수정";
        Title = "수업 수정";
        Opened += (_, _) => LoadFrom(existing);
    }

    private void LoadFrom(Course c)
    {
        _suppressRoomsAutoFill = true;
        try
        {
            TxtSubject.Text = c.Subject;
            CBoxGrade.SelectedIndex = Math.Clamp(c.Grade - 1, 0, 2);
            NumUnit.Value = c.Unit;

            // 유형 매칭
            for (int i = 0; i < CBoxType.ItemCount; i++)
            {
                if (CBoxType.Items[i] is ComboBoxItem item &&
                    (item.Tag?.ToString() ?? "") == c.Type)
                {
                    CBoxType.SelectedIndex = i;
                    break;
                }
            }
            TxtRooms.Text  = c.Rooms;
            TxtRemark.Text = c.Remark;
            UpdateRoomsPreview();
        }
        finally
        {
            _suppressRoomsAutoFill = false;
        }
    }

    // ── 강의실 미리보기 ────────────────────────────────────
    private void OnRoomsTextChanged(object? sender, TextChangedEventArgs e) => UpdateRoomsPreview();

    private void UpdateRoomsPreview()
    {
        var text = (TxtRooms.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            TxtRoomsPreview.IsVisible = false;
            return;
        }
        var rooms = new Course { Rooms = text }.RoomList;
        if (rooms.Count > 0)
        {
            TxtRoomsPreview.Text = $"📍 {string.Join(", ", rooms)}";
            TxtRoomsPreview.IsVisible = true;
        }
        else
        {
            TxtRoomsPreview.IsVisible = false;
        }
    }

    // ── 유형/학년 변경 시 강의실 자동 채우기 ─────────────────
    private bool _suppressRoomsAutoFill;

    private async void OnGradeOrTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 초기화 중(LoadFrom) 또는 재진입 방지
        if (_suppressRoomsAutoFill) return;
        if (TxtRooms is null) return;

        string type = "";
        if (CBoxType.SelectedItem is ComboBoxItem ti) type = ti.Tag?.ToString() ?? "";

        if (type != CourseTypes.Class) return;  // Class 유형만 자동 채움

        int grade = 1;
        if (CBoxGrade.SelectedItem is ComboBoxItem gi && int.TryParse(gi.Tag?.ToString(), out var g))
            grade = g;

        try
        {
            using var svc = new EnrollmentService();
            var classList = await svc.GetClassListAsync(_schoolCode, _year, grade);
            TxtRooms.Text = classList.Count > 0
                ? string.Join(", ", classList.Select(c => $"{grade}-{c}"))
                : string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CourseEditDialog] 강의실 자동 채우기 실패: {ex.Message}");
        }
    }

    // ── 강의실 프리셋 ─────────────────────────────────────
    private void OnPresetClass15(object? sender, RoutedEventArgs e) => TxtRooms.Text = "1반,2반,3반,4반,5반";
    private void OnPresetOdd     (object? sender, RoutedEventArgs e) => TxtRooms.Text = "1반,3반,5반,7반";
    private void OnPresetABC     (object? sender, RoutedEventArgs e) => TxtRooms.Text = "A반,B반,C반";
    private void OnPresetSpecial (object? sender, RoutedEventArgs e) => TxtRooms.Text = "음악실,미술실,과학실";

    // ── 저장 / 취소 ────────────────────────────────────────
    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            string subject = (TxtSubject.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                StatusText.Text = "과목명을 입력해 주세요.";
                return;
            }

            int grade = 1;
            if (CBoxGrade.SelectedItem is ComboBoxItem gi && int.TryParse(gi.Tag?.ToString(), out var g)) grade = g;

            string type = "Class";
            if (CBoxType.SelectedItem is ComboBoxItem ti && !string.IsNullOrEmpty(ti.Tag?.ToString()))
                type = ti.Tag!.ToString()!;

            string rooms = (TxtRooms.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(rooms))
            {
                StatusText.Text = "강의실을 입력해 주세요.";
                return;
            }

            int unit = (int)(NumUnit.Value ?? 0m);
            string remark = (TxtRemark.Text ?? "").Trim();

            using var repo = new CourseRepository(SchoolDatabase.DbPath);
            if (_existing is null)
            {
                var c = new Course
                {
                    SchoolCode = _schoolCode,
                    TeacherID  = _teacherId,
                    Year       = _year,
                    Semester   = _semester,
                    Grade      = grade,
                    Subject    = subject,
                    Unit       = unit,
                    Type       = type,
                    Rooms      = rooms,
                    Remark     = remark,
                };
                c.No   = await repo.CreateAsync(c);
                Result = c;
            }
            else
            {
                _existing.Grade   = grade;
                _existing.Subject = subject;
                _existing.Unit    = unit;
                _existing.Type    = type;
                _existing.Rooms   = rooms;
                _existing.Remark  = remark;
                await repo.UpdateAsync(_existing);
                Result = _existing;
            }

            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"저장 실패: {ex.Message}";
            Debug.WriteLine($"[CourseEditDialog] {ex}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
