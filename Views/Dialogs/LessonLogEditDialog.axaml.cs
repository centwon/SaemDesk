using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 수업 차시 기록(LessonLog) 추가/편집 다이얼로그.
/// NewSchool LessonLogEditDialog 포팅 — Course 사전 선택 / 단원 선택은 단순 텍스트로 대체.
/// </summary>
public partial class LessonLogEditDialog : Window
{
    private LessonLog? _existing;
    private List<Course> _courses = new();

    private readonly ObservableCollection<LessonLogAttachItem> _attachments = new();
    private readonly List<LessonLogAttachItem> _deleted = new();

    /// <summary>저장된 LessonLog (성공 시 채워짐).</summary>
    public LessonLog? Result { get; private set; }
    /// <summary>삭제 여부.</summary>
    public bool Deleted { get; private set; }

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public LessonLogEditDialog() : this(null) { }

    /// <summary>기존 기록 편집.</summary>
    public LessonLogEditDialog(LessonLog? existing)
    {
        InitializeComponent();
        AttachList.ItemsSource = _attachments;
        _existing = existing;

        if (existing is null)
        {
            HeaderText.Text            = "수업 기록 추가";
            Title                      = "수업 기록 추가";
            LogDatePicker.SelectedDate = new DateTimeOffset(
                DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified), TimeSpan.Zero);
            CBoxPeriod.SelectedIndex   = 0;
            BtnDelete.IsVisible        = false;
        }
        else
        {
            HeaderText.Text     = "수업 기록 수정";
            Title               = "수업 기록 수정";
            BtnDelete.IsVisible = true;
            Opened += (_, _) => LoadFrom(existing);
        }

