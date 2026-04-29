using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 학급 단위 통합 내보내기 다이얼로그.
/// 데이터 종류(누가기록/학생부 특기사항/좌석/학생카드) × 형식(Excel/PDF/HTML/CSV) 조합으로
/// UnifiedExportService에 위임해 파일을 생성한다. 결과 파일은 UserDataPath\Exports 에 저장된다.
/// </summary>
public partial class ExportDialog : Window
{
    private readonly int _year;
    private readonly int _grade;
    private readonly int _classNo;
    private string? _lastExportedPath;
    private bool _isBusy;

    public ExportDialog() : this(
        Settings.WorkYear.Value,
        Settings.HomeGrade.Value,
        Settings.HomeRoom.Value)
    { }

    public ExportDialog(int year, int grade, int classNo)
    {
        InitializeComponent();

        _year    = year;
        _grade   = grade;
        _classNo = classNo;

        SubTitleText.Text = $"{_year}학년도 · {_grade}학년 {_classNo}반";
        UpdateFormatHint();
    }

    private void OnDataTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FormatCombo is null) return;
        UpdateFormatHint();
    }

    private void UpdateFormatHint()
    {
        string dt = GetSelectedTag(DataTypeCombo) ?? "StudentLog";
        FormatHintText.Text = dt switch
        {
            "Seats"       => "좌석 배정표는 PDF / HTML 형식만 지원합니다.",
            "StudentCard" => "학생 카드는 PDF / HTML 형식만 지원합니다.",
            _             => "Excel · PDF · HTML · CSV 모두 지원합니다.",
        };
    }

    private static string? GetSelectedTag(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item)
            return item.Tag?.ToString();
        return null;
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        string dataTypeStr = GetSelectedTag(DataTypeCombo) ?? "StudentLog";
        string formatStr   = GetSelectedTag(FormatCombo)   ?? "Excel";

        if (!Enum.TryParse<UnifiedExportService.DataType>(dataTypeStr, out var dataType) ||
            !Enum.TryParse<UnifiedExportService.ExportFormat>(formatStr, out var format))
        {
            StatusText.Text = "선택한 항목을 해석할 수 없습니다.";
            return;
        }

        // 좌석/학생카드는 PDF/HTML 만 허용
        if ((dataType == UnifiedExportService.DataType.Seats ||
             dataType == UnifiedExportService.DataType.StudentCard) &&
            format != UnifiedExportService.ExportFormat.Pdf &&
            format != UnifiedExportService.ExportFormat.Html)
        {
            StatusText.Text = "선택한 데이터 종류는 PDF 또는 HTML 형식만 지원합니다.";
            return;
        }

        _isBusy = true;
        BusyBar.IsVisible       = true;
        ExportButton.IsEnabled  = false;
        OpenFolderButton.IsEnabled = false;
        ResultPathText.IsVisible = false;
        StatusText.Text         = "내보내는 중…";

        try
        {
            var service = new UnifiedExportService();
            var path = await service.ExportClassAsync(dataType, format, _year, _grade, _classNo);

            if (string.IsNullOrEmpty(path))
            {
                StatusText.Text = "내보낼 데이터가 없거나 해당 형식을 지원하지 않습니다.";
                _lastExportedPath = null;
            }
            else
            {
                _lastExportedPath = path;
                StatusText.Text  = "내보내기가 완료되었습니다.";
                ResultPathText.Text = path;
                ResultPathText.IsVisible = true;
                OpenFolderButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"실패: {ex.Message}";
            Debug.WriteLine($"[ExportDialog] {ex}");
        }
        finally
        {
            BusyBar.IsVisible = false;
            ExportButton.IsEnabled = true;
            _isBusy = false;
        }
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = !string.IsNullOrEmpty(_lastExportedPath)
                ? Path.GetDirectoryName(_lastExportedPath)
                : Path.Combine(Settings.UserDataPath, "Exports");
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName        = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"폴더를 열 수 없습니다: {ex.Message}";
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
