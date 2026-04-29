using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 좌석배정표 인쇄 옵션 다이얼로그.
/// 원본: NewSchool.Dialogs.SeatPrintOptionsDialog (WinUI3 ContentDialog).
/// </summary>
public partial class SeatPrintOptionsDialog : Window
{
    public bool IsSuccess { get; private set; }

    public PrintOrientation Orientation
    {
        get
        {
            if (RbPortrait.IsChecked  == true) return PrintOrientation.Portrait;
            if (RbLandscape.IsChecked == true) return PrintOrientation.Landscape;
            return PrintOrientation.Auto;
        }
    }

    public bool IncludeRoster => ChkIncludeRoster.IsChecked == true;

    public SeatPrintOptionsDialog()
    {
        InitializeComponent();
    }

    private void OnPrint(object? sender, RoutedEventArgs e)
    {
        IsSuccess = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
