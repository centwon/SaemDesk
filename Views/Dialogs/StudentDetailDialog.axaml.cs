using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 상세 다이얼로그.
/// 탭 1: StudentDetail (보호자, 가족, 진로, 건강 등)
/// 탭 2: StudentLog 목록 + CRUD
/// </summary>
public partial class StudentDetailDialog : Window
{
    private readonly string _studentId;
    private readonly string _studentName;

    private StudentDetail _detail;
    private bool _detailExists;
    private bool _isSavingDetail;

    public ObservableCollection<StudentLog> Logs { get; } = [];

    public StudentDetailDialog() : this("unknown", "이름 미상") { }

    public StudentDetailDialog(string studentId, string studentName)
    {
        InitializeComponent();

        _studentId   = studentId ?? string.Empty;
        _studentName = string.IsNullOrWhiteSpace(studentName) ? "이름 미상" : studentName;
        _detail = new StudentDetail { StudentID = _studentId };

        TitleText.Text    = _studentName;
        SubTitleText.Text = $"학번 {_studentId}";

        LogListBox.ItemsSource = Logs;

        // 첫 표시 시 데이터 로드
        Opened += async (_, _) =>
        {
            await LoadDetailAsync();
            await LoadLogsAsync();
        };
    }

    // ────────────────────────────────────────────────────
    //  StudentDetail 로드 / 저장
    // ────────────────────────────────────────────────────

