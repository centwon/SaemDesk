using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SaemDesk.Services.Platform;

namespace SaemDesk.Helpers;

/// <summary>
/// Excel 파일 처리 헬퍼 — IFilePickerService 기반으로 플랫폼 독립적.
/// </summary>
public static class ExcelHelpers
{
    public static async Task<string?> PickExcelFileAsync(IFilePickerService picker)
        => await picker.OpenFileAsync(".xlsx", ".xls");

    public static async Task<string?> SaveExcelFileAsync(
        IFilePickerService picker,
        string defaultFileName = "데이터.xlsx")
        => await picker.SaveFileAsync(defaultFileName, ".xlsx");

    public static async Task<bool> SaveDataTableToExcelAsync(
        IFilePickerService picker,
        DataTable data,
        string? title = null,
        string? subtitle = null,
        bool openAfterSave = true)
    {
        try
        {
            string defaultFileName = string.IsNullOrWhiteSpace(title)
                ? $"데이터_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                : $"{title}_{DateTime.Now:yyyyMMdd}.xlsx";

            var savePath = await SaveExcelFileAsync(picker, defaultFileName);
            if (savePath is null) return false;

            string tempPath = await ExcelHelper.WriteDataAsync(data, title, subtitle);
            File.Copy(tempPath, savePath, overwrite: true);
            File.Delete(tempPath);

            if (openAfterSave) OpenFile(savePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> ExportStudentsToExcelAsync<T>(
        IFilePickerService picker,
        IEnumerable<T> students,
        string title = "학생 명단",
        bool openAfterSave = true) where T : class
    {
        try
        {
            string defaultFileName = $"{title}_{DateTime.Now:yyyyMMdd}.xlsx";
            var savePath = await SaveExcelFileAsync(picker, defaultFileName);
            if (savePath is null) return false;

            string tempPath = await ExcelHelper.WriteListAsync(students);
            File.Copy(tempPath, savePath, overwrite: true);
            File.Delete(tempPath);

            if (openAfterSave) OpenFile(savePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> DownloadStudentTemplateAsync(IFilePickerService picker)
    {
        try
        {
            var savePath = await SaveExcelFileAsync(picker, $"학생명단_템플릿_{DateTime.Now:yyyyMMdd}.xlsx");
            if (savePath is null) return false;

            string tempPath = ExcelHelper.CreateStudentTemplate();
            File.Copy(tempPath, savePath, overwrite: true);
            File.Delete(tempPath);

            OpenFile(savePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> SaveMultipleSheetsAsync(
        IFilePickerService picker,
        Dictionary<string, DataTable> sheets,
        string defaultFileName = "통합문서.xlsx",
        bool openAfterSave = true)
    {
        try
        {
            var savePath = await SaveExcelFileAsync(picker, defaultFileName);
            if (savePath is null) return false;

            string tempPath = ExcelHelper.WriteMultipleSheets(sheets);
            File.Copy(tempPath, savePath, overwrite: true);
            File.Delete(tempPath);

            if (openAfterSave) OpenFile(savePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> QuickExportAsync(
        IFilePickerService picker,
        DataTable data,
        string fileName = "내보내기")
        => await SaveDataTableToExcelAsync(
            picker, data,
            title: fileName,
            subtitle: $"생성일시: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            openAfterSave: true);

    private static void OpenFile(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { }
    }
}
