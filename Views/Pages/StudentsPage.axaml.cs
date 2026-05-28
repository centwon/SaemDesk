using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 학생 관리 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.StudentManagementPage (WinUI3).
/// 구성: 필터(학년도/학년/반) + 고정 헤더 + 인라인 편집 ListBox.
/// </summary>
public partial class StudentsPage : UserControl
{
    private StudentsPageVM VM => (StudentsPageVM)DataContext!;

    private bool _suppressCheckChange;

    public StudentsPage()
    {
        InitializeComponent();
        DataContext = new StudentsPageVM();

        YearSemPicker.YearSemesterChanged += OnYearSemesterChanged;

        // Students 컬렉션 변경 시 UI 갱신
        VM.Students.CollectionChanged += (_, _) => UpdateUI();
        UpdateUI();
    }

    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        await ClassFilter.LoadAsync(e.Year, e.Semester);
    }

    private async void OnQueryClick(object? sender, RoutedEventArgs e)
    {
        VM.FilterYear  = YearSemPicker.Year;
        VM.FilterGrade = ClassFilter.Grade;
        VM.FilterClass = ClassFilter.ClassNum;
        await VM.LoadStudentsAsync();
        UpdateUI();
    }

    // ────────────────────────────────────────────────────
    //  UI 상태
    // ────────────────────────────────────────────────────

    private void UpdateUI()
    {
        bool has = VM.Students.Count > 0;
        EmptyState.IsVisible  = !has;
        StudentList.IsVisible = has;
    }

    // ────────────────────────────────────────────────────
    //  전체 선택/해제
    // ────────────────────────────────────────────────────

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        if (_suppressCheckChange) return;
        bool select = ChkSelectAll.IsChecked == true;
        VM.ToggleSelectAll(select);
    }

    private void UpdateSelectAllState()
    {
        if (VM.Students.Count == 0)
        {
            _suppressCheckChange = true;
            ChkSelectAll.IsChecked = false;
            _suppressCheckChange = false;
            return;
        }

        int sel = VM.Students.Count(s => s.IsSelected);
        _suppressCheckChange = true;
        ChkSelectAll.IsChecked = sel == 0 ? false
            : sel == VM.Students.Count ? true
            : null;   // Indeterminate
        _suppressCheckChange = false;
    }

    private void OnStudentCheckBoxClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is StudentManagementViewModel vm)
        {
            vm.IsSelected = cb.IsChecked == true;
            VM.NotifySelectionChanged();
            UpdateSelectAllState();
        }
    }

    // ────────────────────────────────────────────────────
    //  인라인 편집
    // ────────────────────────────────────────────────────

    private void OnStudentDataChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is StudentManagementViewModel vm)
            vm.IsModified = true;
    }

    // ────────────────────────────────────────────────────
    //  버튼
    // ────────────────────────────────────────────────────

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await VM.SaveSelectedAsync();
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (!VM.HasSelection) return;

        int count = VM.SelectedCount;
        var dlg   = new ConfirmDialog(
            "학생 삭제",
            $"선택한 {count}명을 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.");
        bool ok = await dlg.ShowDialogAsync(TopLevel.GetTopLevel(this) as Window ?? new Window());
        if (!ok) return;

        await VM.DeleteSelectedAsync();
        UpdateUI();
    }

    private void OnAddStudentClick(object? sender, RoutedEventArgs e)
    {
        // AddStudentsPage 로 네비게이션
        // MainWindowViewModel 을 통해 페이지 전환
        if (TopLevel.GetTopLevel(this) is Window w &&
            w.DataContext is SaemDesk.ViewModels.MainWindowViewModel mvm)
        {
            var target = SaemDesk.ViewModels.MainWindowViewModel.AllNavItems
                .SelectMany(n => n.Children ?? System.Array.Empty<SaemDesk.ViewModels.NavItem>())
                .FirstOrDefault(n => n.Title == "학생 관리");
            if (target is not null) mvm.SelectedNavItem = target;
        }
    }
}
