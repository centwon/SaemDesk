using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 기록 일괄 출력 필터 다이얼로그.
/// 원본: NewSchool.Dialogs.BatchExportFilterDialog (WinUI3 ContentDialog).
/// </summary>
public partial class BatchExportFilterDialog : Window
{
    public bool IsSuccess { get; private set; }

    public LogCategory SelectedCategory =>
        CBoxCategory.SelectedItem is LogCategory cat ? cat : LogCategory.전체;

    public int SelectedSemester
    {
        get
        {
            if (CBoxSemester.SelectedItem is ComboBoxItem ci && ci.Tag?.ToString() is string s
                && int.TryParse(s, out int v)) return v;
            return 0;
        }
    }

    public string Keyword => TbKeyword.Text?.Trim() ?? string.Empty;
    public bool   IsPdf   => RbPdf.IsChecked == true;

    public BatchExportFilterDialog()
    {
        InitializeComponent();

        CBoxCategory.ItemsSource  = Enum.GetValues<LogCategory>().Cast<LogCategory>().ToList();
        CBoxCategory.SelectedIndex = 0;
        CBoxSemester.SelectedIndex = 0;
    }

    private void OnExport(object? sender, RoutedEventArgs e)
    {
        IsSuccess = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
