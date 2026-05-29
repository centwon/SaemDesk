using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SaemDesk.Models;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class ClassTimetablePage : UserControl
{
    private static readonly IBrush DividerBrush = new SolidColorBrush(Color.Parse("#30000000"));
    private static readonly IBrush HoverBrush   = new SolidColorBrush(Color.Parse("#08000000"));
    private static readonly IBrush SubTextBrush  = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));

    private readonly ClassTimetablePageVM _vm;

    public ClassTimetablePage()
    {
        InitializeComponent();
        _vm = new ClassTimetablePageVM();
        DataContext = _vm;
        _vm.DataChanged += RefreshCells;

        // 필터 이벤트 연결 (초기 로드는 ClassPicker.Loaded → ClassChanged 가 구동)
        YearSemPicker.YearSemesterChanged += OnYearSemesterChanged;
        ClassFilter.ClassChanged          += OnClassFilterChanged;
    }

    // 학년도·학기 변경 → ClassPicker 재로드
    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        await ClassFilter.LoadAsync(e.Year, e.Semester);
    }

    // 학급 변경 → 해당 학급 시간표 로드
    private async void OnClassFilterChanged(object? sender, ClassChangedEventArgs e)
    {
        await _vm.LoadAsync(e.Year, e.Semester, e.Grade, e.Class);
    }

    private void RefreshCells()
    {
        var toRemove = new List<Control>();
        foreach (var child in TimetableGrid.Children)
        {
            if (child is Control ctrl && Grid.GetRow(ctrl) > 0 && Grid.GetColumn(ctrl) > 0)
                toRemove.Add(ctrl);
        }
        foreach (var c in toRemove)
            TimetableGrid.Children.Remove(c);

        for (int day = 1; day <= 5; day++)
        {
            for (int period = 1; period <= 7; period++)
            {
                var slot = _vm.GetSlot(day, period);
                var cell = BuildCell(day, period, slot);
                Grid.SetRow(cell, period);
                Grid.SetColumn(cell, day);
                TimetableGrid.Children.Add(cell);
            }
        }
    }

    private Border BuildCell(int day, int period, ClassTimetable? slot)
    {
        bool lastCol = day == 5;
        bool lastRow = period == 7;
        bool editMode = _vm.IsEditMode;

        var border = new Border
        {
            BorderBrush     = DividerBrush,
            BorderThickness = new Thickness(0, 0, lastCol ? 0 : 1, lastRow ? 0 : 1),
            Padding         = new Thickness(4),
            Background      = Brushes.Transparent,
            Cursor          = editMode ? new Cursor(StandardCursorType.Hand) : null,
            Tag             = new int[] { day, period },
        };

        if (slot != null && !string.IsNullOrEmpty(slot.SubjectName))
        {
            var stack = new StackPanel
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing             = 0,
                IsHitTestVisible    = false,
            };

            stack.Children.Add(new TextBlock
            {
                Text          = slot.SubjectName,
                FontSize      = 13,
                TextAlignment = TextAlignment.Center,
                TextWrapping  = TextWrapping.Wrap,
            });

            if (!string.IsNullOrEmpty(slot.TeacherName))
            {
                stack.Children.Add(new TextBlock
                {
                    Text          = slot.TeacherName,
                    FontSize      = 10,
                    Foreground    = SubTextBrush,
                    TextAlignment = TextAlignment.Center,
                });
            }

            border.Child = stack;
        }
        else if (editMode)
        {
            // 편집 모드에서만 빈 칸에 추가 힌트 표시
            border.Child = new TextBlock
            {
                Text                = "+",
                FontSize            = 16,
                Foreground          = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                IsHitTestVisible    = false,
            };
        }

        if (editMode)
        {
            border.Tapped         += OnCellTapped;
            border.PointerEntered += (_, _) => border.Background = HoverBrush;
            border.PointerExited  += (_, _) => border.Background = Brushes.Transparent;
        }

        return border;
    }

    private void OnCellTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border border && border.Tag is int[] pos && pos.Length == 2)
            _vm.BeginEdit(pos[0], pos[1]);
    }
}
