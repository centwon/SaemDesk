using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 통합 내보내기 페이지 ViewModel.
/// PreviewCommand: UnifiedExportService.PreviewClassAsync → HTML → RichEditor(ReadOnly) 표시.
/// ExportCommand:  파일 저장 후 자동 열기.
/// ClipboardCommand: TSV 클립보드 복사 (누가기록/학생부만).
/// </summary>
public partial class UnifiedExportPageVM : ViewModelBase
{
    private readonly UnifiedExportService _service = new();

    // ────────────────────────────────────────────────
    // 선택 목록
    // ────────────────────────────────────────────────

    public string[] DataTypes { get; } =
        ["누가기록", "학생부 특기사항", "좌석배정", "학생카드"];

    public string[] Formats { get; } =
        ["Excel", "PDF", "HTML", "CSV"];

    // ────────────────────────────────────────────────
    // 필터 입력값 (ClassFilterBar → View 코드비하인드에서 설정)
    // ────────────────────────────────────────────────

    [ObservableProperty]
    private int _year = Settings.WorkYear;

    [ObservableProperty]
    private int _grade = 1;

    [ObservableProperty]
    private int _classNo = 1;

    // ────────────────────────────────────────────────
    // 선택 인덱스
    // ────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExcelEnabled))]
    [NotifyPropertyChangedFor(nameof(IsCsvEnabled))]
    private int _selectedDataIndex;

    [ObservableProperty]
    private int _selectedFormatIndex;

    // ────────────────────────────────────────────────
    // 상태
    // ────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusBar))]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusBar))]
    private string _errorText = string.Empty;

    /// <summary>내보내기 완료 후 파일 경로. 결과 패널 표시 여부에도 사용.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusBar))]
    private string _lastFilePath = string.Empty;

    // ────────────────────────────────────────────────
    // 미리보기
    // ────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private string _previewHtml = string.Empty;

    /// <summary>미리보기 패널 표시 여부.</summary>
    public bool HasPreview => !string.IsNullOrEmpty(PreviewHtml);

    /// <summary>상태 바 표시 여부 — 상태 텍스트·에러·파일 경로 중 하나라도 있으면 표시.</summary>
    public bool HasStatusBar =>
        !string.IsNullOrEmpty(StatusText) ||
        !string.IsNullOrEmpty(ErrorText) ||
        !string.IsNullOrEmpty(LastFilePath);

    // ────────────────────────────────────────────────
    // 형식 제한
    // ────────────────────────────────────────────────

    /// <summary>좌석·학생카드는 Excel 비활성.</summary>
    public bool IsExcelEnabled => SelectedDataIndex is 0 or 1;

    /// <summary>CSV 는 누가기록·학생부만.</summary>
    public bool IsCsvEnabled => SelectedDataIndex is 0 or 1;

    // ────────────────────────────────────────────────
    // 미리보기 Command
    // ────────────────────────────────────────────────

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (IsBusy) return;
        if (!ValidateFilter()) return;

        ErrorText  = string.Empty;
        StatusText = "미리보기 생성 중...";
        IsBusy     = true;

        try
        {
            var dataType = (UnifiedExportService.DataType)SelectedDataIndex;
            var html     = await _service.PreviewClassAsync(dataType, Year, Grade, ClassNo);

            if (string.IsNullOrEmpty(html))
            {
                PreviewHtml = string.Empty;
                StatusText  = "해당 조건에 맞는 데이터가 없습니다.";
                return;
            }

            PreviewHtml = html;
            StatusText  = "미리보기 준비 완료";
        }
        catch (Exception ex)
        {
            ErrorText  = ex.Message;
            StatusText = string.Empty;
            Debug.WriteLine($"[UnifiedExportPageVM] Preview: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ────────────────────────────────────────────────
    // 내보내기 Command
    // ────────────────────────────────────────────────

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy) return;
        if (!ValidateFilter()) return;

        ErrorText    = string.Empty;
        StatusText   = "내보내는 중...";
        LastFilePath = string.Empty;
        IsBusy       = true;

        try
        {
            var dataType = (UnifiedExportService.DataType)SelectedDataIndex;
            var format   = (UnifiedExportService.ExportFormat)SelectedFormatIndex;
            var path     = await _service.ExportClassAsync(dataType, format, Year, Grade, ClassNo);

            if (string.IsNullOrEmpty(path))
            {
                StatusText = "데이터가 없거나 취소되었습니다.";
                return;
            }

            LastFilePath = path;
            StatusText   = $"{Path.GetFileName(path)} 생성 완료";
            TryOpen(path);
        }
        catch (Exception ex)
        {
            ErrorText  = ex.Message;
            StatusText = string.Empty;
            Debug.WriteLine($"[UnifiedExportPageVM] Export: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ────────────────────────────────────────────────
    // 클립보드 Command (View 에서 호출, text 주입)
    // ────────────────────────────────────────────────

    [RelayCommand]
    private async Task ClipboardAsync()
    {
        if (IsBusy) return;
        if (!ValidateFilter()) return;
        if (!IsCsvEnabled)
        {
            StatusText = "좌석배정·학생카드는 클립보드 복사를 지원하지 않습니다.";
            return;
        }

        ErrorText  = string.Empty;
        StatusText = "데이터를 불러오는 중...";
        IsBusy     = true;

        try
        {
            var dataType = (UnifiedExportService.DataType)SelectedDataIndex;
            string? csv  = dataType == UnifiedExportService.DataType.StudentSpec
                ? await _service.BuildClassSpecsCsvAsync(Year, Grade, ClassNo)
                : await _service.BuildClassLogsCsvAsync(Year, Grade, ClassNo);

            if (string.IsNullOrEmpty(csv))
            {
                StatusText = "해당 조건에 맞는 데이터가 없습니다.";
                return;
            }

            // 클립보드 쓰기는 View 콜백으로 위임
            ClipboardTextReady?.Invoke(this, csv);

            int rowCount = 0;
            foreach (var ch in csv) if (ch == '\n') rowCount++;
            StatusText = $"클립보드에 복사됨 ({rowCount}행)";
        }
        catch (Exception ex)
        {
            ErrorText  = ex.Message;
            StatusText = string.Empty;
            Debug.WriteLine($"[UnifiedExportPageVM] Clipboard: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>CSV 준비 완료 시 View 에 클립보드 쓰기를 위임하는 이벤트.</summary>
    public event EventHandler<string>? ClipboardTextReady;

    // ────────────────────────────────────────────────
    // 파일/폴더 열기 Command
    // ────────────────────────────────────────────────

    [RelayCommand]
    private void OpenFile()  => TryOpen(LastFilePath);

    [RelayCommand]
    private void OpenFolder()
    {
        var dir = Path.GetDirectoryName(LastFilePath);
        if (!string.IsNullOrEmpty(dir)) TryOpen(dir);
    }

    // ────────────────────────────────────────────────
    // 내부 헬퍼
    // ────────────────────────────────────────────────

    private bool ValidateFilter()
    {
        if (Year == 0 || Grade == 0)
        {
            ErrorText = "학년도, 학년을 선택해주세요.";
            return false;
        }
        return true;
    }

    private static void TryOpen(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { /* 경로는 UI에 표시됨 */ }
    }
}
