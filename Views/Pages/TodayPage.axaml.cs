using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 오늘 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.TodayPage (WinUI3).
/// 구성: 좌(시간표 + 학사일정 + 급식) / 우(어젠다 + 메모보드).
/// 4개 로드를 병렬 실행.
/// </summary>
public partial class TodayPage : UserControl
{
    private bool _loaded;

    public TodayPage()
    {
        InitializeComponent();
        DataContext = new TodayPageVM();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            // 렌더링 스레드 과부하 방지: 짧은 지연 선행 후 시작
            await Task.Delay(50);

            // UI 업데이트가 있는 작업들은 의도적으로 분산
            await Safe("시간표",   () => LecTimeTable.LoadMyScheduleAsync());
            await Task.Delay(16); // 한 프레임 대기
            await Safe("학사일정", () => ScheduleList.LoadSchedulesAsync(DateTime.Today, 28, true));
            await Task.Delay(16);
            await Safe("어젠다",   () => AgendaList.LoadPendingAndFutureAsync());
            await Task.Delay(16);
            await Safe("급식",     () => MealBox.LoadMealsAsync(DateTime.Today));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TodayPage] 로드 오류: {ex.Message}");
        }
    }

    private static async Task Safe(string name, Func<Task> load)
    {
        try   { await load(); }
        catch (Exception ex)
        { System.Diagnostics.Debug.WriteLine($"[TodayPage] {name} 실패: {ex.Message}"); }
    }
}