        Opened += OnOpenedLoadSuggestions;
    }

    // ────────────────────────────────────────────────────
    //  과목·강의실 자동완성 목록 (교사 과목에서)
    // ────────────────────────────────────────────────────

    private async void OnOpenedLoadSuggestions(object? sender, EventArgs e)
    {
        try
        {
            using var svc = new CourseService();
            _courses = await svc.GetMyCoursesAsync();

            AcSubject.ItemsSource = _courses
                .Select(c => c.Subject)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            AcSubject.TextChanged += (_, _) => RefreshRoomSuggestions();
            AcRoom.TextChanged    += (_, _) => TryFillGradeClassFromRoom();
            RefreshRoomSuggestions();

            // 프리필(시간표 셀 클릭/편집)된 강의실도 1회 분해 — 구독 전에 Text가 세팅되므로
            TryFillGradeClassFromRoom();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LessonLogEditDialog] 과목 목록 로드 실패: {ex.Message}");
        }

        if (_existing is not null) await LoadAttachmentsAsync(_existing.No);
    }

    /// <summary>강의실 제안을 선택된 과목의 RoomList로 갱신(없으면 전체 강의실).</summary>
    private void RefreshRoomSuggestions()
    {
        string subject = (AcSubject.Text ?? string.Empty).Trim();
        var course = _courses.FirstOrDefault(c => c.Subject == subject);

        AcRoom.ItemsSource = course is not null && course.RoomList.Count > 0
            ? course.RoomList
            : _courses.SelectMany(c => c.RoomList).Distinct().ToList();
    }

    /// <summary>강의실이 "학년-반"(예: 1-1) 형태면 분해해 학년·반 칸에 채움(학급공통 자동생성 대응).
    /// 물리 강의실 번호(예: 201-1) 오인 방지를 위해 유효 범위 안일 때만 채운다.</summary>
    private void TryFillGradeClassFromRoom()
    {
        var parts = (AcRoom.Text ?? string.Empty).Split('-');
        if (parts.Length == 2
            && int.TryParse(parts[0].Trim(), out var grade)
            && int.TryParse(parts[1].Trim(), out var cls)
            && grade >= 1 && grade <= (int)NumGrade.Maximum
            && cls   >= 1 && cls   <= (int)NumClass.Maximum)
        {
            NumGrade.Value = grade;
            NumClass.Value = cls;
        }
    }

    /// <summary>
    /// 새 기록 생성 — 오늘의 수업 클릭 시 교시·학급·과목을 미리 채우는 팩토리 메서드.
    /// </summary>
    public static LessonLogEditDialog CreateNew(
        string subject = "",
        string room    = "",
        int    grade   = 0,
        int    cls     = 0,
        int    period  = 0,
        DateTime? date = null)
    {
        var dlg = new LessonLogEditDialog(null);

        // 팩토리로 전달받은 초기값 덮어쓰기 (자동완성 목록은 Opened에서 로드)
        dlg.AcSubject.Text = subject;
        dlg.AcRoom.Text    = room;

        if (date is DateTime d)
            dlg.LogDatePicker.SelectedDate = new DateTimeOffset(
                DateTime.SpecifyKind(d.Date, DateTimeKind.Unspecified), TimeSpan.Zero);

        if (grade > 0) dlg.NumGrade.Value = grade;
        if (cls   > 0) dlg.NumClass.Value = cls;

        if (period >= 1 && period <= 7)
        {
            for (int i = 0; i < dlg.CBoxPeriod.ItemCount; i++)
            {
                if (dlg.CBoxPeriod.Items[i] is ComboBoxItem item &&
                    int.TryParse(item.Tag?.ToString(), out var p) && p == period)
                {
                    dlg.CBoxPeriod.SelectedIndex = i;
                    break;
                }
            }
        }

        return dlg;
    }

    // ────────────────────────────────────────────────────
    //  내부 메서드
    // ────────────────────────────────────────────────────

    private void LoadFrom(LessonLog l)
    {
        LogDatePicker.SelectedDate = new DateTimeOffset(l.Date.Date, TimeSpan.Zero);

        for (int i = 0; i < CBoxPeriod.ItemCount; i++)
        {
            if (CBoxPeriod.Items[i] is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out var p) && p == l.Period)
            {
                CBoxPeriod.SelectedIndex = i;
                break;
            }
        }

        AcSubject.Text      = l.Subject;
        NumGrade.Value      = l.Grade;
        NumClass.Value      = l.Class;
        AcRoom.Text         = l.Room;
        TxtSectionName.Text = l.SectionName;
        TxtTopic.Text       = l.Topic;
        TxtContent.Text     = l.Content;
        TxtNote.Text        = l.Note;
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            string subject = (AcSubject.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                StatusText.Text = "과목을 입력해 주세요.";
                return;
            }
            string topic = (TxtTopic.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(topic))
            {
                StatusText.Text = "주제를 입력해 주세요.";
                return;
            }
            if (LogDatePicker.SelectedDate is not DateTimeOffset dto)
            {
                StatusText.Text = "날짜를 선택해 주세요.";
                return;
            }

            int period = 1;
            if (CBoxPeriod.SelectedItem is ComboBoxItem pi &&
                int.TryParse(pi.Tag?.ToString(), out var p))
                period = p;

            var entity = _existing ?? new LessonLog
            {
                Year      = Settings.WorkYear.Value,
                Semester  = Math.Max(1, Settings.WorkSemester.Value),
                TeacherID = Settings.User.Value,
                CreatedAt = DateTime.Now,
            };
            entity.Date        = dto.Date;
            entity.Period      = period;
            entity.Subject     = subject;
            entity.Grade       = (int)(NumGrade.Value  ?? 0m);
            entity.Class       = (int)(NumClass.Value  ?? 0m);
            entity.Room        = (AcRoom.Text         ?? "").Trim();
            entity.SectionName = (TxtSectionName.Text ?? "").Trim();
            entity.Topic       = topic;
            entity.Content     = (TxtContent.Text     ?? "").Trim();
            entity.Note        = (TxtNote.Text        ?? "").Trim();
            entity.UpdatedAt   = DateTime.Now;

            using var svc = new LessonLogService();
            if (_existing is null) entity.No = await svc.InsertAsync(entity);
            else                    await svc.UpdateAsync(entity);

            await SaveAttachmentsAsync(entity.No);

            Result = entity;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"저장 실패: {ex.Message}";
            Debug.WriteLine($"[LessonLogEditDialog] {ex}");
        }
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (_existing is null) { Close(); return; }

        bool ok = await DialogService.ShowConfirmAsync(
            "차시 기록 삭제",
            $"'{_existing.Subject} - {_existing.Topic}' 기록을 삭제하시겠습니까?\n복구할 수 없습니다.");
        if (!ok) return;

        try
        {
            using var svc = new LessonLogService();
            await svc.DeleteAsync(_existing.No);

            // 첨부 메타·파일 정리 (고아 방지)
            try
            {
                using var fileRepo = new LessonLogFileRepository(SchoolDatabase.DbPath);
                await fileRepo.DeleteByLessonLogAsync(_existing.No);
                var dir = LessonLogFileRepository.GetDir(_existing.No);
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex) { Debug.WriteLine($"[LessonLogEditDialog] 첨부 정리 실패: {ex.Message}"); }

            Deleted = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"삭제 실패: {ex.Message}";
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    // ────────────────────────────────────────────────────
    //  첨부파일
    // ────────────────────────────────────────────────────

    private async Task LoadAttachmentsAsync(int lessonNo)
    {
        try
        {
            using var repo = new LessonLogFileRepository(SchoolDatabase.DbPath);
            var files = await repo.GetByLessonLogAsync(lessonNo);
            foreach (var f in files)
                _attachments.Add(new LessonLogAttachItem { No = f.No, Name = f.FileName, Size = f.FileSize });
        }
        catch (Exception ex) { Debug.WriteLine($"[LessonLogEditDialog] 첨부 로드 실패: {ex.Message}"); }
    }

    private async void OnAddAttachment(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "첨부할 파일 선택",
            AllowMultiple = true,
        });

        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            var fi = new FileInfo(path);
            _attachments.Add(new LessonLogAttachItem { No = 0, Name = fi.Name, Size = fi.Length, SrcPath = path });
        }
    }

    private void OnRemoveAttachment(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is LessonLogAttachItem item)
        {
            if (item.No > 0) _deleted.Add(item);
            _attachments.Remove(item);
        }
    }

    private void OnOpenAttachment(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (AttachList.SelectedItem is not LessonLogAttachItem item) return;
        string? path = item.No > 0 && _existing is not null
            ? LessonLogFileRepository.GetFilePath(_existing.No, item.Name)
            : item.SrcPath;
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { Debug.WriteLine($"[LessonLogEditDialog] 첨부 열기 실패: {ex.Message}"); }
    }

    private async Task SaveAttachmentsAsync(int lessonNo)
    {
        bool hasNew = _attachments.Any(a => a.No == 0 && !string.IsNullOrEmpty(a.SrcPath));
        if (_deleted.Count == 0 && !hasNew) return;

        using var repo = new LessonLogFileRepository(SchoolDatabase.DbPath);

        // 삭제
        foreach (var d in _deleted)
        {
            try
            {
                await repo.DeleteAsync(d.No);
                var p = LessonLogFileRepository.GetFilePath(lessonNo, d.Name);
                if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception ex) { Debug.WriteLine($"[LessonLogEditDialog] 첨부 삭제 실패: {ex.Message}"); }
        }

        // 신규 복사 + 등록
        foreach (var item in _attachments.Where(a => a.No == 0 && !string.IsNullOrEmpty(a.SrcPath)).ToList())
        {
            try
            {
                LessonLogFileRepository.EnsureDir(lessonNo);
                var ext    = Path.GetExtension(item.SrcPath);
                var name   = Path.GetFileNameWithoutExtension(item.SrcPath);
                var stored = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{name}{ext}";
                var dst    = LessonLogFileRepository.GetFilePath(lessonNo, stored);
                File.Copy(item.SrcPath!, dst, true);
                await repo.CreateAsync(new LessonLogFile
                {
                    LessonLog = lessonNo,
                    FileName  = stored,
                    FileSize  = new FileInfo(dst).Length,
                    CreatedAt = DateTime.Now,
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[LessonLogEditDialog] 첨부 저장 실패: {ex.Message}"); }
        }
    }
}

/// <summary>첨부 목록 항목 — 기존(No&gt;0) 또는 신규(No==0, SrcPath 보유).</summary>
public sealed class LessonLogAttachItem
{
    public int     No      { get; set; }
    public string  Name    { get; set; } = string.Empty;
    public long    Size    { get; set; }
    public string? SrcPath { get; set; }

    public string SizeDisplay => Size switch
    {
        < 1024        => $"{Size} B",
        < 1024 * 1024 => $"{Size / 1024.0:F1} KB",
        _             => $"{Size / (1024.0 * 1024):F1} MB",
    };

    /// <summary>저장 파일명에서 "yyyyMMdd_HHmmss_fff_" 접두를 떼어 원본 이름으로 표시.</summary>
    public string DisplayName
    {
        get
        {
            var parts = Name.Split('_');
            if (parts.Length >= 4
                && parts[0].Length == 8 && long.TryParse(parts[0], out _)
                && parts[1].Length == 6 && long.TryParse(parts[1], out _)
                && parts[2].Length == 3 && long.TryParse(parts[2], out _))
                return string.Join("_", parts.Skip(3));
            return Name;
        }
    }
}
