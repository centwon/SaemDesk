using System;
using System.IO;
using System.Threading.Tasks;
using SaemDesk.Services.Platform;

namespace SaemDesk.Services;

/// <summary>
/// 학생 사진 관리 서비스 — 파일 경로 기반. Avalonia Bitmap 변환은 ViewModel/View 레이어에서 처리.
/// </summary>
public class PhotoService
{
    private readonly string _baseDirectory;
    private readonly IFilePickerService? _filePicker;

    public PhotoService()
    {
        _baseDirectory = Settings.UserDataPath;
    }

    public PhotoService(string baseDirectory, IFilePickerService? filePicker = null)
    {
        _baseDirectory = baseDirectory;
        _filePicker = filePicker;
    }

    #region 사진 선택 및 저장

    public async Task<string?> PickAndSavePhotoAsync(string studentId)
    {
        if (string.IsNullOrEmpty(studentId) || _filePicker is null)
            return null;

        try
        {
            var selectedPath = await _filePicker.OpenFileAsync(".jpg", ".jpeg", ".png", ".bmp", ".gif");
            if (selectedPath is null) return null;

            return await SavePhotoAsync(selectedPath, studentId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PhotoService] PickAndSavePhotoAsync 오류: {ex.Message}");
            return null;
        }
    }

    private Task<string> SavePhotoAsync(string sourcePath, string studentId)
    {
        string year = Settings.WorkYear.Value.ToString();
        string photoDir = Path.Combine(_baseDirectory, "Photos", year);
        Directory.CreateDirectory(photoDir);

        string extension = Path.GetExtension(sourcePath);
        string fileName = $"{studentId}{extension}";
        string destPath = Path.Combine(photoDir, fileName);

        File.Copy(sourcePath, destPath, overwrite: true);

        return Task.FromResult(Path.Combine("Photos", year, fileName));
    }

    #endregion

    #region 사진 경로 해석

    /// <summary>
    /// 상대 경로 → 절대 경로 반환. 파일이 없으면 null.
    /// </summary>
    public string? ResolvePhotoPath(string? photoPath)
    {
        if (string.IsNullOrEmpty(photoPath)) return null;

        string fullPath = Path.IsPathRooted(photoPath)
            ? photoPath
            : Path.Combine(_baseDirectory, photoPath);

        return File.Exists(fullPath) ? fullPath : null;
    }

    #endregion

    #region 사진 삭제

    public Task<bool> DeletePhotoAsync(string? photoPath)
    {
        if (string.IsNullOrEmpty(photoPath)) return Task.FromResult(true);

        try
        {
            string fullPath = Path.IsPathRooted(photoPath)
                ? photoPath
                : Path.Combine(_baseDirectory, photoPath);

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PhotoService] DeletePhotoAsync 오류: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    #endregion

    public void EnsurePhotoDirectory(int year)
        => Directory.CreateDirectory(Path.Combine(_baseDirectory, "Photos", year.ToString()));

    public string GetPhotoFullPath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        return Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(_baseDirectory, relativePath);
    }

    public bool PhotoExists(string? photoPath)
    {
        if (string.IsNullOrEmpty(photoPath)) return false;
        return File.Exists(GetPhotoFullPath(photoPath));
    }
}
