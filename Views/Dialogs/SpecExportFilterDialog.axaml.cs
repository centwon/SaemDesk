using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생부 특기사항 일괄 출력 필터 다이얼로그.
/// 원본: NewSchool.Dialogs.SpecExportFilterDialog (WinUI3 ContentDialog).
/// </summary>
public partial class SpecExportFilterDialog : Window
{
    public bool IsSuccess { get; private set; }

    /// <summary>선택한 영역 (빈 문자열 = 전체)</summary>
    public string SelectedType
    {
        get
        {
            if (CBoxType.SelectedItem is ComboBoxItem ci && ci.Tag?.ToString() is string tag)
                return tag == "전체" ? string.Empty : tag;
            return string.Empty;
        }
    }

    /// <summary>상태 필터: "all" | "draft" | "finalized"</summary>
    public string StatusFilter
    {
        get
        {
            if (CBoxStatus.SelectedItem is ComboBoxItem ci && ci.Tag?.ToString() is string tag)
                return tag;
            return "all";
        }
    }

    public bool ExcludeEmpty => ChkExcludeEmpty.IsChecked == true;
    public bool IsPdf        => RbPdf.IsChecked == true;

    public SpecExportFilterDialog()
    {
        InitializeComponent();
    }

    private void OnExport(object? sender, RoutedEventArgs e)
    {
        IsSuccess = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
