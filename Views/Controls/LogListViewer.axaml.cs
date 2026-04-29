using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using SaemDesk.Models;
using SaemDesk.Services;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학생 기록 목록 뷰어 — Avalonia 12 이식.
/// StudentInfoMode/Category 모드별 컬럼 토글 + ViewModel 가시성 동기화.
/// </summary>
public partial class LogListViewer : UserControl
{
    // 기록 내용 TextBox 의 FontSize 제어
    public static readonly StyledProperty<double> LogFontSizeProperty =
        AvaloniaProperty.Register<LogListViewer, double>(nameof(LogFontSize), defaultValue: 12d);

    public double LogFontSize
    {
        get => GetValue(LogFontSizeProperty);
        set => SetValue(LogFontSizeProperty, value);
    }

    private StudentInfoMode _studentInfoMode = StudentInfoMode.NameOnly;
    private LogCategory _category = LogCategory.전체;

    public ObservableCollection<StudentLogViewModel> Logs { get; } = new();

    public StudentInfoMode StudentInfoMode
    {
        get => _studentInfoMode;
        set { _studentInfoMode = value; ApplyStudentInfoMode(); }
    }

    public LogCategory Category
    {
        get => _category;
        set { _category = value; ApplyCategoryMode(); }
    }

    public IEnumerable<StudentLogViewModel> SelectedLogs => Logs.Where(l => l.IsSelected);
    public int SelectedCount => Logs.Count(l => l.IsSelected);

    public event EventHandler<StudentLog>? LogEdited;

    // 현재 포커스된 행
    private StudentLogViewModel? _focusedLog;
    private Border? _focusedBorder;
    private bool _isLoading;

    public LogListViewer()
    {
        InitializeComponent();
        DataContext = this;
        LogItems.ItemsSource = Logs;
        ChkSelectAll.IsCheckedChanged += OnSelectAllChanged;
        ApplyStudentInfoMode();
        ApplyCategoryMode();
    }

    #region Student Info Mode

    private void ApplyStudentInfoMode()
    {
        bool y, sem, gr, cl, num, nm;
        double yW = 60, semW = 50, grW = 50, clW = 50, numW = 50, nmW = 70;

        switch (_studentInfoMode)
        {
            case StudentInfoMode.HideAll:
                y = sem = gr = cl = num = nm = false;
                yW = semW = grW = clW = numW = nmW = 0;
                break;
            case StudentInfoMode.ShowAll:
                y = sem = gr = cl = num = nm = true;
                break;
            case StudentInfoMode.GradeClassNumName:
                y = sem = false; gr = cl = num = nm = true;
                yW = semW = 0;
                break;
            case StudentInfoMode.ClassNumName:
                y = sem = gr = false; cl = num = nm = true;
                yW = semW = grW = 0;
                break;
            case StudentInfoMode.NumName:
                y = sem = gr = cl = false; num = nm = true;
                yW = semW = grW = clW = 0;
                break;
            case StudentInfoMode.NameOnly:
            default:
                y = sem = gr = cl = num = false; nm = true;
                yW = semW = grW = clW = numW = 0;
                break;
        }

        SetHeaderColWidth(1, yW);   TxtYearHeader.IsVisible     = y;
        SetHeaderColWidth(2, semW); TxtSemesterHeader.IsVisible = sem;
        SetHeaderColWidth(5, grW);  TxtGradeHeader.IsVisible    = gr;
        SetHeaderColWidth(6, clW);  TxtClassHeader.IsVisible    = cl;
        SetHeaderColWidth(7, numW); TxtNumberHeader.IsVisible   = num;
        SetHeaderColWidth(8, nmW);  TxtNameHeader.IsVisible     = nm;

        SyncColumnVisibilityToViewModels();
    }

    #endregion

    #region Category Mode

