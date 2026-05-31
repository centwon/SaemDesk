using MiniExcelLibs;
using SaemDesk.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SaemDesk.Services;

/// <summary>
/// 수업기록(LessonLog) 내보내기 서비스.
/// 두 가지 묶음 단위를 지원한다.
///   - 진도표: 한 학급의 한 과목 기록을 차시 순으로 (날짜·교시·단원·주제·내용·메모)
///   - 기간일지: 선택 기간의 모든 기록을 날짜순으로 (학급·과목 혼합)
/// 형식별 서비스 중 Excel(MiniExcel) 담당. 파일은 UserDataPath\Exports 에 저장 후 경로 반환.
/// </summary>
public class LessonLogExportService
{
    private static string GetOutputDir()
    {
        var dir = Path.Combine(Settings.UserDataPath, "Exports");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    // ────────────────────────────────────────────────────
    //  데이터 로딩 (PDF·HTML 빌더와 공유)
    // ────────────────────────────────────────────────────

    /// <summary>진도표용 — 해당 학기 교사 기록에서 과목·강의실로 필터, 차시 순 정렬. room=null이면 전체 강의실.</summary>
    public static async Task<List<LessonLog>> LoadProgressAsync(int semester, string subject, string? room)
    {
        using var svc = new LessonLogService();
        var all = await svc.GetMyLessonsAsync(semester);
        return all
            .Where(l => l.Subject == subject && (room is null || l.Room == room))
            .OrderBy(l => l.Date).ThenBy(l => l.Period)
            .ToList();
    }

    /// <summary>기간일지용 — 해당 학기 교사 기록에서 날짜 범위로 필터, 날짜·교시 순 정렬.</summary>
    public static async Task<List<LessonLog>> LoadJournalAsync(int semester, DateTime from, DateTime to)
    {
        using var svc = new LessonLogService();
        var all = await svc.GetMyLessonsAsync(semester);
        return all
            .Where(l => l.Date.Date >= from.Date && l.Date.Date <= to.Date)
            .OrderBy(l => l.Date).ThenBy(l => l.Period)
            .ToList();
    }

    // ────────────────────────────────────────────────────
    //  Excel
    // ────────────────────────────────────────────────────

    /// <summary>진도표 Excel. 데이터 없으면 null.</summary>
    public string? ExportProgressToExcel(string subject, string? room, List<LessonLog> logs)
    {
        if (logs.Count == 0) return null;

        var rows = logs.Select((l, i) => new LessonProgressRow
        {
            차시 = i + 1,
            날짜 = l.Date.ToString("yyyy-MM-dd"),
            교시 = l.Period > 0 ? $"{l.Period}교시" : string.Empty,
            단원 = l.SectionName,
            주제 = l.Topic,
            내용 = l.Content,
            메모 = l.Note,
        }).ToList();

        string roomLabel = string.IsNullOrEmpty(room) ? "전체" : room;
        var fileName = $"수업진도_{subject}_{roomLabel}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var path = Path.Combine(GetOutputDir(), fileName);
        MiniExcel.SaveAs(path, new Dictionary<string, object> { [$"{subject} {roomLabel}"] = rows });
        return path;
    }

    /// <summary>기간일지 Excel. 데이터 없으면 null.</summary>
    public string? ExportJournalToExcel(DateTime from, DateTime to, List<LessonLog> logs)
    {
        if (logs.Count == 0) return null;

        var rows = logs.Select(l => new LessonJournalRow
        {
            날짜 = l.Date.ToString("yyyy-MM-dd"),
            교시 = l.Period > 0 ? $"{l.Period}교시" : string.Empty,
            과목 = l.Subject,
            학급 = l.Grade > 0 && l.Class > 0 ? $"{l.Grade}-{l.Class}" : string.Empty,
            단원 = l.SectionName,
            주제 = l.Topic,
            내용 = l.Content,
        }).ToList();

        var fileName = $"수업일지_{from:yyyyMMdd}-{to:yyyyMMdd}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var path = Path.Combine(GetOutputDir(), fileName);
        MiniExcel.SaveAs(path, new Dictionary<string, object> { ["수업일지"] = rows });
        return path;
    }
}

// ════════════════════════════════════════════════════════
//  Excel 행 DTO (MiniExcel — 프로퍼티명이 헤더가 됨, AOT 호환)
// ════════════════════════════════════════════════════════

public sealed class LessonProgressRow
{
    public int    차시 { get; set; }
    public string 날짜 { get; set; } = string.Empty;
    public string 교시 { get; set; } = string.Empty;
    public string 단원 { get; set; } = string.Empty;
    public string 주제 { get; set; } = string.Empty;
    public string 내용 { get; set; } = string.Empty;
    public string 메모 { get; set; } = string.Empty;
}

public sealed class LessonJournalRow
{
    public string 날짜 { get; set; } = string.Empty;
    public string 교시 { get; set; } = string.Empty;
    public string 과목 { get; set; } = string.Empty;
    public string 학급 { get; set; } = string.Empty;
    public string 단원 { get; set; } = string.Empty;
    public string 주제 { get; set; } = string.Empty;
    public string 내용 { get; set; } = string.Empty;
}
