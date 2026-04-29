using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학생부 특이사항 목록 뷰어 — Avalonia 12 이식.
/// StudentInfoMode/Category 컬럼 토글, 체크 다중선택.
/// </summary>
public partial class SpecListViewer : UserControl
{
    // 기록 내용 TextBox 의 FontSize 제어
    public static readonly StyledProperty<double> SpecFontSizeProperty =
        AvaloniaProperty.Register<SpecListViewer, double>(nameof(SpecFontSize), defaultValue: 13d);

    public double SpecFontSize
    {
        get => GetValue(SpecFontSizeProperty);
        set => SetValue(SpecFontSizeProperty, value);
    }
    private StudentInfoMode _studentInfoMode = StudentInfoMode.HideAll;
    private LogCategory _category = LogCategory.전체;

    public StudentInfoMode StudentInfoMode
    {
        get => _studentInfoMode;
        set { if (_studentInfoMode != value) { _studentInfoMode = value; SetStudentInfoVisibility(); } }
    }

    public LogCategory Category
    {
        get => _category;
        set { if (_category != value) { _category = value; ChangeCategory(); } }
    }

    public ObservableCollection<StudentSpecialViewModel> Specs { get; } = new();

    public IEnumerable<StudentSpecialViewModel> SelectedSpecs =>
        Specs.Where(s => s.IsSelected || s.IsModified);

    public SpecListViewer()
    {
        InitializeComponent();
        SpecItems.ItemsSource = Specs;
        ChkSelectAll.IsCheckedChanged += OnSelectAllChanged;

        SetStudentInfoVisibility();
        ChangeCategory();
    }

    private void OnSelectAllChanged(object? sender, RoutedEventArgs e)
    {
        bool? state = ChkSelectAll.IsChecked;
        if (state == true)       foreach (var s in Specs) s.IsSelected = true;
        else if (state == false) foreach (var s in Specs) s.IsSelected = false;
    }

    // 데이터 행 콜럼 너비 동기화 상태
    private readonly GridLength[] _colWidths = new GridLength[10];

    private void SetCol(int col, double w)
    {
        var gw = w <= 0 ? new GridLength(0) : new GridLength(w);
        _colWidths[col] = gw;

        if (col >= 0 && col < HeaderGrid.ColumnDefinitions.Count)
            HeaderGrid.ColumnDefinitions[col].Width = gw;

        foreach (var spec in Specs)
            spec.SetColWidth(col, w);
    }

    private void SetStudentInfoVisibility()
    {
        switch (_studentInfoMode)
        {
            case StudentInfoMode.HideAll:
                SetCol(1, 0); SetCol(4, 0); SetCol(5, 0); SetCol(6, 0); SetCol(7, 0);
                TxtYearHeader.IsVisible = TxtGradeHeader.IsVisible = TxtClassHeader.IsVisible
                    = TxtNumberHeader.IsVisible = TxtNameHeader.IsVisible = false;
                break;
            case StudentInfoMode.ShowAll:
                SetCol(1, 60); SetCol(4, 40); SetCol(5, 40); SetCol(6, 40); SetCol(7, 60);
                TxtYearHeader.IsVisible = TxtGradeHeader.IsVisible = TxtClassHeader.IsVisible
                    = TxtNumberHeader.IsVisible = TxtNameHeader.IsVisible = true;
                break;
            case StudentInfoMode.GradeClassNumName:
                SetCol(1, 0); SetCol(4, 40); SetCol(5, 40); SetCol(6, 40); SetCol(7, 60);
                TxtYearHeader.IsVisible = false;
                TxtGradeHeader.IsVisible = TxtClassHeader.IsVisible
                    = TxtNumberHeader.IsVisible = TxtNameHeader.IsVisible = true;
                break;
            case StudentInfoMode.ClassNumName:
                SetCol(1, 0); SetCol(4, 0); SetCol(5, 40); SetCol(6, 40); SetCol(7, 60);
                TxtYearHeader.IsVisible = TxtGradeHeader.IsVisible = false;
                TxtClassHeader.IsVisible = TxtNumberHeader.IsVisible = TxtNameHeader.IsVisible = true;
                break;
            case StudentInfoMode.NumName:
                SetCol(1, 0); SetCol(4, 0); SetCol(5, 0); SetCol(6, 40); SetCol(7, 60);
                TxtYearHeader.IsVisible = TxtGradeHeader.IsVisible = TxtClassHeader.IsVisible = false;
                TxtNumberHeader.IsVisible = TxtNameHeader.IsVisible = true;
                break;
            case StudentInfoMode.NameOnly:
                SetCol(1, 0); SetCol(4, 0); SetCol(5, 0); SetCol(6, 0); SetCol(7, 60);
                TxtYearHeader.IsVisible = TxtGradeHeader.IsVisible = TxtClassHeader.IsVisible
                    = TxtNumberHeader.IsVisible = false;
                TxtNameHeader.IsVisible = true;
                break;
        }
    }

    private void ChangeCategory()
    {
        switch (_category)
        {
            case LogCategory.전체:
                SetCol(2, 100); SetCol(3, 100);
                TxtTypeHeader.IsVisible = TxtSubjectHeader.IsVisible = true;
                TxtSubjectHeader.Text = "세부영역";
                break;
            case LogCategory.교과활동:
                SetCol(2, 0); SetCol(3, 100);
                TxtTypeHeader.IsVisible = false;
                TxtSubjectHeader.IsVisible = true;
                TxtSubjectHeader.Text = "과목";
                break;
            case LogCategory.동아리활동:
                SetCol(2, 0); SetCol(3, 100);
                TxtTypeHeader.IsVisible = false;
                TxtSubjectHeader.IsVisible = true;
                TxtSubjectHeader.Text = "동아리";
                break;
            default:
                SetCol(2, 0); SetCol(3, 0);
                TxtTypeHeader.IsVisible = TxtSubjectHeader.IsVisible = false;
                break;
        }
    }

    public void LoadSpecs(IEnumerable<StudentSpecial> specials)
    {
        Specs.Clear();
        foreach (var s in specials)
        {
            var vm = new StudentSpecialViewModel(s);
            ApplyCurrentWidths(vm);
            Specs.Add(vm);
        }
        ChkSelectAll.IsChecked = false;
    }

    public void LoadSpecs(IEnumerable<StudentSpecial> specials,
                          Dictionary<string, (int Grade, int ClassNum, int Number, string Name)> studentInfoLookup)
    {
        Specs.Clear();
        foreach (var s in specials)
        {
            StudentSpecialViewModel vm;
            if (studentInfoLookup.TryGetValue(s.StudentID, out var info))
                vm = new StudentSpecialViewModel(s, info.Grade, info.ClassNum, info.Number, info.Name);
            else
                vm = new StudentSpecialViewModel(s);
            ApplyCurrentWidths(vm);
            Specs.Add(vm);
        }
        ChkSelectAll.IsChecked = false;
    }

    private void ApplyCurrentWidths(StudentSpecialViewModel vm)
    {
        for (int i = 0; i < _colWidths.Length; i++)
        {
            if (_colWidths[i] == default) continue;
            var gl = _colWidths[i];
            double d = gl.IsStar ? -1 : gl.Value;
            vm.SetColWidth(i, d);
        }
    }

    public void ClearSelection()
    {
        foreach (var s in Specs) s.IsSelected = false;
        ChkSelectAll.IsChecked = false;
    }
}
