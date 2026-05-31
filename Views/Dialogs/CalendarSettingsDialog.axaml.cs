using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Google;
using SaemDesk.Models;
using SaemDesk.Scheduler;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 일정 설정 다이얼로그 — NewSchool 의 CalendarSettingsDialog 와 동등한 구조.
/// 4개 Expander: 일정 표시 / Google 캘린더 연동 / 동기화 / 학사일정 일괄 등록.
/// </summary>
public partial class CalendarSettingsDialog : Window
{
    private readonly ObservableCollection<GoogleCalendarCheckItem> _calendarItems = new();
    private bool _busy;

    public CalendarSettingsDialog()
    {
        InitializeComponent();

        GoogleCalendarListView.ItemsSource = _calendarItems;

        Opened += OnOpened;
    }

    // ────────────────────────────────────────────────────
    //  Open / Close
    // ────────────────────────────────────────────────────

    private async void OnOpened(object? sender, EventArgs e)
    {
        // 1. 일정 표시 / 폰트 / 자동 동기화 설정 로드
        ShowEventsToggle.IsChecked          = Settings.ShowEvents.Value;
        ShowTasksToggle.IsChecked           = Settings.ShowTasks.Value;
        EventFontSizeBox.Value              = (decimal)Settings.EventFontSize.Value;
        TaskFontSizeBox.Value               = (decimal)Settings.TaskFontSize.Value;
        UseGoogleToggle.IsChecked           = Settings.UseGoogle.Value;
        GoogleAutoSyncToggle.IsChecked      = Settings.GoogleAutoSync.Value;
        GoogleSyncIntervalBox.Value         = Settings.GoogleSyncIntervalMinutes.Value;

        // 2. Google 인증 상태 갱신 + 캘린더 목록 로드
        RefreshGoogleAuthStatus();
        if (IsAuthenticated())
            await LoadGoogleCalendarListAsync();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        // 일정 표시 / 동기화 옵션 즉시 저장
        try
        {
            Settings.ShowEvents.Set(ShowEventsToggle.IsChecked == true);
            Settings.ShowTasks.Set(ShowTasksToggle.IsChecked == true);
            if (EventFontSizeBox.Value is decimal ef) Settings.EventFontSize.Set((double)ef);
            if (TaskFontSizeBox.Value is decimal tf)  Settings.TaskFontSize.Set((double)tf);
            Settings.UseGoogle.Set(UseGoogleToggle.IsChecked == true);
            Settings.GoogleAutoSync.Set(GoogleAutoSyncToggle.IsChecked == true);
            if (GoogleSyncIntervalBox.Value is decimal iv)
                Settings.GoogleSyncIntervalMinutes.Set((int)iv);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CalendarSettingsDialog] 설정 저장 오류: {ex.Message}");
        }

        // 설정 반영 — 자동 동기화 토글/간격 변경을 즉시 반영
        try { App.RestartGoogleAutoSync(); }
        catch (Exception ex) { Debug.WriteLine($"[CalendarSettingsDialog] 자동 동기화 재시작 실패: {ex.Message}"); }

