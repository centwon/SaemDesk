using System;

namespace SaemDesk.Board.Models;

/// <summary>PostFile 모델 — NewSchool.Board.PostFile 에서 WinUI3 의존성 제거.</summary>
public class PostFile
{
    public int      No        { get; set; } = -1;
    public int      Post      { get; set; }
    public DateTime DateTime  { get; set; } = DateTime.Now;
    public string   FileName  { get; set; } = string.Empty;
    public long     FileSize  { get; set; }

    public string FileSizeDisplay => FileSize switch
    {
        < 1024           => $"{FileSize} B",
        < 1024 * 1024    => $"{FileSize / 1024.0:F1} KB",
        _                => $"{FileSize / (1024.0 * 1024):F1} MB",
    };
}
