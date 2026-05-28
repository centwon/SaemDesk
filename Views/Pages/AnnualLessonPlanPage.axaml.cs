using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SaemDesk.Models;
using SaemDesk.Repositories;
using SaemDesk.Services;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Views.Pages;

public partial class AnnualLessonPlanPage : UserControl
{
    private List<Course> _courses = [];
    private Course? _selectedCourse;
    private List<string> _classColumns = [];
    private List<WeeklyClassHoursRow> _weeklyRows = [];
    private List<SchoolSchedule> _schoolSchedules = [];
    private List<Lesson> _courseSchedules = [];

    private readonly ObservableCollection<CourseSection> _courseSections = [];

    private static IBrush? GetResource(string key)
    {
        if (Application.Current is null) return null;
        Application.Current.TryFindResource(key, out var val);
        return val as IBrush;
    }

    public AnnualLessonPlanPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateSemesterDisplay();
        await LoadSchoolSchedulesAsync();
        await LoadCoursesAsync();
    }

    private void UpdateSemesterDisplay()
    {
        TxtSubtitle.Text = $"{Settings.WorkYear.Value}학년도 {Settings.WorkSemester.Value}학기";
    }

    // ──────────────────────────────────────────────
    //  학사일정 로드 — DB 우선, 없으면 NEIS API
    // ──────────────────────────────────────────────

    private async Task LoadSchoolSchedulesAsync()
    {
        ShowLoading("학사일정을 불러오는 중...");
        try
        {
            int workYear     = Settings.WorkYear.Value;
            int workSemester = Settings.WorkSemester.Value;
            string schoolCode   = Settings.SchoolCode.Value;
            string provinceCode = Settings.ProvinceCode.Value;

            if (workYear == 0 || string.IsNullOrWhiteSpace(schoolCode))
            {
                _schoolSchedules = [];
                ShowHoursInfo("학교/학년도 설정 후 학사일정을 불러올 수 있습니다.", isWarning: true);
                return;
            }

            var (semStart, semEnd) = GetSemesterDateRange(workYear, workSemester);

            // ── 1단계: DB에서 먼저 조회 ──────────────────────────
            using var service = new SchoolScheduleService(SchoolDatabase.DbPath);
            var dbResult = await service.GetSchedulesBySchoolYearAsync(schoolCode, workYear);

            if (dbResult.Success && dbResult.Schedules.Count > 0)
            {
                _schoolSchedules = dbResult.Schedules
                    .Where(s => s.AA_YMD.Date >= semStart.Date && s.AA_YMD.Date <= semEnd.Date)
                    .ToList();

                HoursInfoBorder.IsVisible = false;
                Debug.WriteLine($"[AnnualLessonPlanPage] DB에서 학사일정 {_schoolSchedules.Count}개 로드");
                return;
            }

            // ── 2단계: DB에 없으면 NEIS API 시도 ─────────────────
            if (string.IsNullOrWhiteSpace(provinceCode) ||
                string.IsNullOrWhiteSpace(Settings.NeisApiKey.Value))
            {
                _schoolSchedules = [];
                ShowHoursInfo(
                    "학사일정이 없습니다. [설정 > 학사일정 관리]에서 NEIS 동기화를 먼저 실행해주세요.",
                    isWarning: true);
                return;
            }

            var neisResult = await service.DownloadFromNeisAsync(
                schoolCode, provinceCode, workYear, semStart, semEnd);

            if (neisResult.Success && neisResult.Schedules.Count > 0)
            {
                _schoolSchedules = neisResult.Schedules;
                HoursInfoBorder.IsVisible = false;
                Debug.WriteLine($"[AnnualLessonPlanPage] NEIS에서 학사일정 {_schoolSchedules.Count}개 로드");
            }
            else
            {
                _schoolSchedules = [];
                string msg = neisResult.Success
                    ? "해당 기간의 학사일정이 없습니다. [설정 > 학사일정 관리]에서 NEIS 동기화를 실행해주세요."
                    : $"학사일정 로드 실패: {neisResult.Message}";
                ShowHoursInfo(msg, isWarning: true);
            }
        }
        catch (Exception ex)
        {
            _schoolSchedules = [];
            Debug.WriteLine($"[AnnualLessonPlanPage] 학사일정 로드 오류: {ex.Message}");
            ShowHoursInfo($"학사일정 로드 오류: {ex.Message}", isWarning: false);
        }
        finally
        {
            HideLoading();
        }
    }

    private async Task LoadCoursesAsync()
    {
        ShowLoading("수업 목록을 불러오는 중...");
        try
        {
            using var courseService = new CourseService();
            _courses = await courseService.GetMyCoursesAsync();
            CmbCourse.ItemsSource = _courses;

            if (_courses.Count == 0)
                TxtSubtitle.Text = "등록된 수업이 없습니다. 수업 관리에서 수업을 먼저 등록해주세요.";
        }
        catch (Exception ex)
        {
            TxtSubtitle.Text = $"수업 목록 로드 실패: {ex.Message}";
        }
        finally
        {
            HideLoading();
        }
    }

    // ──────────────────────────────────────────────
    //  수업 선택
    // ──────────────────────────────────────────────

    private async void OnCourseSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CmbCourse.SelectedItem is not Course course) return;

        ShowLoading("시간표와 시수를 계산하는 중...");
        _selectedCourse = course;

        await LoadCourseSchedulesAsync(course.No);
        await BuildWeeklyClassTableAsync(course);
        await LoadCourseSectionsAsync(course.No);

        var (semStart, semEnd) = GetSemesterDateRange(Settings.WorkYear.Value, Settings.WorkSemester.Value);
        DpStartDate.SelectedDate = semStart;
        DpEndDate.SelectedDate   = semEnd;

        if (_classColumns.Count > 0)
            await LoadAllRoomPlacementsAsync(course.No);

        HideLoading();
        TxtSubtitle.Text = $"{Settings.WorkYear.Value}학년도 {Settings.WorkSemester.Value}학기 - {course.Subject}";
    }

    private async Task LoadCourseSchedulesAsync(int courseNo)
    {
        try
        {
            using var repo = new LessonRepository(SchoolDatabase.DbPath);
            var lessons = await repo.GetByCourseAsync(courseNo);
            _courseSchedules = lessons
                .Where(l => l.IsRecurring && !l.IsCancelled)
                .OrderBy(l => l.Room).ThenBy(l => l.DayOfWeek).ThenBy(l => l.Period)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AnnualLessonPlanPage] 시간표 로드 오류: {ex.Message}");
            _courseSchedules = [];
        }
    }

    private async Task LoadCourseSectionsAsync(int courseNo)
    {
        try
        {
            using var repo = new CourseSectionRepository(SchoolDatabase.DbPath);
            var sections = await repo.GetByCourseAsync(courseNo);
            _courseSections.Clear();
            foreach (var s in sections) _courseSections.Add(s);

            SectionListBox.ItemsSource = _courseSections;
            SectionSummaryListBox.ItemsSource = _courseSections;
            UpdateSectionUI();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AnnualLessonPlanPage] 단원 로드 실패: {ex.Message}");
            _courseSections.Clear();
        }
    }

    // ──────────────────────────────────────────────
    //  시수 테이블
    // ──────────────────────────────────────────────

    private async Task BuildWeeklyClassTableAsync(Course course)
    {
        try
        {
            _classColumns = course.RoomList;

            if (_classColumns.Count == 0 || _courseSchedules.Count == 0)
            {
                ClearTable();
                UpdateHoursUI();
                return;
            }

            var roomItems = new List<string> { "전체" };
            roomItems.AddRange(_classColumns);
            CmbRoom.ItemsSource = roomItems;
            if (CmbRoom.SelectedIndex < 0) CmbRoom.SelectedIndex = 0;

            var (semStart, semEnd) = GetSemesterDateRange(Settings.WorkYear.Value, Settings.WorkSemester.Value);
            _weeklyRows = await Task.Run(() => BuildWeeklyRowsWithTimetable(course, semStart, semEnd));

            if (_weeklyRows.Count == 0) { ClearTable(); UpdateHoursUI(); return; }

            BuildTableHeader();
            BuildTableBody();
            UpdateHoursUI();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AnnualLessonPlanPage] 테이블 생성 실패: {ex.Message}");
            UpdateHoursUI();
        }
    }

    private (DateTime Start, DateTime End) GetSemesterDateRange(int year, int semester) =>
        semester == 1
            ? (new DateTime(year, 3, 1), new DateTime(year, 7, 31))
            : (new DateTime(year, 8, 1), new DateTime(year + 1, 2, DateTime.DaysInMonth(year + 1, 2)));

    private List<WeeklyClassHoursRow> BuildWeeklyRowsWithTimetable(Course course, DateTime semStart, DateTime semEnd)
    {
        var rows = new List<WeeklyClassHoursRow>();

        var timetableByRoom = _courseSchedules
            .GroupBy(l => l.Room)
            .ToDictionary(g => g.Key, g => g.GroupBy(l => l.DayOfWeek).ToDictionary(d => d.Key, d => d.Count()));

        var weekStart = semStart;
        while (weekStart.DayOfWeek != DayOfWeek.Monday) weekStart = weekStart.AddDays(1);

        int weekNum = 1;
        while (weekStart <= semEnd)
        {
            var weekEnd = weekStart.AddDays(4);
            if (weekEnd > semEnd) weekEnd = semEnd;

            var classDays  = GetClassDaysInWeek(weekStart, weekEnd, course.Grade);
            var weekEvents = GetWeekEvents(weekStart, weekEnd, course.Grade);

            if (classDays.Count > 0)
            {
                var row = new WeeklyClassHoursRow
                {
                    Week           = weekNum,
                    WeekDisplay    = $"{weekNum}주",
                    DateRange      = $"{weekStart:MM/dd}~{weekEnd:MM/dd}",
                    StartDate      = weekStart,
                    EndDate        = weekEnd,
                    ClassDaysCount = classDays.Count,
                    ClassDays      = classDays,
                    Remark         = weekEvents
                };

                foreach (var room in _classColumns)
                {
                    int roomHours = 0;
                    if (timetableByRoom.TryGetValue(room, out var dayHours))
                    {
                        foreach (var classDay in classDays)
                        {
                            int dow = (int)classDay.DayOfWeek;
                            if (dow == 0) dow = 7;
                            if (dayHours.TryGetValue(dow, out var h)) roomHours += h;
                        }
                    }
                    row.ClassHours[room] = roomHours;
                }

                rows.Add(row);
                weekNum++;
            }
            weekStart = weekStart.AddDays(7);
        }
        return rows;
    }

    private List<DateTime> GetClassDaysInWeek(DateTime weekStart, DateTime weekEnd, int grade)
    {
        var days = new List<DateTime>();
        for (var d = weekStart; d <= weekEnd; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (IsHoliday(d, grade)) continue;
            days.Add(d);
        }
        return days;
    }

    private bool IsHoliday(DateTime date, int grade)
    {
        foreach (var s in _schoolSchedules.Where(s => s.AA_YMD.Date == date.Date))
        {
            if (s.IsHoliday) return true;
            bool isGradeEvent = grade switch
            {
                1 => s.ONE_GRADE_EVENT_YN, 2 => s.TW_GRADE_EVENT_YN,
                3 => s.THREE_GRADE_EVENT_YN, 4 => s.FR_GRADE_EVENT_YN,
                5 => s.FIV_GRADE_EVENT_YN, 6 => s.SIX_GRADE_EVENT_YN, _ => false
            };
            if (isGradeEvent && s.EVENT_NM.Contains("방학")) return true;
        }
        return false;
    }

    private string GetWeekEvents(DateTime weekStart, DateTime weekEnd, int grade)
    {
        var events = new List<string>();
        foreach (var s in _schoolSchedules
            .Where(s => s.AA_YMD.Date >= weekStart.Date && s.AA_YMD.Date <= weekEnd.Date)
            .OrderBy(s => s.AA_YMD))
        {
            if (string.IsNullOrWhiteSpace(s.EVENT_NM)) continue;
            bool isGrade = grade switch
            {
                1 => s.ONE_GRADE_EVENT_YN, 2 => s.TW_GRADE_EVENT_YN,
                3 => s.THREE_GRADE_EVENT_YN, 4 => s.FR_GRADE_EVENT_YN,
                5 => s.FIV_GRADE_EVENT_YN, 6 => s.SIX_GRADE_EVENT_YN, _ => true
            };
            bool isAll = !s.ONE_GRADE_EVENT_YN && !s.TW_GRADE_EVENT_YN && !s.THREE_GRADE_EVENT_YN
                      && !s.FR_GRADE_EVENT_YN  && !s.FIV_GRADE_EVENT_YN && !s.SIX_GRADE_EVENT_YN;

            if (isAll || isGrade)
            {
                string text = $"{s.AA_YMD:M/d} {s.EVENT_NM}";
                if (!events.Contains(text)) events.Add(text);
            }
        }
        return string.Join(", ", events);
    }

    private void ClearTable()
    {
        TableHeader.Children.Clear();
        TableHeader.ColumnDefinitions.Clear();
        _weeklyRows.Clear();
        WeeklyHoursTable.Children.Clear();
    }

    private void BuildTableHeader()
    {
        TableHeader.Children.Clear();
        TableHeader.ColumnDefinitions.Clear();

        int col = 0;
        AddHeaderCol(TableHeader, ref col, "주차", 50);
        AddHeaderCol(TableHeader, ref col, "기간",  100, Avalonia.Layout.HorizontalAlignment.Center);
        AddHeaderCol(TableHeader, ref col, "일수",   45, Avalonia.Layout.HorizontalAlignment.Center);
        foreach (var room in _classColumns)
            AddHeaderCol(TableHeader, ref col, room, 50, Avalonia.Layout.HorizontalAlignment.Center);
        AddHeaderCol(TableHeader, ref col, "합계", 50, Avalonia.Layout.HorizontalAlignment.Center);
        TableHeader.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)) { MinWidth = 150 });
        var remarkHdr = new TextBlock
        {
            Text = "비고 (학사일정)", FontSize = 12, FontWeight = FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(remarkHdr, col);
        TableHeader.Children.Add(remarkHdr);
    }

    private static void AddHeaderCol(Grid grid, ref int col, string text, double width,
        Avalonia.Layout.HorizontalAlignment align = Avalonia.Layout.HorizontalAlignment.Left)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(width)));
        var tb = new TextBlock
        {
            Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = align, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(tb, col++);
        grid.Children.Add(tb);
    }

    private void BuildTableBody()
    {
        WeeklyHoursTable.Children.Clear();

        foreach (var row in _weeklyRows)
        {
            var rowBorder = new Border
            {
                BorderBrush     = GetResource("SystemBaseMediumLowColor") ?? Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(8, 4, 8, 4)
            };

            var rowGrid = new Grid();
            int col = 0;

            AddBodyTextCol(rowGrid, ref col, row.WeekDisplay, 50);
            AddBodyTextCol(rowGrid, ref col, row.DateRange, 100, isSecondary: true,
                align: Avalonia.Layout.HorizontalAlignment.Center);
            AddBodyTextCol(rowGrid, ref col, row.ClassDaysCount.ToString(), 45,
                align: Avalonia.Layout.HorizontalAlignment.Center);

            int totalHours = 0;
            for (int i = 0; i < _classColumns.Count; i++)
            {
                int hours    = row.GetEffectiveHours(_classColumns[i]);
                totalHours  += hours;
                bool isManual = row.ManualHours.TryGetValue(_classColumns[i], out var manual) && manual.HasValue;

                rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));

                var cellBorder = new Border
                {
                    Background    = Brushes.Transparent,
                    CornerRadius  = new CornerRadius(2),
                    Padding       = new Thickness(2),
                    Cursor        = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };
                ToolTip.SetTip(cellBorder, "클릭하여 편집");

                var hoursText = new TextBlock
                {
                    Text               = hours.ToString(),
                    FontSize           = 12,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment  = Avalonia.Layout.VerticalAlignment.Center
                };
                if (isManual)
                {
                    hoursText.Foreground = GetResource("SystemAccentColor");
                    hoursText.FontWeight = FontWeight.SemiBold;
                }
                cellBorder.Child = hoursText;

                cellBorder.PointerEntered += (s, _) =>
                {
                    if (s is Border b) b.Background = GetResource("SystemListLowColor") ?? Brushes.Transparent;
                };
                cellBorder.PointerExited += (s, _) =>
                {
                    if (s is Border b) b.Background = Brushes.Transparent;
                };

                string capturedRoom     = _classColumns[i];
                int    capturedBodyCol  = col;
                cellBorder.PointerPressed += (_, _) =>
                    OnHoursCellTapped(cellBorder, row, capturedRoom, rowGrid, capturedBodyCol);

                Grid.SetColumn(cellBorder, col++);
                rowGrid.Children.Add(cellBorder);
            }

            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
            var totalTb = new TextBlock
            {
                Text                = totalHours.ToString(),
                FontSize            = 12,
                FontWeight          = FontWeight.SemiBold,
                Foreground          = GetResource("SystemAccentColor"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(totalTb, col++);
            rowGrid.Children.Add(totalTb);

            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)) { MinWidth = 150 });
            var remarkTb = new TextBlock
            {
                Text              = row.Remark,
                FontSize          = 11,
                Foreground        = GetResource("SystemBaseMediumColor"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming      = Avalonia.Media.TextTrimming.CharacterEllipsis
            };
            ToolTip.SetTip(remarkTb, row.Remark);
            Grid.SetColumn(remarkTb, col);
            rowGrid.Children.Add(remarkTb);

            rowBorder.Child = rowGrid;
            WeeklyHoursTable.Children.Add(rowBorder);
        }

        BuildSummaryRow();
    }

    private void AddBodyTextCol(Grid grid, ref int col, string text, double width,
        bool isSecondary = false,
        Avalonia.Layout.HorizontalAlignment align = Avalonia.Layout.HorizontalAlignment.Left)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(width)));
        var tb = new TextBlock
        {
            Text               = text,
            FontSize           = 12,
            HorizontalAlignment = align,
            VerticalAlignment  = Avalonia.Layout.VerticalAlignment.Center
        };
        if (isSecondary) tb.Foreground = GetResource("SystemBaseMediumColor");
        Grid.SetColumn(tb, col++);
        grid.Children.Add(tb);
    }

    private void BuildSummaryRow()
    {
        var border = new Border { Background = GetResource("SystemListLowColor"), Padding = new Thickness(8, 6) };
        var grid   = new Grid();
        int col    = 0;

        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
        var lbl = new TextBlock { Text = "합계", FontWeight = FontWeight.Bold, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Grid.SetColumn(lbl, col++); grid.Children.Add(lbl);

        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(100))); col++;
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(45)));
        var daysTb = new TextBlock
        {
            Text = _weeklyRows.Sum(r => r.ClassDaysCount).ToString(), FontWeight = FontWeight.Bold, FontSize = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(daysTb, col++); grid.Children.Add(daysTb);

        int grand = 0;
        for (int i = 0; i < _classColumns.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
            int ct = _weeklyRows.Sum(r => r.GetEffectiveHours(_classColumns[i]));
            grand += ct;
            var ctTb = new TextBlock
            {
                Text = ct.ToString(), FontWeight = FontWeight.Bold, FontSize = 12,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(ctTb, col++); grid.Children.Add(ctTb);
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(50)));
        var grandTb = new TextBlock
        {
            Text = grand.ToString(), FontWeight = FontWeight.Bold, FontSize = 12,
            Foreground = GetResource("SystemAccentColor"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(grandTb, col++); grid.Children.Add(grandTb);

        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)) { MinWidth = 150 });
        border.Child = grid;
        WeeklyHoursTable.Children.Add(border);
    }

    private void OnHoursCellTapped(Border cellBorder, WeeklyClassHoursRow row, string className, Grid rowGrid, int colIndex)
    {
        if (cellBorder.Child is NumericUpDown) return;

        int current = row.GetEffectiveHours(className);
        var nud = new NumericUpDown
        {
            Value = current, Minimum = 0, Maximum = 20,
            Width = 44, FontSize = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        cellBorder.Child = nud;
        nud.Focus();

        nud.LostFocus += (_, _) =>
        {
            int newVal = (int)(nud.Value ?? 0);
            row.ManualHours[className] = newVal;

            var tb = new TextBlock
            {
                Text = newVal.ToString(), FontSize = 12,
                Foreground = GetResource("SystemAccentColor"), FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Center
            };
            cellBorder.Child = tb;

            UpdateRowTotal(rowGrid, row);
            UpdateSummaryRow();
            UpdateStatisticsDisplay();
        };
    }

    private void UpdateRowTotal(Grid rowGrid, WeeklyClassHoursRow row)
    {
        int totalColIndex = 3 + _classColumns.Count;
        int total = _classColumns.Sum(c => row.GetEffectiveHours(c));
        foreach (var child in rowGrid.Children)
            if (child is TextBlock tb && Grid.GetColumn(tb) == totalColIndex) { tb.Text = total.ToString(); break; }
    }

    private void UpdateSummaryRow()
    {
        if (WeeklyHoursTable.Children.Count == 0) return;
        if (WeeklyHoursTable.Children[^1] is Border b && b.Child is Grid g)
        {
            int grand = 0;
            for (int i = 0; i < _classColumns.Count; i++)
            {
                int ct = _weeklyRows.Sum(r => r.GetEffectiveHours(_classColumns[i]));
                grand += ct;
                int targetCol = 3 + i;
                foreach (var child in g.Children)
                    if (child is TextBlock tb && Grid.GetColumn(tb) == targetCol) { tb.Text = ct.ToString(); break; }
            }
            int grandCol = 3 + _classColumns.Count;
            foreach (var child in g.Children)
                if (child is TextBlock tb && Grid.GetColumn(tb) == grandCol) { tb.Text = grand.ToString(); break; }
        }
    }

    // ──────────────────────────────────────────────
    //  UI 업데이트 헬퍼
    // ──────────────────────────────────────────────

    private void UpdateHoursUI()
    {
        if (_selectedCourse == null)
        {
            TxtTimetableInfo.Text = "수업을 선택하세요";
            TimetableItemsControl.ItemsSource = null;
            TxtScheduleInfo.Text = "학사일정을 불러오는 중...";
            HoursEmptyState.IsVisible = true;
            WeeklyHoursScrollViewer.IsVisible = false;
            return;
        }

        UpdateTimetableInfo();
        UpdateScheduleInfo(_selectedCourse.Grade);

        bool hasRows = _weeklyRows.Count > 0;
        HoursEmptyState.IsVisible = !hasRows;
        WeeklyHoursScrollViewer.IsVisible = hasRows;

        if (!hasRows)
        {
            TxtHoursEmptyMessage.Text = _classColumns.Count == 0 ? "학급 목록이 없습니다"
                                      : _courseSchedules.Count == 0 ? "시간표 배치가 없습니다"
                                      : "수업일이 없습니다";
        }

        UpdateStatisticsDisplay();
    }

    private void UpdateTimetableInfo()
    {
        if (_courseSchedules.Count == 0)
        {
            TxtTimetableInfo.Text = "⚠️ 시간표 배치가 없습니다.\n수업 관리에서 '시간표 배치'를 먼저 설정하세요.";
            TimetableItemsControl.ItemsSource = null;
            return;
        }

        var items = _courseSchedules.GroupBy(l => l.Room).Select(rg =>
        {
            var dayParts = rg.GroupBy(l => l.DayOfWeek).OrderBy(d => d.Key)
                .Select(dg =>
                {
                    string day = dg.Key switch { 1=>"월", 2=>"화", 3=>"수", 4=>"목", 5=>"금", _=>"" };
                    return $"{day}{dg.Count()}";
                });
            return $"{rg.Key}: {string.Join(", ", dayParts)}";
        }).ToList();

        TxtTimetableInfo.Text = $"주당 {_courseSchedules.Count}시간 ({_classColumns.Count}개 학급)";
        TimetableItemsControl.ItemsSource = items;
    }

    private void UpdateScheduleInfo(int grade)
    {
        if (_schoolSchedules.Count == 0)
        {
            TxtScheduleInfo.Text = "⚠️ 학사일정 없음 — [설정 > 학사일정 관리]에서 NEIS 동기화를 실행하세요.";
            return;
        }

        var (semStart, semEnd) = GetSemesterDateRange(Settings.WorkYear.Value, Settings.WorkSemester.Value);
        int holidayCount = _schoolSchedules.Count(s => s.AA_YMD >= semStart && s.AA_YMD <= semEnd && s.IsHoliday);
        int eventCount   = _schoolSchedules.Count;
        TxtScheduleInfo.Text = $"📅 학사일정 {eventCount}개 / 공휴일·휴업 {holidayCount}일";
    }

    private void UpdateStatisticsDisplay()
    {
        TxtTotalWeeks.Text    = $"{_weeklyRows.Count}주";
        TxtTotalClassDays.Text = $"{_weeklyRows.Sum(r => r.ClassDaysCount)}일";

        int totalAuto    = _weeklyRows.Sum(r => _classColumns.Sum(c => r.GetEffectiveHours(c)));
        int totalPlanned = _courseSections.Sum(s => s.EstimatedHours);
        TxtAutoHours.Text    = totalAuto.ToString();
        TxtPlannedHours.Text = totalPlanned.ToString();
        TxtUnitSummary.Text  = $"{_courseSections.Count}개 단원 / {totalPlanned}시간";

        if (totalPlanned > 0 && totalAuto > 0)
        {
            int diff = totalAuto - totalPlanned;
            if (diff > 0)
                ShowHoursInfo($"총 시수({totalAuto})가 단원 시수({totalPlanned})보다 {diff}시간 많습니다.", isWarning: false);
            else if (diff < 0)
                ShowHoursInfo($"단원 시수({totalPlanned})가 총 시수({totalAuto})보다 {-diff}시간 많습니다.", isWarning: true);
            else
                HoursInfoBorder.IsVisible = false;
        }
        else
        {
            HoursInfoBorder.IsVisible = false;
        }
    }

    private void ShowHoursInfo(string message, bool isWarning)
    {
        TxtHoursInfo.Text = message;
        HoursInfoBorder.Background = GetResource(
            isWarning ? "SystemFillColorCautionBackgroundBrush" : "SystemListLowColor");
        HoursInfoBorder.IsVisible = true;
    }

    private void UpdateSectionUI()
    {
        bool has = _courseSections.Count > 0;
        SectionEmptyState.IsVisible = !has;
        SectionListBox.IsVisible    = has;
        SectionListHeader.IsVisible = has;

        if (has)
        {
            int unitCount      = _courseSections.Select(s => s.UnitNo).Distinct().Count();
            int chapterCount   = _courseSections.Select(s => (s.UnitNo, s.ChapterNo)).Distinct().Count();
            int totalHours     = _courseSections.Sum(s => s.EstimatedHours);
            int examCount      = _courseSections.Count(s => s.SectionType == "Exam");
            int assessmentCount= _courseSections.Count(s => s.SectionType == "Assessment");

            TxtSectionStatistics.Text = $"대단원 {unitCount}개 · 중단원 {chapterCount}개 · 소단원 {_courseSections.Count}개 · 총 {totalHours}차시"
                + (examCount + assessmentCount > 0 ? $" | 지필 {examCount}개 · 수행 {assessmentCount}개" : "");

            TxtNormalCount.Text    = $"일반: {_courseSections.Count(s => s.SectionType == "Normal")}";
            TxtExamCount.Text      = $"지필: {examCount}";
            TxtAssessmentCount.Text= $"수행: {assessmentCount}";
            TxtPinnedCount.Text    = $"📌 고정: {_courseSections.Count(s => s.IsPinned)}";
        }
        else
        {
            TxtSectionStatistics.Text = "";
            TxtNormalCount.Text    = "일반: 0";
            TxtExamCount.Text      = "지필: 0";
            TxtAssessmentCount.Text= "수행: 0";
            TxtPinnedCount.Text    = "📌 고정: 0";
        }

        UpdateStatisticsDisplay();
    }

    // ──────────────────────────────────────────────
    //  단원 관리 이벤트
    // ──────────────────────────────────────────────

    private void OnSectionSelectionChanged(object? sender, SelectionChangedEventArgs e) { }

    private async void OnAddSectionClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCourse == null) { ShowSectionError("먼저 수업을 선택해주세요."); return; }
        var dialog = new CourseSectionDialog(_selectedCourse, null);
        await DialogService.ShowAsync(dialog);
        if (dialog.IsSuccess) await LoadCourseSectionsAsync(_selectedCourse.No);
    }

    private async void OnDeleteSectionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CourseSection section)
        {
            bool confirmed = await DialogService.ShowConfirmAsync("삭제 확인",
                $"\"{section.SectionName}\" 단원을 삭제하시겠습니까?");
            if (!confirmed) return;
            try
            {
                using var repo = new CourseSectionRepository(SchoolDatabase.DbPath);
                await repo.DeleteAsync(section.No);
                _courseSections.Remove(section);
                UpdateSectionUI();
            }
            catch (Exception ex) { ShowSectionError($"삭제 오류: {ex.Message}"); }
        }
    }

    private async void OnClearAllClick(object? sender, RoutedEventArgs e)
    {
        if (_courseSections.Count == 0) { ShowSectionError("삭제할 단원이 없습니다."); return; }
        if (_selectedCourse == null) return;
        bool confirmed = await DialogService.ShowConfirmAsync("전체 삭제",
            $"{_courseSections.Count}개의 단원을 모두 삭제하시겠습니까?");
        if (!confirmed) return;
        try
        {
            using var repo = new CourseSectionRepository(SchoolDatabase.DbPath);
            await repo.DeleteByCourseAsync(_selectedCourse.No);
            _courseSections.Clear();
            UpdateSectionUI();
        }
        catch (Exception ex) { ShowSectionError($"전체 삭제 오류: {ex.Message}"); }
    }

    private void ShowSectionError(string message)
    {
        TxtSectionError.Text = message;
        SectionErrorBorder.IsVisible = true;
    }

    private void OnGoToHoursTabClick(object? sender, RoutedEventArgs e) =>
        MainTabControl.SelectedIndex = 1;

    // ──────────────────────────────────────────────
    //  CSV Import/Export
    // ──────────────────────────────────────────────

    private async void OnImportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCourse == null) { ShowSectionError("먼저 수업을 선택해주세요."); return; }
        try
        {
            string? path = await App.FilePicker.OpenFileAsync(".csv");
            if (string.IsNullOrEmpty(path)) return;
            var content  = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var sections = ParseCsv(content);
            if (sections.Count == 0) { ShowSectionError("유효한 단원 데이터가 없습니다."); return; }
            if (_courseSections.Count > 0)
            {
                bool ok = await DialogService.ShowConfirmAsync("CSV 가져오기",
                    $"기존 {_courseSections.Count}개 단원이 삭제되고 {sections.Count}개 단원이 추가됩니다.\n계속하시겠습니까?");
                if (!ok) return;
            }
            await ApplyImportSectionsAsync(sections);
        }
        catch (Exception ex) { ShowSectionError($"CSV 가져오기 오류: {ex.Message}"); }
    }

    private async Task ApplyImportSectionsAsync(List<CourseSection> sections)
    {
        if (_selectedCourse == null) return;
        try
        {
            using var repo = new CourseSectionRepository(SchoolDatabase.DbPath);
            await repo.BulkCreateAsync(_selectedCourse.No, sections);
            _courseSections.Clear();
            foreach (var s in sections) _courseSections.Add(s);
            UpdateSectionUI();
        }
        catch (Exception ex) { ShowSectionError($"저장 오류: {ex.Message}"); }
    }

    private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (_courseSections.Count == 0) { ShowSectionError("내보낼 단원이 없습니다."); return; }
        try
        {
            string? path = await App.FilePicker.SaveFileAsync($"{_selectedCourse?.Subject ?? "단원"}_단원구조", ".csv");
            if (string.IsNullOrEmpty(path)) return;
            await File.WriteAllTextAsync(path, GenerateCsv(), Encoding.UTF8);
        }
        catch (Exception ex) { ShowSectionError($"CSV 내보내기 오류: {ex.Message}"); }
    }

    private async void OnDownloadTemplateClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? path = await App.FilePicker.SaveFileAsync("단원구조_템플릿", ".csv");
            if (string.IsNullOrEmpty(path)) return;
            await File.WriteAllTextAsync(path, GenerateCsvTemplate(), Encoding.UTF8);
        }
        catch (Exception ex) { ShowSectionError($"템플릿 다운로드 오류: {ex.Message}"); }
    }

    private List<CourseSection> ParseCsv(string content)
    {
        var sections = new List<CourseSection>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);
            if (fields.Length < 6) continue;
            try
            {
                var section = new CourseSection
                {
                    UnitNo    = int.TryParse(fields[0], out var u) ? u : 0,
                    UnitName  = fields[1].Trim(),
                    ChapterNo = int.TryParse(fields[2], out var c) ? c : 0,
                    ChapterName = fields[3].Trim(),
                    SectionNo = int.TryParse(fields[4], out var s) ? s : 0,
                    SectionName = fields[5].Trim(),
                    StartPage = fields.Length > 6 && int.TryParse(fields[6], out var sp) ? sp : 0,
                    EndPage   = fields.Length > 7 && int.TryParse(fields[7], out var ep) ? ep : 0,
                    EstimatedHours = fields.Length > 8 && int.TryParse(fields[8], out var h) && h > 0 ? h : 1,
                    SectionType = fields.Length > 9 && !string.IsNullOrWhiteSpace(fields[9]) ? fields[9].Trim() : "Normal",
                    LearningObjective = fields.Length > 10 ? fields[10].Trim() : "",
                    LessonPlan  = fields.Length > 11 ? fields[11].Trim() : "",
                    MaterialPath= fields.Length > 12 ? fields[12].Trim() : "",
                    MaterialUrl = fields.Length > 13 ? fields[13].Trim() : "",
                    Memo        = fields.Length > 14 ? fields[14].Trim() : ""
                };
                if (fields.Length > 15 && DateTime.TryParse(fields[15].Trim(), out var pd))
                { section.IsPinned = true; section.PinnedDate = pd; }
                if (section.SectionType is "Exam" or "Assessment") section.IsPinned = true;
                if (section.UnitNo > 0 && !string.IsNullOrWhiteSpace(section.SectionName))
                    sections.Add(section);
            }
            catch { }
        }
        return sections;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        foreach (char ch in line)
        {
            if (ch == '"') inQuotes = !inQuotes;
            else if (ch == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(ch);
        }
        fields.Add(current.ToString());
        return [.. fields];
    }

    private string GenerateCsv()
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine("대단원번호,대단원명,중단원번호,중단원명,소단원번호,소단원명,시작페이지,끝페이지,예상차시,유형,학습목표,수업계획,자료파일,자료링크,메모,고정날짜");
        foreach (var s in _courseSections)
            sb.AppendLine(string.Join(",",
                s.UnitNo, Esc(s.UnitName), s.ChapterNo, Esc(s.ChapterName),
                s.SectionNo, Esc(s.SectionName), s.StartPage, s.EndPage,
                s.EstimatedHours, s.SectionType,
                Esc(s.LearningObjective), Esc(s.LessonPlan),
                Esc(s.MaterialPath), Esc(s.MaterialUrl), Esc(s.Memo),
                s.PinnedDate?.ToString("yyyy-MM-dd") ?? ""));
        return sb.ToString();
    }

    private static string GenerateCsvTemplate()
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine("대단원번호,대단원명,중단원번호,중단원명,소단원번호,소단원명,시작페이지,끝페이지,예상차시,유형,학습목표,수업계획,자료파일,자료링크,메모,고정날짜");
        sb.AppendLine("1,수와 연산,1,자연수의 혼합 계산,1,덧셈과 뺄셈의 혼합 계산,8,11,2,Normal,덧셈과 뺄셈의 혼합 계산 순서를 안다,개념 도입 → 연습,,,,");
        sb.AppendLine("0,1학기 중간고사,0,지필평가,1,1단원 평가,0,0,1,Exam,,,,,, 2026-04-15");
        return sb.ToString();
    }

    private static string Esc(string? f)
    {
        if (string.IsNullOrEmpty(f)) return "";
        if (f.Contains(',') || f.Contains('"') || f.Contains('\n'))
            return $"\"{f.Replace("\"", "\"\"")}\"";
        return f;
    }

    // ──────────────────────────────────────────────
    //  NEIS 새로고침
    // ──────────────────────────────────────────────

    private async void OnRefreshScheduleClick(object? sender, RoutedEventArgs e)
    {
        await LoadSchoolSchedulesAsync();
        if (_selectedCourse != null)
            await BuildWeeklyClassTableAsync(_selectedCourse);
    }

    // ──────────────────────────────────────────────
    //  단원 배치 (탭3)
    // ──────────────────────────────────────────────

    private async void OnRoomSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedCourse == null || CmbRoom.SelectedItem == null) return;
        string room = CmbRoom.SelectedItem.ToString()!;
        BtnGeneratePlan.IsEnabled = room != "전체";
        if (room == "전체") await LoadAllRoomPlacementsAsync(_selectedCourse.No);
        else await LoadPlacementResultsAsync(_selectedCourse.No, room);
    }

    private async void OnGeneratePlanClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCourse == null) { ShowSectionError("먼저 수업을 선택해주세요."); return; }
        if (_courseSections.Count == 0) { ShowSectionError("먼저 단원을 추가해주세요."); return; }
        if (CmbRoom.SelectedItem?.ToString() == "전체" || CmbRoom.SelectedItem == null)
        { ShowSectionError("개별 학급을 선택해주세요."); return; }
        if (DpStartDate.SelectedDate == null || DpEndDate.SelectedDate == null)
        { ShowSectionError("배치 기간을 설정해주세요."); return; }

        string room      = CmbRoom.SelectedItem.ToString()!;
        DateTime startDate = DpStartDate.SelectedDate.Value;
        DateTime endDate   = DpEndDate.SelectedDate.Value;

        ShowLoading("배치 미리보기 중...");
        try
        {
            using var scheduleRepo     = new ScheduleRepository(SchoolDatabase.DbPath);
            using var mapRepo          = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            using var sectionRepo      = new CourseSectionRepository(SchoolDatabase.DbPath);
            using var lessonRepo       = new LessonRepository(SchoolDatabase.DbPath);
            using var schoolScheduleRepo = new SchoolScheduleRepository(SchoolDatabase.DbPath);

            var engine  = new SchedulingEngine(scheduleRepo, mapRepo, sectionRepo, lessonRepo, schoolScheduleRepo);
            var preview = await engine.PreviewScheduleAsync(_selectedCourse.No, room, startDate, endDate);
            HideLoading();

            string confirmMsg = $"학급: {room}\n기간: {startDate:yyyy.MM.dd} ~ {endDate:yyyy.MM.dd}\n\n"
                + $"단원: {preview.TotalSections}개 (고정 {preview.PinnedSections} + 일반 {preview.NormalSections})\n"
                + $"필요 시수: {preview.TotalRequiredHours}차시 / 가용 슬롯: {preview.TotalAvailableSlots}개\n"
                + $"{(preview.CanComplete ? "✅ 배치 가능" : $"⚠️ 시수 부족: {-preview.ExcessSlots}차시")}\n\n"
                + "기존 배치 데이터를 삭제하고 새로 배치합니다. 계속하시겠습니까?";

            bool confirmed = await DialogService.ShowConfirmAsync("자동 배치 확인", confirmMsg);
            if (!confirmed) return;

            ShowLoading("배치 실행 중...");
            var result = await engine.GenerateScheduleAsync(_selectedCourse.No, room, startDate, endDate, clearExisting: true);
            HideLoading();

            string resultMsg = $"{result.Message}\n고정 배치: {result.AnchoredCount}개, 순차 배치: {result.FilledCount}개";
            if (result.UnplacedCount > 0) resultMsg += $"\n미배치: {result.UnplacedCount}개";

            await DialogService.ShowInfoAsync(resultMsg);
            await LoadPlacementResultsAsync(_selectedCourse.No, room);
            await RefreshUndoRedoButtonsAsync();
        }
        catch (Exception ex) { HideLoading(); ShowSectionError($"자동 배치 오류: {ex.Message}"); }
    }

    private async Task LoadPlacementResultsAsync(int courseNo, string room)
    {
        try
        {
            using var scheduleRepo = new ScheduleRepository(SchoolDatabase.DbPath);
            using var mapRepo      = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            using var sectionRepo  = new CourseSectionRepository(SchoolDatabase.DbPath);

            var schedules = (await scheduleRepo.GetByCourseAndRoomAsync(courseNo, room))
                .OrderBy(s => s.Date).ThenBy(s => s.Period).ToList();

            if (schedules.Count == 0) { UnitPlanListBox.ItemsSource = null; return; }

            var allSections = await sectionRepo.GetByCourseAsync(courseNo);
            var sectionDict = allSections.ToDictionary(s => s.No);
            var progress    = new Dictionary<int, int>();
            var displayItems= new List<PlacementDisplayItem>();
            var semStart    = schedules.First().Date;

            var weekSchedules = new List<(Schedule s, List<ScheduleUnitMap> maps)>();
            foreach (var s in schedules)
                weekSchedules.Add((s, await mapRepo.GetByScheduleWithSectionAsync(s.No)));

            foreach (var wg in weekSchedules.GroupBy(ws => GetWeekNumber(ws.s.Date, semStart)).OrderBy(g => g.Key))
            {
                var dates = wg.Select(ws => ws.s.Date).Distinct().OrderBy(d => d).ToList();
                string range = dates.Count > 1 ? $"{dates.First():M/d}~{dates.Last():M/d}" : $"{dates.First():M/d}";
                displayItems.Add(new PlacementDisplayItem { IsHeader = true, WeekNumber = wg.Key, WeekRange = range, WeekSlotCount = wg.Count() });

                foreach (var (schedule, maps) in wg.OrderBy(ws => ws.s.Date).ThenBy(ws => ws.s.Period))
                    foreach (var map in maps)
                    {
                        if (!sectionDict.TryGetValue(map.CourseSectionId, out var section)) continue;
                        progress[section.No] = progress.GetValueOrDefault(section.No) + 1;
                        int cur = progress[section.No];
                        displayItems.Add(new PlacementDisplayItem
                        {
                            IsHeader = false, Date = schedule.Date, Period = schedule.Period,
                            SectionName = section.SectionName, SectionType = section.SectionType,
                            ProgressDisplay = section.EstimatedHours > 1 ? $"({cur}/{section.EstimatedHours})" : "",
                            IsPinned = section.IsPinned, ScheduleNo = schedule.No,
                            SectionNo = section.No, MapNo = map.No
                        });
                    }
            }
            UnitPlanListBox.ItemsSource = displayItems;
        }
        catch (Exception ex) { Debug.WriteLine($"[AnnualLessonPlanPage] 배치 결과 로드 실패: {ex.Message}"); }
    }

    private async Task LoadAllRoomPlacementsAsync(int courseNo)
    {
        try
        {
            using var scheduleRepo = new ScheduleRepository(SchoolDatabase.DbPath);
            using var mapRepo      = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            using var sectionRepo  = new CourseSectionRepository(SchoolDatabase.DbPath);

            var allSchedules = new List<Schedule>();
            foreach (var room in _classColumns)
                allSchedules.AddRange(await scheduleRepo.GetByCourseAndRoomAsync(courseNo, room));

            allSchedules = allSchedules.OrderBy(s => s.Date).ThenBy(s => s.Period).ThenBy(s => s.Room).ToList();
            if (allSchedules.Count == 0) { UnitPlanListBox.ItemsSource = null; return; }

            var allSections  = await sectionRepo.GetByCourseAsync(courseNo);
            var sectionDict  = allSections.ToDictionary(s => s.No);
            var progress     = new Dictionary<(int, string), int>();
            var displayItems = new List<PlacementDisplayItem>();
            var semStart     = allSchedules.First().Date;

            var weekSchedules = new List<(Schedule s, List<ScheduleUnitMap> maps)>();
            foreach (var s in allSchedules)
                weekSchedules.Add((s, await mapRepo.GetByScheduleWithSectionAsync(s.No)));

            foreach (var wg in weekSchedules.GroupBy(ws => GetWeekNumber(ws.s.Date, semStart)).OrderBy(g => g.Key))
            {
                var dates = wg.Select(ws => ws.s.Date).Distinct().OrderBy(d => d).ToList();
                string range = dates.Count > 1 ? $"{dates.First():M/d}~{dates.Last():M/d}" : $"{dates.First():M/d}";
                displayItems.Add(new PlacementDisplayItem { IsHeader = true, WeekNumber = wg.Key, WeekRange = range, WeekSlotCount = wg.Count() });

                foreach (var (schedule, maps) in wg.OrderBy(ws => ws.s.Date).ThenBy(ws => ws.s.Period).ThenBy(ws => ws.s.Room))
                    foreach (var map in maps)
                    {
                        if (!sectionDict.TryGetValue(map.CourseSectionId, out var section)) continue;
                        var key = (section.No, schedule.Room);
                        progress[key] = progress.GetValueOrDefault(key) + 1;
                        int cur = progress[key];
                        displayItems.Add(new PlacementDisplayItem
                        {
                            IsHeader = false, Date = schedule.Date, Period = schedule.Period,
                            Room = schedule.Room, SectionName = section.SectionName, SectionType = section.SectionType,
                            ProgressDisplay = section.EstimatedHours > 1 ? $"({cur}/{section.EstimatedHours})" : "",
                            IsPinned = section.IsPinned, ScheduleNo = schedule.No,
                            SectionNo = section.No, MapNo = map.No
                        });
                    }
            }
            UnitPlanListBox.ItemsSource = displayItems;
        }
        catch (Exception ex) { Debug.WriteLine($"[AnnualLessonPlanPage] 전체 배치 로드 실패: {ex.Message}"); }
    }

    private async Task RefreshPlacementListAsync()
    {
        if (_selectedCourse == null || CmbRoom.SelectedItem == null) return;
        string room = CmbRoom.SelectedItem.ToString()!;
        if (room == "전체") await LoadAllRoomPlacementsAsync(_selectedCourse.No);
        else await LoadPlacementResultsAsync(_selectedCourse.No, room);
    }

    private void OnPlacementSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool isSlot = UnitPlanListBox.SelectedItem is PlacementDisplayItem item && !item.IsHeader;
        BtnChangeUnit.IsEnabled = isSlot;
        BtnRemoveUnit.IsEnabled = isSlot;
    }

    private async void OnChangeUnitClick(object? sender, RoutedEventArgs e)
    {
        if (UnitPlanListBox.SelectedItem is not PlacementDisplayItem item || item.IsHeader) return;
        if (_selectedCourse == null) return;

        var sectionNames = _courseSections.Select(s => s.SectionName).ToList();
        var cb = new ComboBox { ItemsSource = sectionNames, Width = 350 };
        int curIdx = _courseSections.ToList().FindIndex(s => s.No == item.SectionNo);
        if (curIdx >= 0) cb.SelectedIndex = curIdx;

        bool confirmed = await DialogService.ShowCustomAsync($"단원 변경 - {item.DateDisplay} {item.PeriodDisplay}", cb);
        if (!confirmed || cb.SelectedIndex < 0) return;

        var newSection = _courseSections[cb.SelectedIndex];
        if (newSection.No == item.SectionNo) return;

        try
        {
            using var mapRepo = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            await mapRepo.DeleteAsync(item.MapNo);
            await mapRepo.AddUnitToScheduleAsync(item.ScheduleNo, newSection.No);
            await RefreshPlacementListAsync();
        }
        catch (Exception ex) { ShowSectionError($"단원 변경 오류: {ex.Message}"); }
    }

    private async void OnRemoveUnitClick(object? sender, RoutedEventArgs e)
    {
        if (UnitPlanListBox.SelectedItem is not PlacementDisplayItem item || item.IsHeader) return;
        bool confirmed = await DialogService.ShowConfirmAsync("배치 삭제",
            $"{item.DateDisplay} {item.PeriodDisplay}\n'{item.SectionName}' 배치를 삭제하시겠습니까?");
        if (!confirmed) return;
        try
        {
            using var mapRepo = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            await mapRepo.DeleteAsync(item.MapNo);
            await RefreshPlacementListAsync();
        }
        catch (Exception ex) { ShowSectionError($"배치 삭제 오류: {ex.Message}"); }
    }

    // ──────────────────────────────────────────────
    //  Undo/Redo
    // ──────────────────────────────────────────────

    private async void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCourse == null || CmbRoom.SelectedItem?.ToString() == "전체") return;
        ShowLoading("작업을 취소하는 중...");
        try
        {
            string room = CmbRoom.SelectedItem!.ToString()!;
            using var scheduleRepo = new ScheduleRepository(SchoolDatabase.DbPath);
            using var mapRepo      = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            using var undoRepo     = new UndoHistoryRepository(SchoolDatabase.DbPath);
            using var lessonRepo   = new LessonRepository(SchoolDatabase.DbPath);
            using var schoolScheduleRepo = new SchoolScheduleRepository(SchoolDatabase.DbPath);
            var svc    = new ScheduleShiftService(scheduleRepo, mapRepo, undoRepo, lessonRepo, schoolScheduleRepo);
            var result = await svc.UndoLastActionAsync(_selectedCourse.No, room);
            HideLoading();
            await DialogService.ShowInfoAsync(result.Message);
            if (result.Success) await RefreshUndoRedoButtonsAsync();
        }
        finally { HideLoading(); }
    }

    private async void OnRedoClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCourse == null || CmbRoom.SelectedItem?.ToString() == "전체") return;
        ShowLoading("작업을 다시 실행하는 중...");
        try
        {
            string room = CmbRoom.SelectedItem!.ToString()!;
            using var scheduleRepo = new ScheduleRepository(SchoolDatabase.DbPath);
            using var mapRepo      = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            using var undoRepo     = new UndoHistoryRepository(SchoolDatabase.DbPath);
            using var lessonRepo   = new LessonRepository(SchoolDatabase.DbPath);
            using var schoolScheduleRepo = new SchoolScheduleRepository(SchoolDatabase.DbPath);
            var svc    = new ScheduleShiftService(scheduleRepo, mapRepo, undoRepo, lessonRepo, schoolScheduleRepo);
            var result = await svc.RedoLastActionAsync(_selectedCourse.No, room);
            HideLoading();
            await DialogService.ShowInfoAsync(result.Message);
            if (result.Success) await RefreshUndoRedoButtonsAsync();
        }
        finally { HideLoading(); }
    }

    private async Task RefreshUndoRedoButtonsAsync()
    {
        if (_selectedCourse == null || CmbRoom.SelectedItem?.ToString() == "전체")
        { BtnUndo.IsEnabled = false; BtnRedo.IsEnabled = false; return; }
        try
        {
            string room = CmbRoom.SelectedItem!.ToString()!;
            using var undoRepo = new UndoHistoryRepository(SchoolDatabase.DbPath);
            BtnUndo.IsEnabled = await undoRepo.CanUndoAsync(_selectedCourse.No, room);
            BtnRedo.IsEnabled = await undoRepo.CanRedoAsync(_selectedCourse.No, room);
        }
        catch { BtnUndo.IsEnabled = false; BtnRedo.IsEnabled = false; }
    }

    // ──────────────────────────────────────────────
    //  엑셀 내보내기
    // ──────────────────────────────────────────────

    private async void OnExportExcelClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedCourse == null) { ShowSectionError("먼저 수업을 선택해주세요."); return; }
        if (_courseSections.Count == 0) { ShowSectionError("내보낼 단원이 없습니다."); return; }
        try
        {
            string? path = await App.FilePicker.SaveFileAsync(
                $"연간수업계획_{_selectedCourse.Subject}_{DateTime.Now:yyyyMMdd}", ".xlsx");
            if (string.IsNullOrEmpty(path)) return;
            ShowLoading("엑셀 내보내는 중...");
            using var sectionRepo  = new CourseSectionRepository(SchoolDatabase.DbPath);
            using var scheduleRepo = new ScheduleRepository(SchoolDatabase.DbPath);
            using var mapRepo      = new ScheduleUnitMapRepository(SchoolDatabase.DbPath);
            using var progressRepo = new LessonProgressRepository(SchoolDatabase.DbPath);
            var exportService = new ReportExportService(sectionRepo, scheduleRepo, mapRepo, progressRepo);
            var result = await exportService.ExportYearPlanToExcelAsync(
                _selectedCourse, path, Settings.WorkYear.Value, Settings.WorkSemester.Value);
            HideLoading();
            await DialogService.ShowInfoAsync(result.Message);
        }
        catch (Exception ex) { HideLoading(); ShowSectionError($"엑셀 내보내기 오류: {ex.Message}"); }
    }

    // ──────────────────────────────────────────────
    //  유틸리티
    // ──────────────────────────────────────────────

    private void ShowLoading(string message)
    {
        TxtLoading.Text = message;
        LoadingOverlay.IsVisible = true;
    }

    private void HideLoading() => LoadingOverlay.IsVisible = false;

    private static int GetWeekNumber(DateTime date, DateTime start) =>
        (int)((date.Date - start.Date).TotalDays / 7) + 1;
}