    private async Task LoadDetailAsync()
    {
        try
        {
            using var repo = new StudentDetailRepository(SchoolDatabase.DbPath);
            var existing = await repo.GetByStudentIdAsync(_studentId);
            if (existing is not null)
            {
                _detail = existing;
                _detailExists = true;
            }
            else
            {
                _detail = new StudentDetail { StudentID = _studentId };
                _detailExists = false;
            }

            FatherNameBox.Text       = _detail.FatherName;
            FatherPhoneBox.Text      = _detail.FatherPhone;
            FatherJobBox.Text        = _detail.FatherJob;
            MotherNameBox.Text       = _detail.MotherName;
            MotherPhoneBox.Text      = _detail.MotherPhone;
            MotherJobBox.Text        = _detail.MotherJob;
            GuardianNameBox.Text     = _detail.GuardianName;
            GuardianPhoneBox.Text    = _detail.GuardianPhone;
            GuardianRelationBox.Text = _detail.GuardianRelation;
            FamilyBox.Text           = _detail.FamilyInfo;
            FriendsBox.Text          = _detail.Friends;
            InterestsBox.Text        = _detail.Interests;
            TalentsBox.Text          = _detail.Talents;
            CareerBox.Text           = _detail.CareerGoal;
            HealthBox.Text           = _detail.HealthInfo;
            AllergyBox.Text          = _detail.Allergies;
            SpecialNeedsBox.Text     = _detail.SpecialNeeds;
            MemoBox.Text             = _detail.Memo;

            DetailStatusText.Text = _detailExists
                ? $"마지막 수정: {_detail.UpdatedAt:yyyy-MM-dd HH:mm}"
                : "신규 — 아직 저장되지 않은 상세 정보입니다.";
        }
        catch (Exception ex)
        {
            DetailStatusText.Text = $"불러오기 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[StudentDetailDialog] {ex.Message}");
        }
    }

    private async void OnSaveDetail(object? sender, RoutedEventArgs e)
    {
        if (_isSavingDetail) return;
        _isSavingDetail = true;

        try
        {
            _detail.StudentID        = _studentId;
            _detail.FatherName       = FatherNameBox.Text       ?? string.Empty;
            _detail.FatherPhone      = FatherPhoneBox.Text      ?? string.Empty;
            _detail.FatherJob        = FatherJobBox.Text        ?? string.Empty;
            _detail.MotherName       = MotherNameBox.Text       ?? string.Empty;
            _detail.MotherPhone      = MotherPhoneBox.Text      ?? string.Empty;
            _detail.MotherJob        = MotherJobBox.Text        ?? string.Empty;
            _detail.GuardianName     = GuardianNameBox.Text     ?? string.Empty;
            _detail.GuardianPhone    = GuardianPhoneBox.Text    ?? string.Empty;
            _detail.GuardianRelation = GuardianRelationBox.Text ?? string.Empty;
            _detail.FamilyInfo       = FamilyBox.Text           ?? string.Empty;
            _detail.Friends          = FriendsBox.Text          ?? string.Empty;
            _detail.Interests        = InterestsBox.Text        ?? string.Empty;
            _detail.Talents          = TalentsBox.Text          ?? string.Empty;
            _detail.CareerGoal       = CareerBox.Text           ?? string.Empty;
            _detail.HealthInfo       = HealthBox.Text           ?? string.Empty;
            _detail.Allergies        = AllergyBox.Text          ?? string.Empty;
            _detail.SpecialNeeds     = SpecialNeedsBox.Text     ?? string.Empty;
            _detail.Memo             = MemoBox.Text             ?? string.Empty;
            _detail.UpdatedAt        = DateTime.Now;
            if (!_detailExists) _detail.CreatedAt = DateTime.Now;

            using var repo = new StudentDetailRepository(SchoolDatabase.DbPath);
            if (_detailExists)
            {
                await repo.UpdateAsync(_detail);
            }
            else
            {
                await repo.CreateAsync(_detail);
                _detailExists = true;
            }

            DetailStatusText.Text = $"저장됨: {_detail.UpdatedAt:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            DetailStatusText.Text = $"저장 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[StudentDetailDialog] 저장 오류: {ex.Message}");
        }
        finally
        {
            _isSavingDetail = false;
        }
    }

    // ────────────────────────────────────────────────────
    //  StudentLog 로드 / CRUD
    // ────────────────────────────────────────────────────

    private async Task LoadLogsAsync()
    {
        try
        {
            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByStudentAsync(_studentId, Settings.WorkYear.Value, 0);

            Logs.Clear();
            foreach (var l in list) Logs.Add(l);

            LogCountText.Text  = $"{Settings.WorkYear}학년도 · 총 {Logs.Count}건";
            LogEmptyText.IsVisible = Logs.Count == 0;
        }
        catch (Exception ex)
        {
            LogCountText.Text = $"불러오기 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[StudentDetailDialog] Logs {ex.Message}");
        }
    }

    private void OnLogSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool hasSel = LogListBox.SelectedItem is StudentLog;
        EditLogButton.IsEnabled   = hasSel;
        DeleteLogButton.IsEnabled = hasSel;
    }

    private async void OnAddLog(object? sender, RoutedEventArgs e)
    {
        var dlg = new StudentLogEditDialog(_studentId, _studentName, null);
        await dlg.ShowDialog(this);
        if (dlg.Result is not null) await LoadLogsAsync();
    }

    private async void OnEditLog(object? sender, RoutedEventArgs e)
    {
        if (LogListBox.SelectedItem is not StudentLog log) return;
        var dlg = new StudentLogEditDialog(_studentId, _studentName, log);
        await dlg.ShowDialog(this);
        if (dlg.Result is not null) await LoadLogsAsync();
    }

    private async void OnDeleteLog(object? sender, RoutedEventArgs e)
    {
        if (LogListBox.SelectedItem is not StudentLog log) return;

        var confirm = new ConfirmDialog(
            "기록 삭제",
            $"{log.Date:yyyy-MM-dd} {log.Category} 기록을 삭제하시겠습니까?");
        await confirm.ShowDialog(this);
        if (!confirm.Result) return;

        try
        {
            using var repo = new StudentLogRepository(SchoolDatabase.DbPath);
            await repo.DeleteAsync(log.No);
            await LoadLogsAsync();
        }
        catch (Exception ex)
        {
            LogCountText.Text = $"삭제 실패: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[StudentDetailDialog] 삭제 오류: {ex.Message}");
        }
    }

    private async void OnRefreshLogs(object? sender, RoutedEventArgs e)
    {
        await LoadLogsAsync();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