    private void ApplyCategoryMode()
    {
        switch (_category)
        {
            case LogCategory.전체:
                SetHeaderColWidth(3, 80); SetHeaderColWidth(4, 80);
                TxtCategoryHeader.IsVisible = true;
                TxtSubjectHeader.Text = "세부영역"; TxtClassHeader.Text = "소속";
                break;
            case LogCategory.교과활동:
                SetHeaderColWidth(3, 0); SetHeaderColWidth(4, 80);
                TxtCategoryHeader.IsVisible = false;
                TxtSubjectHeader.Text = "과목"; TxtClassHeader.Text = "강의실";
                break;
            case LogCategory.동아리활동:
                SetHeaderColWidth(3, 0); SetHeaderColWidth(4, 80);
                TxtCategoryHeader.IsVisible = false;
                TxtSubjectHeader.Text = "동아리"; TxtClassHeader.Text = "강의실";
                break;
            case LogCategory.개인별세특:
            case LogCategory.봉사활동:
            case LogCategory.상담기록:
            case LogCategory.자율활동:
            case LogCategory.진로활동:
            case LogCategory.종합의견:
                SetHeaderColWidth(3, 0); SetHeaderColWidth(4, 0);
                TxtCategoryHeader.IsVisible = false;
                TxtClassHeader.Text = "학급";
                break;
            case LogCategory.기타:
                SetHeaderColWidth(3, 0); SetHeaderColWidth(4, 0);
                TxtCategoryHeader.IsVisible = false;
                break;
        }

        SyncColumnVisibilityToViewModels();
    }

    private void AdjustColumnsToData()
    {
        if (Logs.Count == 0) return;

        bool hasSubject = Logs.Any(l => !string.IsNullOrWhiteSpace(l.SubjectName));
        if (!hasSubject) SetHeaderColWidth(4, 0);

        if (_category == LogCategory.전체 && Logs.Select(l => l.Category).Distinct().Count() == 1)
        {
            SetHeaderColWidth(3, 0);
            TxtCategoryHeader.IsVisible = false;
        }

        SyncColumnVisibilityToViewModels();
    }

    private void SetHeaderColWidth(int col, double w)
    {
        if (col < 0 || col >= HeaderGrid.ColumnDefinitions.Count) return;
        var cd = HeaderGrid.ColumnDefinitions[col];
        if (w <= 0)
        {
            cd.Width    = new GridLength(0);
            cd.MinWidth = 0;
            cd.MaxWidth = 0;
        }
        else
        {
            cd.MinWidth = 0;
            cd.MaxWidth = double.PositiveInfinity;
            cd.Width    = new GridLength(w);
        }
    }

    /// <summary>헤더 가시성을 각 항목 ViewModel 에 전파 (각 항목 IsVisible 바인딩).</summary>
    private void SyncColumnVisibilityToViewModels()
    {
        foreach (var log in Logs)
        {
            log.YearColumnVisibility     = TxtYearHeader.IsVisible;
            log.SemesterColumnVisibility = TxtSemesterHeader.IsVisible;
            log.CategoryColumnVisibility = TxtCategoryHeader.IsVisible;
            log.SubjectColumnVisibility  = TxtSubjectHeader.IsVisible;
            log.GradeColumnVisibility    = TxtGradeHeader.IsVisible;
            log.ClassColumnVisibility    = TxtClassHeader.IsVisible;
            log.NumberColumnVisibility   = TxtNumberHeader.IsVisible;
            log.NameColumnVisibility     = TxtNameHeader.IsVisible;
        }
    }

    #endregion

    #region Row Click Highlight

