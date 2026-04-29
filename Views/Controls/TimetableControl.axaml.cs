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
/// 시간표 표시 — 5일(월~금) × 7교시 그리드. NewSchool TimetableControl 동등 이식.
/// </summary>
public partial class TimetableControl : UserControl
{
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
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TimetableViewModel vm) UpdateTimetable(vm);
        };
    }

    public async Task LoadTeacherScheduleAsync(string teacherId, int year, int semester)
    {
        DisplayMode = TimetableDisplayMode.Teacher;
        using var svc = new LessonService();
        DataContext = await svc.GetTeacherTimetableViewModelAsync(teacherId, year, semester);
    }

    public async Task LoadMyScheduleAsync()
    {
        DisplayMode = TimetableDisplayMode.Teacher;
        using var svc = new LessonService();
        DataContext = await svc.GetMyTimetableViewModelAsync();
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
            for (int period = 1; period <= 7; period++)
            {
                var item = vm.GetItem(day, period);
                var cell = item != null ? CreateCell(item) : CreateEmptyCell();
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

    private static Border CreateEmptyCell() => new()
    {
        Padding = new Thickness(2),
        Background = new SolidColorBrush(Color.Parse("#10000000"))
    };

    private Border CreateCell(TimetableItemViewModel item)
    {
        var border = new Border { Padding = new Thickness(2) };

        if (item.IsEmpty)
        {
            border.Background = new SolidColorBrush(Color.Parse("#10000000"));
            return border;
        }

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 0
        };

        stack.Children.Add(new TextBlock
        {
            Text = item.SubjectName,
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        string sub = DisplayMode == TimetableDisplayMode.Teacher ? item.Room : item.TeacherName;
        if (!string.IsNullOrEmpty(sub))
        {
            stack.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                TextAlignment = TextAlignment.Center
            });
        }

        border.Child = stack;
        border.Background = new SolidColorBrush(Colors.White);
        return border;
    }
}
