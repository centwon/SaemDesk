using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Collections;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 오늘 페이지 ViewModel — 날짜·인사말·학급 현황 대시보드.
/// </summary>
public partial class TodayPageVM : ViewModelBase
{
    // ── 날짜 / 인사말 (정적, 앱 시작 시 1회 계산) ──────────
    public string TodayDateText  { get; } = DateTime.Today.ToString("yyyy년 M월 d일");
    public string TodayDayOfWeek { get; } = GetKoreanDayOfWeek(DateTime.Today.DayOfWeek);
    public string TodayGreeting  { get; } = BuildGreeting();

    // ── 현재 교시 (View 타이머가 1분마다 RefreshCurrentPeriod 호출) ──
    [ObservableProperty] private string _currentPeriodText = Functions.GetPeriodNow().Name;

    public void RefreshCurrentPeriod() => CurrentPeriodText = Functions.GetPeriodNow().Name;

    // ── 오늘 학사일정 (행사 있는 날만 헤더에 표시) ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTodayEvent))]
    private string _todayEventText = string.Empty;

    public bool HasTodayEvent => !string.IsNullOrEmpty(TodayEventText);

    // ── 학교 / 담임 정보 ──────────────────────────────────
    public string SchoolName       { get; } = string.IsNullOrWhiteSpace(Settings.SchoolName)
                                              ? "학교명을 설정해 주세요"
                                              : Settings.SchoolName.Value;
    public string TeacherInfo      { get; } = BuildTeacherInfo();
    public string WorkYearSemester { get; } = $"{Settings.WorkYear}학년도 {Settings.WorkSemester}학기";

    /// <summary>담임 여부 — 학년·반이 모두 설정되어 있으면 true. "우리 반" 시간표 표시 조건.</summary>
    public bool IsHomeroom { get; } = Settings.HomeGrade > 0 && Settings.HomeRoom > 0;

    // ── 안내 메시지 ───────────────────────────────────────
    public string HintText { get; }
    public bool   HasHint  { get; }

