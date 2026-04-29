using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 통합 내보내기 페이지 ViewModel — UnifiedExportService 위임.
/// 학급 단위 (학년도/학년/반) 데이터 타입 × 형식 조합 일괄 내보내기.
/// </summary>
public partial class UnifiedExportPageVM : ViewModelBase
{
    private readonly UnifiedExportService _service = new();

    public IReadOnlyList<string> DataTypes { get; } = new[]
    {
        "누가기록", "학생부 특기사항", "좌석배정", "학생카드"
    };

    public IReadOnlyList<string> Formats { get; } = new[]
    {
        "Excel", "PDF", "HTML", "CSV"
    };

    [ObservableProperty]
    private int _year = Settings.WorkYear;

    [ObservableProperty]
    private int _grade = 1;

    [ObservableProperty]
    private int _classNo = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExcelEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCsvEnabled))]
    private int _selectedDataIndex;

    [ObservableProperty]
    private int _selectedFormatIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _errorText = string.Empty;

    /// <summary>좌석/학생카드는 Excel 비활성화.</summary>
    public bool IsExcelEnabled => SelectedDataIndex is 0 or 1;

    /// <summary>CSV 는 표 형태(누가기록/학생부)만.</summary>
    public bool IsCsvEnabled => SelectedDataIndex is 0 or 1;

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy) return;
        ErrorText = string.Empty;
        StatusText = "내보내는 중...";
        IsBusy = true;
        try
        {
            var dataType = (UnifiedExportService.DataType)SelectedDataIndex;
            var format = (UnifiedExportService.ExportFormat)SelectedFormatIndex;
            var path = await _service.ExportClassAsync(dataType, format, Year, Grade, ClassNo);
            StatusText = string.IsNullOrEmpty(path)
                ? "데이터가 없거나 취소되었습니다."
                : $"저장됨: {path}";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