        Close();
    }

    // ────────────────────────────────────────────────────
    //  Google 인증
    // ────────────────────────────────────────────────────

    private static bool IsAuthenticated()
    {
        using var auth = new GoogleAuthService();
        return auth.IsAuthenticated;
    }

    private void RefreshGoogleAuthStatus()
    {
        bool hasCreds = GoogleAuthService.HasCredentials;
        bool authed   = IsAuthenticated();

        if (!hasCreds)
        {
            GoogleAuthStatusText.Text = "OAuth 자격증명 미설정";
            GoogleAuthHintText.Text   = "secrets.props 의 GoogleClientId / GoogleClientSecret 항목을 설정하고 다시 빌드한 뒤 앱을 재시작하세요.";
            GoogleAuthButton.IsEnabled       = false;
            GoogleSignOutButton.IsEnabled    = false;
            GoogleCalendarSaveButton.IsEnabled = false;
            GoogleSyncNowButton.IsEnabled    = false;
            UploadSchoolScheduleButton.IsEnabled = false;
            return;
        }

        if (authed)
        {
            GoogleAuthStatusText.Text = "연결됨 — Google Calendar 동기화 가능";
            GoogleAuthHintText.Text   = "체크박스로 동기화 대상 캘린더를 지정한 뒤 저장하세요.";
            GoogleAuthButton.IsEnabled       = false;
            GoogleSignOutButton.IsEnabled    = true;
            GoogleCalendarSaveButton.IsEnabled = true;
            GoogleSyncNowButton.IsEnabled    = true;
            UploadSchoolScheduleButton.IsEnabled = !string.IsNullOrEmpty(Settings.SchoolCode.Value);
        }
        else
        {
            GoogleAuthStatusText.Text = "연결되지 않음";
            GoogleAuthHintText.Text   = "연결 버튼을 눌러 Google 계정에 로그인하세요.";
            GoogleAuthButton.IsEnabled       = true;
            GoogleSignOutButton.IsEnabled    = false;
            GoogleCalendarSaveButton.IsEnabled = false;
            GoogleSyncNowButton.IsEnabled    = false;
            UploadSchoolScheduleButton.IsEnabled = false;
        }
    }

    private async void OnGoogleAuthClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        GoogleAuthButton.IsEnabled = false;
        try
        {
            using var auth = new GoogleAuthService();
            bool ok = await auth.AuthenticateAsync();
            if (ok)
            {
                Settings.UseGoogle.Set(true);
                UseGoogleToggle.IsChecked = true;
                await FetchAndSaveGoogleCalendarsAsync(auth);
                await LoadGoogleCalendarListAsync();
            }
            else
            {
                GoogleSyncStatusText.Text = "인증이 취소되었거나 실패했습니다.";
            }
        }
        catch (Exception ex)
        {
            GoogleSyncStatusText.Text = $"연결 실패: {ex.Message}";
            Debug.WriteLine($"[CalendarSettingsDialog] 연결 실패: {ex}");
        }
        finally
        {
            RefreshGoogleAuthStatus();
            _busy = false;
        }
    }

    private async void OnGoogleSignOutClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        GoogleSignOutButton.IsEnabled = false;
        try
        {
            using var auth = new GoogleAuthService();
            await auth.SignOutAsync();

            // 로컬 캘린더의 GoogleId / SyncMode / SyncToken 초기화
            using var svc = Scheduler.Scheduler.CreateService();
            var cals = await svc.GetAllCalendarsAsync();
            foreach (var c in cals)
            {
                c.GoogleId  = string.Empty;
                c.SyncMode  = "None";
                c.SyncToken = string.Empty;
                await svc.UpdateCalendarAsync(c);
            }

            _calendarItems.Clear();
            GoogleSyncStatusText.Text = "연결이 해제되었습니다.";
        }
        catch (Exception ex)
        {
            GoogleSyncStatusText.Text = $"해제 실패: {ex.Message}";
        }
        finally
        {
            RefreshGoogleAuthStatus();
            _busy = false;
        }
    }

    // ────────────────────────────────────────────────────
    //  Google 캘린더 자동 매핑 (NewSchool 동등 로직)
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 인증 직후 호출: Google 캘린더 목록을 가져와
    /// (1) primary → 개인 카테고리, (2) 학교명 캘린더(없으면 생성) → 수업/학급/업무 카테고리에 매핑.
    /// </summary>
    private async System.Threading.Tasks.Task FetchAndSaveGoogleCalendarsAsync(GoogleAuthService auth)
    {
        try
        {
            var api = new GoogleCalendarApiClient(auth);
            var googleList = await api.GetCalendarListAsync();

            // 기본(primary) 캘린더
            var primary = googleList.FirstOrDefault(c => c.Primary == true);

            // 학교명 캘린더 — 없으면 생성
            string schoolName = string.IsNullOrWhiteSpace(Settings.SchoolName.Value)
                ? "SaemDesk"
                : Settings.SchoolName.Value;

            var schoolCal = googleList.FirstOrDefault(
                c => string.Equals(c.Summary, schoolName, StringComparison.Ordinal));

            string? schoolCalId = null;
            if (schoolCal != null)
            {
                schoolCalId = schoolCal.Id;
            }
            else
            {
                var created = await api.InsertCalendarAsync(schoolName, "학사 일정 · 수업 · 학급 · 업무");
                schoolCalId = created?.Id;
            }

            // 로컬 캘린더에 GoogleId 매핑
            using var svc = Scheduler.Scheduler.CreateService();
            var locals = await svc.GetAllCalendarsAsync();

            foreach (var local in locals)
            {
                string? newId = local.Title switch
                {
                    CategoryNames.Personal                 => primary?.Id,
                    CategoryNames.Lesson                   => schoolCalId,
                    CategoryNames.Homeroom                 => schoolCalId,
                    CategoryNames.Work                     => schoolCalId,
                    _                                      => null
                };

                if (string.IsNullOrEmpty(newId)) continue;

                local.GoogleId = newId;
                if (local.SyncMode == "None") local.SyncMode = "TwoWay";
                local.SyncToken = string.Empty;
                await svc.UpdateCalendarAsync(local);
            }

            GoogleSyncStatusText.Text = "Google 캘린더 매핑이 완료되었습니다.";
        }
        catch (Exception ex)
        {
            GoogleSyncStatusText.Text = $"캘린더 매핑 실패: {ex.Message}";
            Debug.WriteLine($"[CalendarSettingsDialog] 매핑 실패: {ex}");
        }
    }

    private async System.Threading.Tasks.Task LoadGoogleCalendarListAsync()
    {
        _calendarItems.Clear();
        try
        {
            using var svc = Scheduler.Scheduler.CreateService();
            var locals = await svc.GetAllCalendarsAsync();
            foreach (var c in locals.OrderBy(x => x.SortOrder))
            {
                _calendarItems.Add(new GoogleCalendarCheckItem
                {
                    CalendarNo = c.No,
                    Title      = c.Title,
                    GoogleId   = c.GoogleId,
                    IsChecked  = c.SyncMode == "TwoWay" && !string.IsNullOrEmpty(c.GoogleId),
                });
            }
        }
        catch (Exception ex)
        {
            GoogleSyncStatusText.Text = $"캘린더 목록 조회 실패: {ex.Message}";
        }
    }

    private async void OnGoogleCalendarSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        GoogleCalendarSaveButton.IsEnabled = false;
        try
        {
            using var svc = Scheduler.Scheduler.CreateService();
            var locals = await svc.GetAllCalendarsAsync();
            var byNo   = locals.ToDictionary(c => c.No);

            foreach (var item in _calendarItems)
            {
                if (!byNo.TryGetValue(item.CalendarNo, out var local)) continue;

                string newMode = item.IsChecked && !string.IsNullOrEmpty(item.GoogleId)
                    ? "TwoWay"
                    : "None";

                if (local.SyncMode != newMode)
                {
                    local.SyncMode = newMode;
                    if (newMode == "None") local.SyncToken = string.Empty;
                    await svc.UpdateCalendarAsync(local);
                }
            }
            GoogleSyncStatusText.Text = "동기화 캘린더 설정이 저장되었습니다.";
        }
        catch (Exception ex)
        {
            GoogleSyncStatusText.Text = $"저장 실패: {ex.Message}";
        }
        finally
        {
            GoogleCalendarSaveButton.IsEnabled = true;
            _busy = false;
        }
    }

    // ────────────────────────────────────────────────────
    //  지금 동기화
    // ────────────────────────────────────────────────────

    private async void OnGoogleSyncNowClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        GoogleSyncNowButton.IsEnabled    = false;
        GoogleSyncProgressRing.IsVisible = true;
        GoogleSyncStatusText.Text        = "동기화 중…";
        try
        {
            using var auth = new GoogleAuthService();
            var api = new GoogleCalendarApiClient(auth);
            using var sync = new GoogleSyncService(auth, api);
            var result = await sync.SyncAllAsync();
            GoogleSyncStatusText.Text = result.Summary;
        }
        catch (Exception ex)
        {
            GoogleSyncStatusText.Text = $"동기화 실패: {ex.Message}";
        }
        finally
        {
            GoogleSyncProgressRing.IsVisible = false;
            GoogleSyncNowButton.IsEnabled    = true;
            _busy = false;
        }
    }

    // ────────────────────────────────────────────────────
    //  학사일정 일괄 등록
    // ────────────────────────────────────────────────────

    private async void OnUploadSchoolScheduleClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;

        string schoolCode = Settings.SchoolCode.Value;
        if (string.IsNullOrEmpty(schoolCode))
        {
            UploadScheduleStatusText.Text = "학교가 설정되지 않았습니다. 설정 화면에서 학교를 먼저 검색하세요.";
            return;
        }

        int year = Settings.WorkYear.Value;

        _busy = true;
        UploadSchoolScheduleButton.IsEnabled = false;
        UploadScheduleProgressRing.IsVisible = true;
        UploadScheduleStatusText.Text        = "학사일정 조회 중…";

        try
        {
            // 1. DB에서 학사일정 조회
            using var schedSvc = new SchoolScheduleService(SchoolDatabase.DbPath);
            var (ok, msg, schedules) = await schedSvc.GetSchedulesBySchoolYearAsync(schoolCode, year);
            if (!ok || schedules.Count == 0)
            {
                UploadScheduleStatusText.Text = "등록할 학사일정이 없습니다. 학사일정 페이지에서 먼저 NEIS 동기화를 해 주세요.";
                return;
            }

            // 2. Google 업로드
            UploadScheduleStatusText.Text = $"{schedules.Count}건 → Google 업로드 중…";
            using var auth = new GoogleAuthService();
            var api  = new GoogleCalendarApiClient(auth);
            using var sync = new GoogleSyncService(auth, api);
            var result = await sync.UploadSchoolSchedulesAsync(schedules);

            UploadScheduleStatusText.Text = result.Summary;
        }
        catch (Exception ex)
        {
            UploadScheduleStatusText.Text = $"업로드 실패: {ex.Message}";
            Debug.WriteLine($"[CalendarSettingsDialog] 업로드 실패: {ex}");
        }
        finally
        {
            UploadScheduleProgressRing.IsVisible = false;
            UploadSchoolScheduleButton.IsEnabled = true;
            _busy = false;
        }
    }
}
