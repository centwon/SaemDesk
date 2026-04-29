using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SaemDesk.Models;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학생 목록 UserControl (Avalonia 12 + ListBox)
/// Enrollment 모델 직접 사용. NewSchool ListStudent 1:1 이식.
///
/// 열 너비는 StyledProperty → XAML 바인딩으로 제어.
/// ShowCheckBox  : 체크박스 + 하단 카운터 표시 여부 (다중선택 UI)
/// AllowMultiSelect : 체크박스 없이 다중 선택 허용 (자리배정 등)
/// </summary>
public partial class ListStudent : UserControl
{
    public enum View { Full, ClassNumName, NumName, NameOnly }

    public static readonly DataFormat<Enrollment> EnrollmentDragFormat =
        DataFormat.CreateInProcessFormat<Enrollment>("saemdesk.enrollment");

    // ── StyledProperty: XAML 바인딩 전용 ────────────────

    public static readonly StyledProperty<bool> ShowCheckBoxProperty =
        AvaloniaProperty.Register<ListStudent, bool>(nameof(ShowCheckBox), defaultValue: false);

    public static readonly StyledProperty<bool> AllowMultiSelectProperty =
        AvaloniaProperty.Register<ListStudent, bool>(nameof(AllowMultiSelect), defaultValue: false);

    public static readonly StyledProperty<GridLength> CheckColumnWidthProperty =
        AvaloniaProperty.Register<ListStudent, GridLength>(nameof(CheckColumnWidth), defaultValue: new GridLength(0));

    public static readonly StyledProperty<GridLength> GradeColumnWidthProperty =
        AvaloniaProperty.Register<ListStudent, GridLength>(nameof(GradeColumnWidth), defaultValue: new GridLength(0));

    public static readonly StyledProperty<GridLength> ClassColumnWidthProperty =
        AvaloniaProperty.Register<ListStudent, GridLength>(nameof(ClassColumnWidth), defaultValue: new GridLength(0));

    public static readonly StyledProperty<GridLength> NumberColumnWidthProperty =
        AvaloniaProperty.Register<ListStudent, GridLength>(nameof(NumberColumnWidth), defaultValue: new GridLength(50));

    public static readonly StyledProperty<bool> ShowGradeColumnProperty =
        AvaloniaProperty.Register<ListStudent, bool>(nameof(ShowGradeColumn), defaultValue: false);

    public static readonly StyledProperty<bool> ShowClassColumnProperty =
        AvaloniaProperty.Register<ListStudent, bool>(nameof(ShowClassColumn), defaultValue: false);

    // ── 공개 프로퍼티 ────────────────────────────────────

    public bool ShowCheckBox
    {
        get => GetValue(ShowCheckBoxProperty);
        set
        {
            SetValue(ShowCheckBoxProperty, value);
            ApplyCheckBoxVisibility(value);
        }
    }

    /// <summary>
    /// 체크박스 UI 없이 다중 선택만 허용.
    /// ShowCheckBox=True 이면 이 값과 무관하게 다중선택이 켜진다.
    /// </summary>
    public bool AllowMultiSelect
    {
        get => GetValue(AllowMultiSelectProperty);
        set
        {
            SetValue(AllowMultiSelectProperty, value);
            ApplySelectionMode();
        }
    }

    public GridLength CheckColumnWidth
    {
        get => GetValue(CheckColumnWidthProperty);
        private set => SetValue(CheckColumnWidthProperty, value);
    }

    public GridLength GradeColumnWidth
    {
        get => GetValue(GradeColumnWidthProperty);
        private set => SetValue(GradeColumnWidthProperty, value);
    }

    public GridLength ClassColumnWidth
    {
        get => GetValue(ClassColumnWidthProperty);
        private set => SetValue(ClassColumnWidthProperty, value);
    }

    public GridLength NumberColumnWidth
    {
        get => GetValue(NumberColumnWidthProperty);
        private set => SetValue(NumberColumnWidthProperty, value);
    }

