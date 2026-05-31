using System;

namespace SaemDesk.Models;

/// <summary>수업기록 첨부파일 메타. 실제 파일은 UserDataPath/LessonLogFiles/{LessonLog}/{FileName}.</summary>
public class LessonLogFile
{
    public int      No        { get; set; } = -1;
    public int      LessonLog { get; set; }
    public string   FileName  { get; set; } = string.Empty;
    public long     FileSize  { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string FileSizeDisplay => FileSize switch
    {
        < 1024        => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        _             => $"{FileSize / (1024.0 * 1024):F1} MB",
    };
}
