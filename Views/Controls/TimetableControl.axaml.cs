using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SaemDesk.Services;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Controls;

public enum TimetableDisplayMode
{
    /// <summary>교사용 — 과목 + 강의실</summary>
    Teacher,
    /// <summary>학급용 — 과목 + 교사</summary>
    Class
}

/// <summary>
/// 시간표 표시 — 5일(월~금) × 7교시 그리드.
/// UserControl은 BorderThickness를 지원하지 않으므로
/// 내부 RootBorder를 통해 테두리를 제공한다.
/// 카드에 밀착할 때는 RootBorder.BorderThickness = 0 으로 설정한다.
/// </summary>
public partial class TimetableControl : UserControl
{
    // 오늘 요일 (1=월 ~ 5=금, 주말이면 0)
    private static readonly int TodayColumn =
        DateTime.Today.DayOfWeek switch
        {
            DayOfWeek.Monday    => 1,
            DayOfWeek.Tuesday   => 2,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Thursday  => 4,
            DayOfWeek.Friday    => 5,
            _                   => 0
        };

    // 오늘 열 색상
    private static readonly IBrush TodayHeaderBrush = new SolidColorBrush(Color.Parse("#994FC3F7"));
    private static readonly IBrush TodayCellBrush   = new SolidColorBrush(Color.Parse("#1A4FC3F7"));
    private static readonly IBrush TodayTextBrush   = new SolidColorBrush(Color.Parse("#FF0078D4"));

    // 셀 구분선 색
    private static readonly IBrush DividerBrush = new SolidColorBrush(Color.Parse("#30000000"));

    public static readonly StyledProperty<TimetableDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<TimetableControl, TimetableDisplayMode>(
            nameof(DisplayMode), TimetableDisplayMode.Class);

    public TimetableDisplayMode DisplayMode
    {
        get => GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    public TimetableControl()
    {
        InitializeComponent();
        HighlightTodayHeader();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TimetableViewModel vm) UpdateTimetable(vm);
        };
    }

    /// <summary>오늘 요일 헤더 강조</summary>
    private void HighlightTodayHeader()
    {
        if (TodayColumn == 0) return;

        var (hdr, txt) = TodayColumn switch
        {
            1 => ((Border)HdrMon, (TextBlock)TxtMon),
            2 => ((Border)HdrTue, (TextBlock)TxtTue),
            3 => ((Border)HdrWed, (TextBlock)TxtWed),
            4 => ((Border)HdrThu, (TextBlock)TxtThu),
            5 => ((Border)HdrFri, (TextBlock)TxtFri),
            _ => (null!, null!)
        };
        if (hdr == null) return;

        hdr.Background = TodayHeaderBrush;
        txt.Foreground = TodayTextBrush;
        txt.FontWeight = FontWeight.Bold;
    }

    public async Task LoadTeacherScheduleAsync(string teacherId, int year, int semester)
    {
        DisplayMode = TimetableDisplayMode.Teacher;
        using var svc = new LessonService();
        DataContext = await svc.GetTeacherTimetableViewModelAsync(teacherId, year, semester);
    }

    public async Task LoadClassScheduleAsync(int year, int semester, int grade, int classNum)
    {
        DisplayMode = TimetableDisplayMode.Class;
        using var svc = new LessonService();
        DataContext = await svc.GetClassTimetableViewModelAsync(year, semester, grade, classNum);
    }

    private void UpdateTimetable(TimetableViewModel vm)
    {
        RemoveExistingCells();
        for (int day = 1; day <= 5; day++)
        {
            bool isToday = day == TodayColumn;
            bool lastCol = day == 5;
            for (int period = 1; period <= 7; period++)
            {
                bool lastRow = period == 7;
                var thickness = new Thickness(0, 0, lastCol ? 0 : 1, lastRow ? 0 : 1);

                var item = vm.GetItem(day, period);
                var cell = (item == null || item.IsEmpty)
                    ? CreateEmptyCell(isToday, thickness)
                    : CreateCell(item, isToday, thickness);

                Grid.SetRow(cell, period);
                Grid.SetColumn(cell, day);
                TimetableGrid.Children.Add(cell);
            }
        }
    }

    private void RemoveExistingCells()
    {
        var toRemove = TimetableGrid.Children
            .Where(c => c is Control ctrl && Grid.GetRow(ctrl) > 0 && Grid.GetColumn(ctrl) > 0)
            .ToList();
        foreach (var c in toRemove) TimetableGrid.Children.Remove(c);
    }

    private Border CreateEmptyCell(bool isToday, Thickness borderThickness) => new()
    {
        Padding         = new Thickness(2),
        Background      = isToday ? TodayCellBrush : Brushes.Transparent,
        BorderBrush     = DividerBrush,
        BorderThickness = borderThickness
    };

    private Border CreateCell(TimetableItemViewModel item, bool isToday, Thickness borderThickness)
    {
        var border = new Border
        {
            Padding         = new Thickness(2),
            Background      = isToday ? TodayCellBrush : Brushes.Transparent,
            BorderBrush     = DividerBrush,
            BorderThickness = borderThickness
        };

        var stack = new StackPanel
        {
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 0
        };

        stack.Children.Add(new TextBlock
        {
            Text          = item.SubjectName,
            FontSize      = 12,
            TextAlignment = TextAlignment.Center,
            TextWrapping  = TextWrapping.Wrap
        });

        string sub = DisplayMode == TimetableDisplayMode.Teacher ? item.Room : item.TeacherName;
        if (!string.IsNullOrEmpty(sub))
        {
            stack.Children.Add(new TextBlock
            {
                Text          = sub,
                FontSize      = 10,
                Foreground    = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                TextAlignment = TextAlignment.Center
            });
        }

        border.Child = stack;
        return border;
    }
}