    public bool ShowGradeColumn
    {
        get => GetValue(ShowGradeColumnProperty);
        private set => SetValue(ShowGradeColumnProperty, value);
    }

    public bool ShowClassColumn
    {
        get => GetValue(ShowClassColumnProperty);
        private set => SetValue(ShowClassColumnProperty, value);
    }

    // ── 이벤트 ───────────────────────────────────────────

    /// <summary>단일 선택 모드 전용 — 아이템 선택 시 발화.</summary>
    public event EventHandler<Enrollment>? StudentSelected;

    /// <summary>
    /// ShowCheckBox 모드에서 선택 수 변경 시 발화. arg = 현재 선택된 학생 수.
    /// </summary>
    public event EventHandler<int>? SelectionChangedNotify;

    // ── 내부 필드 ────────────────────────────────────────

    private View _viewMode = View.NumName;
    private bool _suppressSelectAllEvent = false;

    public ObservableCollection<Enrollment> Students { get; } = new();

    public View ViewMode
    {
        get => _viewMode;
        set { _viewMode = value; ApplyViewMode(); }
    }

    /// <summary>아이템 우클릭 시 표시할 컨텍스트 메뉴 (사용처에서 주입)</summary>
    public ContextMenu? ItemContextFlyout { get; set; }

    // ── 생성자 ───────────────────────────────────────────

    public ListStudent()
    {
        InitializeComponent();
        StudentListView.ItemsSource = Students;

        ChkSelectAll.IsCheckedChanged += OnSelectAllChanged;
        StudentListView.SelectionChanged += OnSelectionChanged;

        StudentListView.AddHandler(InputElement.PointerPressedEvent,  OnListPointerPressed,  RoutingStrategies.Tunnel);
        StudentListView.AddHandler<PointerEventArgs>(InputElement.PointerMovedEvent, OnListPointerMoved, RoutingStrategies.Tunnel);
        StudentListView.AddHandler(InputElement.PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Tunnel);

        ApplyViewMode();
    }

    // ── View Mode ────────────────────────────────────────

    private void ApplyViewMode()
    {
        switch (_viewMode)
        {
            case View.Full:
                ShowGradeColumn   = true;
                ShowClassColumn   = true;
                GradeColumnWidth  = new GridLength(50);
                ClassColumnWidth  = new GridLength(50);
                NumberColumnWidth = new GridLength(50);
                break;
            case View.ClassNumName:
                ShowGradeColumn   = false;
                ShowClassColumn   = true;
                GradeColumnWidth  = new GridLength(0);
                ClassColumnWidth  = new GridLength(50);
                NumberColumnWidth = new GridLength(50);
                break;
            case View.NumName:
                ShowGradeColumn   = false;
                ShowClassColumn   = false;
                GradeColumnWidth  = new GridLength(0);
                ClassColumnWidth  = new GridLength(0);
                NumberColumnWidth = new GridLength(50);
                break;
            case View.NameOnly:
                ShowGradeColumn   = false;
                ShowClassColumn   = false;
                GradeColumnWidth  = new GridLength(0);
                ClassColumnWidth  = new GridLength(0);
                NumberColumnWidth = new GridLength(0);
                break;
        }

        SyncHeaderColumns();
    }

    private void SyncHeaderColumns()
    {
        if (HeaderGrid.ColumnDefinitions.Count < 5) return;
        HeaderGrid.ColumnDefinitions[0].Width = CheckColumnWidth;
        HeaderGrid.ColumnDefinitions[1].Width = GradeColumnWidth;
        HeaderGrid.ColumnDefinitions[2].Width = ClassColumnWidth;
        HeaderGrid.ColumnDefinitions[3].Width = NumberColumnWidth;
    }

    // ── SelectionMode 결정 ───────────────────────────────