    /// <summary>행 클릭 시 왼쪽 액센트 바 하이라이트 + 포커스 로그 기억.</summary>
    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border rowBorder) return;
        if (rowBorder.Tag is not StudentLogViewModel log) return;

        // 이전 포커스 해제
        if (_focusedBorder != null)
        {
            var prev = FindSelectionIndicator(_focusedBorder);
            if (prev != null) prev.Background = Brushes.Transparent;
        }

        _focusedLog = log;
        _focusedBorder = rowBorder;

        var indicator = FindSelectionIndicator(rowBorder);
        if (indicator != null)
            indicator.Background = SolidColorBrush.Parse("#0078D4");
    }

    private static Border? FindSelectionIndicator(Border rowBorder)
    {
        // Border > Grid > Border(SelectionIndicator)
        return rowBorder.GetVisualDescendants()
                        .OfType<Border>()
                        .FirstOrDefault(b => b.Name == "SelectionIndicator");
    }

    #endregion

    #region TextBox Change → Auto Select

    private void OnTopicChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        if (sender is TextBox tb && tb.IsFocused && tb.DataContext is StudentLogViewModel log)
            log.IsSelected = true;
    }

    private void OnLogChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        if (sender is TextBox tb && tb.IsFocused && tb.DataContext is StudentLogViewModel log)
            log.IsSelected = true;
    }

    #endregion

    #region Selection

    private void OnSelectAllChanged(object? sender, RoutedEventArgs e)
    {
        bool? state = ChkSelectAll.IsChecked;
        if (state == true)       foreach (var l in Logs) l.IsSelected = true;
        else if (state == false) foreach (var l in Logs) l.IsSelected = false;
    }

    public void ClearSelection()
    {
        foreach (var l in Logs) l.IsSelected = false;
        ChkSelectAll.IsChecked = false;

        // 하이라이트 해제
        if (_focusedBorder != null)
        {
            var ind = FindSelectionIndicator(_focusedBorder);
            if (ind != null) ind.Background = Brushes.Transparent;
        }
        _focusedLog = null;
        _focusedBorder = null;
    }

    #endregion

    #region Public API

    public void LoadLogs(IEnumerable<StudentLogViewModel> logs)
    {
        _isLoading = true;
        Logs.Clear();
        foreach (var log in logs) Logs.Add(log);
        _isLoading = false;
        SyncColumnVisibilityToViewModels();
        AdjustColumnsToData();
    }

    public async Task AddLog(StudentLog log)
    {
        var vm = await StudentLogViewModel.CreateAsync(log);
        Logs.Insert(0, vm);
    }

    public void Clear() => Logs.Clear();

    public async Task SaveChangedLogsAsync()
    {
        var svc = new StudentLogService();
        try
        {
            foreach (var log in Logs.Where(l => l.IsSelected))
            {
                if (log.No > 0) await svc.UpdateAsync(log.StudentLog);
                else            log.No = await svc.InsertAsync(log.StudentLog);
                log.IsSelected = false;
            }
        }
        finally { svc.Dispose(); }
    }

    public async Task DeleteSelectedLogsAsync()
    {
        var svc = new StudentLogService();
        try
        {
            var toDelete = Logs.Where(l => l.IsSelected).ToList();
            foreach (var vm in toDelete)
            {
                if (vm.No > 0) await svc.DeleteAsync(vm.No);
                Logs.Remove(vm);
            }
        }
        finally { svc.Dispose(); }
    }

    /// <summary>외부 코드 호환 — LogEdited 이벤트 발사용.</summary>
    public void RaiseLogEdited(StudentLog log) => LogEdited?.Invoke(this, log);

    /// <summary>
    /// 포커스된 행 또는 체크된 로그 1건을 StudentLogEditDialog로 전체 편집.
    /// 원본 NewSchool.Controls.LogListViewer.EditSelectedLog() 동등.
    /// </summary>
    public async Task EditSelectedLogAsync()
    {
        var vm = _focusedLog;

        if (vm == null)
        {
            var selected = Logs.Where(l => l.IsSelected).ToList();

            if (selected.Count == 0)
            {
                // TODO: 메시지박스 — "편집할 기록을 선택해주세요."
                return;
            }
            if (selected.Count > 1)
            {
                // TODO: 메시지박스 — "전체 편집은 1건만 선택해주세요."
                return;
            }
            vm = selected[0];
        }

        var log = vm.StudentLog;
        if (log == null) return;

        // 학년·반·번호·이름 포맷 (VM의 StudentInfo 활용)
        string displayName = vm.Grade > 0
            ? $"{vm.Grade}학년 {vm.Class}반 {vm.Number}번 {vm.Name}"
            : vm.Name;

        var dialog = new Views.Dialogs.StudentLogEditDialog(log.StudentID, displayName, log);
        var owner = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
        if (owner != null)
        {
            await dialog.ShowDialog(owner);
            if (dialog.Result != null)
            {
                vm.RefreshFromLog();
                vm.IsSelected = false;
                LogEdited?.Invoke(this, dialog.Result);
            }
        }
    }

    #endregion
}
