using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace SaemDesk.Services.Platform.Windows;

/// <summary>
/// Avalonia StorageProvider 기반 파일 선택 서비스 구현.
/// MainWindow의 TopLevel에서 StorageProvider를 지연(lazy) 획득합니다.
/// </summary>
public class AvaloniaFilePickerService : IFilePickerService
{
    private static IStorageProvider GetStorageProvider()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        var sp = window is not null ? TopLevel.GetTopLevel(window)?.StorageProvider : null;
        return sp ?? throw new InvalidOperationException("[AvaloniaFilePickerService] StorageProvider를 가져올 수 없습니다.");
    }

    // ────────────────────────────────────────────────────
    //  파일 열기
    // ────────────────────────────────────────────────────

    public async Task<string?> OpenFileAsync(params string[] extensions)
    {
        try
        {
            var sp = GetStorageProvider();
            var options = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = extensions.Length > 0 ? BuildFileTypes(extensions) : null
            };
            var result = await sp.OpenFilePickerAsync(options);
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FilePickerService] OpenFileAsync 오류: {ex.Message}");
            return null;
        }
    }

    // ────────────────────────────────────────────────────
    //  파일 저장
    // ────────────────────────────────────────────────────

    public async Task<string?> SaveFileAsync(string defaultName, params string[] extensions)
    {
        try
        {
            var sp = GetStorageProvider();
            var ext = (extensions.FirstOrDefault() ?? ".dat").TrimStart('.');
            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = defaultName,
                DefaultExtension  = ext,
                FileTypeChoices   = extensions.Length > 0 ? BuildFileTypes(extensions) : null
            };
            var result = await sp.SaveFilePickerAsync(options);
            return result?.Path.LocalPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FilePickerService] SaveFileAsync 오류: {ex.Message}");
            return null;
        }
    }

    // ────────────────────────────────────────────────────
    //  폴더 열기
    // ────────────────────────────────────────────────────

    public async Task<string?> OpenFolderAsync()
    {
        try
        {
            var sp = GetStorageProvider();
            var result = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });
            return result.Count > 0 ? result[0].Path.LocalPath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FilePickerService] OpenFolderAsync 오류: {ex.Message}");
            return null;
        }
    }

    // ────────────────────────────────────────────────────
    //  내부 유틸
    // ────────────────────────────────────────────────────

    private static List<FilePickerFileType> BuildFileTypes(string[] extensions)
    {
        // 확장자별로 묶기 (예: .pdf / .xlsx / .xls)
        var patterns = extensions
            .Select(e => e.StartsWith('*') ? e : $"*{e}")
            .ToList();

        return
        [
            new FilePickerFileType(string.Join("/", extensions))
            {
                Patterns = patterns
            }
        ];
    }
}