    /// <summary>ShowCheckBox / AllowMultiSelect 조합으로 SelectionMode 결정.</summary>
    private void ApplySelectionMode()
    {
        bool multi = GetValue(ShowCheckBoxProperty) || GetValue(AllowMultiSelectProperty);
        StudentListView.SelectionMode = multi ? SelectionMode.Multiple : SelectionMode.Single;
    }

    // ── CheckBox 모드 ────────────────────────────────────

    private void ApplyCheckBoxVisibility(bool show)
    {
        if (show)
        {
            CheckColumnWidth           = new GridLength(36);
            ChkSelectAll.IsVisible     = true;
            PnlSelectionInfo.IsVisible = true;
            UpdateSelectionInfo();
        }
        else
        {
            CheckColumnWidth           = new GridLength(0);
            ChkSelectAll.IsVisible     = false;
            PnlSelectionInfo.IsVisible = false;
            StudentListView.SelectedItem = null;
        }

        if (HeaderGrid.ColumnDefinitions.Count >= 5)
            HeaderGrid.ColumnDefinitions[0].Width = CheckColumnWidth;

        ApplySelectionMode();
    }

    private void OnSelectAllChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressSelectAllEvent) return;

        if (ChkSelectAll.IsChecked == true)
            StudentListView.SelectAll();
        else
            UnselectAll();
    }

    private void UnselectAll()
    {
        _suppressSelectAllEvent = true;
        try { StudentListView.SelectedItems?.Clear(); }
        finally { _suppressSelectAllEvent = false; }

        SetSelectAllCheckBox(false);
        UpdateSelectionInfo();
        SelectionChangedNotify?.Invoke(this, 0);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 단일 선택 모드(ShowCheckBox=false, AllowMultiSelect=false)일 때만 StudentSelected 발화
        if (!GetValue(ShowCheckBoxProperty) && !GetValue(AllowMultiSelectProperty)
            && StudentListView.SelectedItem is Enrollment selected)
        {
            StudentSelected?.Invoke(this, selected);
        }

        if (GetValue(ShowCheckBoxProperty))
        {
            UpdateSelectAllCheckBox();
            UpdateSelectionInfo();
            SelectionChangedNotify?.Invoke(this, StudentListView.SelectedItems?.Count ?? 0);
        }
    }

    private void UpdateSelectAllCheckBox()
    {
        int count = StudentListView.SelectedItems?.Count ?? 0;
        int total = Students.Count;

        bool? next = (total == 0 || count == 0) ? false
                   : count == total             ? true
                   :                              (bool?)null;

        SetSelectAllCheckBox(next);
    }

    private void SetSelectAllCheckBox(bool? value)
    {
        _suppressSelectAllEvent = true;
        try { ChkSelectAll.IsChecked = value; }
        finally { _suppressSelectAllEvent = false; }
    }

    private void UpdateSelectionInfo()
    {
        int selected = StudentListView.SelectedItems?.Count ?? 0;
        int total    = Students.Count;

        TxtSelectionInfo.Text = selected > 0
            ? $"✔ {selected} / {total}명 선택됨"
            : $"전체 {total}명";
    }

    // ── Pointer / Context Menu / Drag ────────────────────

    private Point?                  _dragStartPoint;
    private Enrollment?              _dragCandidate;
    private PointerPressedEventArgs? _dragPressArgs;
    private const double DragThreshold = 4.0;

    private void OnListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual v) return;

        var item  = FindEnrollment(v);
        var props = e.GetCurrentPoint(StudentListView).Properties;

        if (props.IsRightButtonPressed)
        {
            if (item != null)
            {
                StudentListView.SelectedItem = item;
                if (ItemContextFlyout != null)
                {
                    ItemContextFlyout.PlacementTarget = StudentListView;
                    ItemContextFlyout.Open(StudentListView);
                }
            }
            e.Handled = true;
            return;
        }

        if (!props.IsLeftButtonPressed || item is null) return;

        // 체크박스 모드: 행 영역 클릭 → IsSelected 토글
        if (GetValue(ShowCheckBoxProperty))
        {
            bool isCheckBoxSelf = v is CheckBox
                || (v.GetVisualParent() is Visual p1 && p1 is CheckBox)
                || (v.GetVisualParent()?.GetVisualParent() is Visual p2 && p2 is CheckBox);

            if (!isCheckBoxSelf)
            {
                e.Handled = true;
                var container = StudentListView.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                    container.IsSelected = !container.IsSelected;
            }
            return;
        }

        _dragStartPoint = e.GetCurrentPoint(StudentListView).Position;
        _dragCandidate  = item;
        _dragPressArgs  = e;
    }

    private async void OnListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStartPoint is null || _dragCandidate is null || _dragPressArgs is null) return;
        if (!e.GetCurrentPoint(StudentListView).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = null; _dragCandidate = null; _dragPressArgs = null;
            return;
        }

        var current = e.GetCurrentPoint(StudentListView).Position;
        var delta   = current - _dragStartPoint.Value;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

        var candidate = _dragCandidate;
        var pressArgs = _dragPressArgs;
        _dragStartPoint = null; _dragCandidate = null; _dragPressArgs = null;

        try
        {
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(EnrollmentDragFormat, candidate));
            await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Copy);
        }
        catch { /* 드래그 취소 무시 */ }
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPoint = null; _dragCandidate = null; _dragPressArgs = null;
    }

    private static Enrollment? FindEnrollment(Visual visual)
    {
        Visual? v = visual;
        while (v != null)
        {
            if (v is StyledElement se && se.DataContext is Enrollment en) return en;
            v = v.GetVisualParent();
        }
        return null;
    }

    // ── Public API ───────────────────────────────────────

    public void LoadStudents(IEnumerable<Enrollment> students)
    {
        StudentListView.ItemsSource = null;
        Students.Clear();
        foreach (var s in students) Students.Add(s);
        StudentListView.ItemsSource = Students;
        if (GetValue(ShowCheckBoxProperty)) UpdateSelectionInfo();
    }

    public void ClearStudents()
    {
        Students.Clear();
        if (GetValue(ShowCheckBoxProperty)) UpdateSelectionInfo();
    }

    public void ClearSelection()
    {
        SetSelectAllCheckBox(false);
        StudentListView.SelectedItems?.Clear();
        StudentListView.SelectedItem = null;
    }

    public int SelectedCount => StudentListView.SelectedItems?.Count ?? 0;

    public void SelectAll() => StudentListView.SelectAll();

    public void DeselectAll() => UnselectAll();

    public Enrollment? SelectedStudent
    {
        get => StudentListView.SelectedItem as Enrollment;
        set => StudentListView.SelectedItem = value;
    }

    public IEnumerable<Enrollment> GetSelectedStudents()
        => StudentListView.SelectedItems?.Cast<Enrollment>() ?? Array.Empty<Enrollment>();

    /// <summary>
    /// 지정한 studentId 목록을 선택 상태로 설정. 나머지는 해제.
    /// AllowMultiSelect 또는 ShowCheckBox 가 켜져 있을 때 유효.
    /// </summary>
    public void SetSelectedStudents(IEnumerable<string> studentIds)
    {
        var idSet = new HashSet<string>(studentIds);

        _suppressSelectAllEvent = true;
        try
        {
            StudentListView.SelectedItems?.Clear();
            foreach (var s in Students)
            {
                if (!idSet.Contains(s.StudentID)) continue;
                var container = StudentListView.ContainerFromItem(s) as ListBoxItem;
                if (container != null)
                    container.IsSelected = true;
            }
        }
        finally { _suppressSelectAllEvent = false; }

        if (GetValue(ShowCheckBoxProperty))
        {
            UpdateSelectAllCheckBox();
            UpdateSelectionInfo();
        }
    }

    public void SelectStudent(string studentId)
    {
        var s = Students.FirstOrDefault(x => x.StudentID == studentId);
        if (s != null)
        {
            StudentListView.SelectedItem = s;
            StudentListView.ScrollIntoView(s);
        }
    }
}