    // ── 학급 오늘 시간표 ─────────────────────────────────────
    public OptimizedObservableCollection<ClassTimetable> TodaySlots { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTodaySlots))]
    private bool _timetableLoaded;

    public bool HasTodaySlots => TodaySlots.Count > 0;

    // ── 교사 오늘 시간표 ─────────────────────────────────────
    public OptimizedObservableCollection<TimetableItemViewModel> TodayTeacherSlots { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTodayTeacherSlots))]
    private bool _teacherTimetableLoaded;

    public bool HasTodayTeacherSlots => TodayTeacherSlots.Count > 0;

    // ── 학생 현황 ─────────────────────────────────────────
    [ObservableProperty] private int    _studentCount;
    [ObservableProperty] private string _studentCountText = "–";

    // ── 오늘 일지 ─────────────────────────────────────────
    [ObservableProperty] private bool   _diaryExists;
    [ObservableProperty] private string _diaryStatusText = "미작성";

    // ── 급식 ─────────────────────────────────────────────
    public OptimizedObservableCollection<string> MealItems { get; } = new();
    [ObservableProperty] private bool   _hasMeal;
    [ObservableProperty] private string _mealCalories  = string.Empty;
    [ObservableProperty] private string _mealStatusText = string.Empty;

    // ── 로딩 ─────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading;

    // ── HTTP (재사용) ─────────────────────────────────────
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public TodayPageVM()
    {
        HintText = BuildHint();
        HasHint  = !string.IsNullOrEmpty(HintText);

        _ = LoadDashboardAsync();
    }

    // ────────────────────────────────────────────────────
    //  대시보드 로드
    // ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadDashboardAsync()
    {
        IsLoading = true;
        try
        {
            await Task.WhenAll(
                LoadStudentCountAsync(),
                LoadTodayTimetableAsync(),
                LoadTodayTeacherTimetableAsync(),
                LoadDiaryStatusAsync(),
                LoadTodayScheduleAsync(),
                LoadMealAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TodayPageVM] 대시보드 로드 오류: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadStudentCountAsync()
    {
        try
        {
            using var repo = new EnrollmentRepository(SchoolDatabase.DbPath);
            var list = await repo.GetByClassAsync(
                Settings.SchoolCode.Value,
                Settings.WorkYear.Value,
                Settings.HomeGrade.Value,
                Settings.HomeRoom.Value);

            StudentCount     = list.Count;
            StudentCountText = $"{list.Count}명";
        }
        catch
        {
            StudentCountText = "–";
        }
    }

    private async Task LoadTodayTimetableAsync()
    {
        try
        {
            // .NET DayOfWeek: 0=Sun, 1=Mon … 5=Fri, 6=Sat
            // ClassTimetable.DayOfWeek: 1=Mon … 5=Fri
            int netDow = (int)DateTime.Today.DayOfWeek;
            int dow    = netDow >= 1 && netDow <= 5 ? netDow : 0;

            if (dow == 0)
            {
                TimetableLoaded = true;
                return; // 주말
            }

            using var repo = new ClassTimetableRepository(SchoolDatabase.DbPath);
            var all = await repo.GetByClassAsync(
                Settings.SchoolCode.Value,
                Settings.WorkYear.Value,
                Settings.WorkSemester.Value,
                Settings.HomeGrade.Value,
                Settings.HomeRoom.Value);

            TodaySlots.ReplaceAll(all.Where(x => x.DayOfWeek == dow).OrderBy(x => x.Period));

            TimetableLoaded = true;
            OnPropertyChanged(nameof(HasTodaySlots));
        }
        catch
        {
            TimetableLoaded = true;
        }
    }

    private async Task LoadTodayTeacherTimetableAsync()
    {
        try
        {
            int netDow = (int)DateTime.Today.DayOfWeek;
            int dow = netDow >= 1 && netDow <= 5 ? netDow : 0;

            if (dow == 0)
            {
                TeacherTimetableLoaded = true;
                return;
            }

            using var svc = new LessonService();
            var vm = await svc.GetTeacherTimetableViewModelAsync(
                Settings.User.Value, Settings.WorkYear.Value, Settings.WorkSemester.Value);

            var todayItems = vm.Items
                .Where(x => x.DayOfWeek == dow && !x.IsEmpty)
                .OrderBy(x => x.Period)
                .ToList();

            TodayTeacherSlots.ReplaceAll(todayItems);
            TeacherTimetableLoaded = true;
            OnPropertyChanged(nameof(HasTodayTeacherSlots));
        }
        catch
        {
            TeacherTimetableLoaded = true;
        }
    }

    private async Task LoadDiaryStatusAsync()
    {
        try
        {
            using var repo = new ClassDiaryRepository(SchoolDatabase.DbPath);
            var diary = await repo.GetByDateAsync(
                Settings.SchoolCode.Value,
                Settings.WorkYear.Value,
                Settings.HomeGrade.Value,
                Settings.HomeRoom.Value,
                DateTime.Today);

            DiaryExists    = diary is not null;
            DiaryStatusText = diary is not null ? "작성 완료" : "미작성";
        }
        catch
        {
            DiaryStatusText = "–";
        }
    }

    private async Task LoadTodayScheduleAsync()
    {
        try
        {
            using var svc = new SchoolScheduleService(SchoolDatabase.DbPath);
            // GetByDateRangeAsync 의 상한은 배타적(AA_YMD < EndDate) → 오늘 하루는 [Today, Today+1)
            var (_, _, list) = await svc.GetSchedulesByDataRangeAsync(
                Settings.SchoolCode.Value, DateTime.Today, DateTime.Today.AddDays(1));

            var names = list
                .Where(s => !string.IsNullOrWhiteSpace(s.EVENT_NM))
                .Select(s => s.EVENT_NM.Trim())
                .Distinct()
                .ToList();

            TodayEventText = names.Count switch
            {
                0 => string.Empty,
                1 => names[0],
                _ => $"{names[0]} 외 {names.Count - 1}",
            };
        }
        catch
        {
            TodayEventText = string.Empty;
        }
    }

    private async Task LoadMealAsync()
    {
        // API 키 또는 학교 코드 미설정이면 안내 메시지만 표시
        if (string.IsNullOrWhiteSpace(Settings.NeisApiKey.Value) ||
            string.IsNullOrWhiteSpace(Settings.SchoolCode.Value))
        {
            MealStatusText = "설정에서 학교와 NEIS API 키를 등록하면 급식 정보가 표시됩니다.";
            return;
        }

        try
        {
            string date   = DateTime.Today.ToString("yyyyMMdd");
            string url    = $"https://open.neis.go.kr/hub/mealServiceDietInfo"
                          + $"?KEY={Settings.NeisApiKey.Value}"
                          + $"&Type=xml"
                          + $"&ATPT_OFCDC_SC_CODE={Settings.ProvinceCode.Value}"
                          + $"&SD_SCHUL_CODE={Settings.SchoolCode.Value}"
                          + $"&MLSV_YMD={date}"
                          + $"&MMEAL_SC_CODE=2";   // 2 = 중식만

            var xml    = await _http.GetStringAsync(url);
            var doc    = XDocument.Parse(xml);

            // NEIS 오류 코드 체크
            var code   = doc.Descendants("CODE").FirstOrDefault()?.Value;
            if (code is "INFO-200")             // 데이터 없음
            {
                MealStatusText = "오늘의 급식 정보가 없습니다.";
                return;
            }
            if (code is not null && code != "INFO-000")
            {
                MealStatusText = "급식 정보를 가져올 수 없습니다.";
                return;
            }

            var row    = doc.Descendants("row").FirstOrDefault();
            if (row is null)
            {
                MealStatusText = "오늘의 급식 정보가 없습니다.";
                return;
            }

            // DDISH_NM: "김치찌개 1.9.13.<br/>쌀밥 1.<br/>..." 형식
            var raw    = row.Element("DDISH_NM")?.Value ?? string.Empty;
            var items  = raw
                .Split(new[] { "<br/>", "<br />" },
                       System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s*\d[\d.,]*$", "").Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            MealItems.ReplaceAll(items);

            MealCalories  = row.Element("CAL_INFO")?.Value ?? string.Empty;
            HasMeal       = MealItems.Count > 0;
            MealStatusText = string.Empty;
        }
        catch (TaskCanceledException)
        {
            MealStatusText = "급식 정보 요청 시간이 초과되었습니다.";
        }
        catch (HttpRequestException)
        {
            MealStatusText = "급식 정보를 가져올 수 없습니다. (네트워크 오류)";
        }
        catch (Exception ex)
        {
            MealStatusText = "급식 정보 로드 중 오류가 발생했습니다.";
            System.Diagnostics.Debug.WriteLine($"[TodayPageVM] 급식 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  헬퍼
    // ────────────────────────────────────────────────────

    private static string GetKoreanDayOfWeek(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday    => "월요일",
        DayOfWeek.Tuesday   => "화요일",
        DayOfWeek.Wednesday => "수요일",
        DayOfWeek.Thursday  => "목요일",
        DayOfWeek.Friday    => "금요일",
        DayOfWeek.Saturday  => "토요일",
        _                   => "일요일",
    };

    private static string BuildGreeting()
    {
        int    h       = DateTime.Now.Hour;
        string name    = Settings.UserName;
        string phrase  = h < 12 ? "좋은 아침이에요" : h < 18 ? "좋은 오후예요" : "좋은 저녁이에요";
        return string.IsNullOrWhiteSpace(name) ? $"{phrase}!" : $"{name} 선생님, {phrase}!";
    }

    private static string BuildTeacherInfo()
    {
        int grade = Settings.HomeGrade;
        int room  = Settings.HomeRoom;
        return grade <= 0 || room <= 0
            ? "담임 정보를 설정해 주세요"
            : $"{grade}학년 {room}반 담임";
    }

    private static string BuildHint()
    {
        bool schoolSet = !string.IsNullOrWhiteSpace(Settings.SchoolCode);
        bool nameSet   = !string.IsNullOrWhiteSpace(Settings.UserName);

        if (!schoolSet && !nameSet) return "설정 메뉴에서 학교와 이름을 먼저 설정해 주세요.";
        if (!schoolSet)             return "설정 메뉴에서 학교를 검색해 등록해 주세요.";
        return string.Empty;
    }
}
