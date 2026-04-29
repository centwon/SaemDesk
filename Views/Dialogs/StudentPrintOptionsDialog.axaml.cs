using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학생 카드 인쇄 옵션 다이얼로그.
/// 원본: NewSchool.Dialogs.StudentPrintOptionsDialog (WinUI3 ContentDialog).
/// </summary>
public partial class StudentPrintOptionsDialog : Window
{
    public bool IsSuccess { get; private set; }

    /// <summary>세부 정보 포함 여부</summary>
    public bool IncludeDetailInfo  => ChkDetailInfo.IsChecked  == true;

    /// <summary>학생 생활 기록 포함 여부</summary>
    public bool IncludeStudentLogs => ChkStudentLogs.IsChecked == true;

    /// <summary>전체 기록 출력 여부 (false면 선택한 기록만)</summary>
    public bool AllLogs => RbAllLogs.IsChecked == true;

    /// <summary>최대 출력 개수</summary>
    public int MaxLogCountValue => (int)(MaxLogCount.Value ?? 50);

    public StudentPrintOptionsDialog()
    {
        InitializeComponent();
    }

    private void OnLogsCheckChanged(object? sender, RoutedEventArgs e)
        => LogOptionsPanel.IsVisible = ChkStudentLogs.IsChecked == true;

    private void OnPrint(object? sender, RoutedEventArgs e)
    {
        IsSuccess = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
