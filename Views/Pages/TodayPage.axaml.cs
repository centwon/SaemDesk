using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SaemDesk.Services;
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

    // 헤더의 현재 교시 표시 갱신 (비주얼 트리에 붙어 있는 동안만 동작)
    private readonly DispatcherTimer _periodTimer = new() { Interval = TimeSpan.FromMinutes(1) };

    public TodayPage()
    {
        InitializeComponent();
        var vm = new TodayPageVM();
        DataContext = vm;

        // 담임이 아니면 '우리 반' 열을 접어 '내 수업'이 전체 폭을 채우게 함
        if (!vm.IsHomeroom)
        {
            TimetableTable.ColumnDefinitions[1].Width = new GridLength(0);
            ClassHeaderCell.IsVisible = false;
            ClassBodyCell.IsVisible = false;
        }

        _periodTimer.Tick += (_, _) => (DataContext as TodayPageVM)?.RefreshCurrentPeriod();

        Loaded += OnLoaded;
        Unloaded += (_, _) => _periodTimer.Stop();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 현재 교시: 즉시 1회 갱신 후 1분 주기 타이머 시작
        (DataContext as TodayPageVM)?.RefreshCurrentPeriod();
        _periodTimer.Start();

        if (_loaded) return;
        _loaded = true;
        try
        {
            // 렌더링 스레드 과부하 방지: 짧은 지연 선행 후 시작
            await Task.Delay(50);

            // UI 업데이트가 있는 작업들은 의도적으로 분산
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