// ──────────────────────────────────────────────────────────────
//  Helper Classes
// ──────────────────────────────────────────────────────────────

public class WeeklyClassHoursRow
{
    public int Week { get; set; }
    public string WeekDisplay { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int ClassDaysCount { get; set; }
    public List<DateTime> ClassDays { get; set; } = [];
    public Dictionary<string, int> ClassHours { get; set; } = [];
    public Dictionary<string, int?> ManualHours { get; set; } = [];
    public string Remark { get; set; } = string.Empty;

    public int GetEffectiveHours(string className)
    {
        if (ManualHours.TryGetValue(className, out var manual) && manual.HasValue) return manual.Value;
        return ClassHours.TryGetValue(className, out var auto) ? auto : 0;
    }
}

public class PlacementDisplayItem
{
    public bool IsHeader { get; set; }
    public bool IsSlot => !IsHeader;

    public int WeekNumber { get; set; }
    public string WeekRange { get; set; } = string.Empty;
    public int WeekSlotCount { get; set; }

    public DateTime Date { get; set; }
    public int Period { get; set; }
    public string Room { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
    public string ProgressDisplay { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public int ScheduleNo { get; set; }
    public int SectionNo { get; set; }
    public int MapNo { get; set; }

    public string RoomDisplay => string.IsNullOrEmpty(Room) ? "" : $"[{Room}]";
    public string DateDisplay => IsHeader ? "" : $"{Date:M/d}({DayName})";
    public string PeriodDisplay => IsHeader ? "" : $"{Period}교시";
    public string HeaderDisplay => IsHeader ? $"[{WeekNumber}주차] {WeekRange}  ({WeekSlotCount}차시)" : "";
    public string TypeIcon => SectionType switch
    {
        "Exam" => "📝", "Assessment" => "📊", "Event" => "🎉", _ => "📖"
    };

    private string DayName => Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "월", DayOfWeek.Tuesday => "화",
        DayOfWeek.Wednesday => "수", DayOfWeek.Thursday => "목",
        DayOfWeek.Friday => "금", DayOfWeek.Saturday => "토",
        DayOfWeek.Sunday => "일", _ => ""
    };
}
