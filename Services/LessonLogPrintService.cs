using System;
using System.Collections.Generic;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SaemDesk.Models;

namespace SaemDesk.Services;

/// <summary>
/// 수업기록(LessonLog) PDF 생성 — 진도표(학급+과목) / 기간일지. QuestPDF.
/// 파일은 UserDataPath\Prints 에 저장 후 경로 반환. 데이터 없으면 null.
/// </summary>
public class LessonLogPrintService
{
    private const float FontSize = 9f;
    private const float HeaderFontSize = 9.5f;

    private static string GetOutputDir()
    {
        var dir = Path.Combine(Settings.UserDataPath, "Prints");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    // ────────────────────────────────────────────────────
    //  진도표 (학급 + 과목)
    // ────────────────────────────────────────────────────

    public string? GenerateProgressPdf(int year, string subject, string? room, List<LessonLog> logs)
    {
        if (logs.Count == 0) return null;
        QuestPDF.Settings.License = LicenseType.Community;

        string roomLabel = string.IsNullOrEmpty(room) ? "전체" : room;
        var fileName = $"수업진도_{subject}_{roomLabel}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var filePath = Path.Combine(GetOutputDir(), fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignLeft().Text($"{year}학년도 수업 진도표")
                            .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                        row.AutoItem().AlignRight().Text($"{subject} · {roomLabel}")
                            .FontSize(13).SemiBold().FontColor(Colors.Grey.Darken2);
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(Colors.Blue.Medium);
                    col.Item().PaddingBottom(6);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(34);  // 차시
                        c.ConstantColumn(64);  // 날짜
                        c.ConstantColumn(40);  // 교시
                        c.RelativeColumn(1.4f); // 단원
                        c.RelativeColumn(1.8f); // 주제
                        c.RelativeColumn(3f);   // 내용
                    });

                    ComposeHeader(table, "차시", "날짜", "교시", "단원", "주제", "내용");

                    uint r = 1;
                    foreach (var l in logs)
                    {
                        var bg = r % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;
                        Data(table, bg, r.ToString());
                        Data(table, bg, l.Date.ToString("yyyy-MM-dd"));
                        Data(table, bg, l.Period > 0 ? $"{l.Period}" : "");
                        Data(table, bg, l.SectionName);
                        Data(table, bg, l.Topic);
                        Data(table, bg, l.Content);
                        r++;
                    }
                });

                ComposeFooter(page);
            });
        }).GeneratePdf(filePath);

        return filePath;
    }

    // ────────────────────────────────────────────────────
    //  기간일지 (날짜 범위)
    // ────────────────────────────────────────────────────

    public string? GenerateJournalPdf(int year, DateTime from, DateTime to, List<LessonLog> logs)
    {
        if (logs.Count == 0) return null;
        QuestPDF.Settings.License = LicenseType.Community;

        var fileName = $"수업일지_{from:yyyyMMdd}-{to:yyyyMMdd}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        var filePath = Path.Combine(GetOutputDir(), fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.PageColor(Colors.White);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignLeft().Text($"{year}학년도 수업 일지")
                            .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                        row.AutoItem().AlignRight().Text($"{from:yyyy-MM-dd} ~ {to:yyyy-MM-dd}")
                            .FontSize(13).SemiBold().FontColor(Colors.Grey.Darken2);
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(Colors.Blue.Medium);
                    col.Item().PaddingBottom(6);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(64);  // 날짜
                        c.ConstantColumn(40);  // 교시
                        c.ConstantColumn(56);  // 과목
                        c.ConstantColumn(48);  // 학급
                        c.RelativeColumn(1.4f); // 단원
                        c.RelativeColumn(1.8f); // 주제
                        c.RelativeColumn(3f);   // 내용
                    });

                    ComposeHeader(table, "날짜", "교시", "과목", "학급", "단원", "주제", "내용");

                    uint r = 1;
                    foreach (var l in logs)
                    {
                        var bg = r % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;
                        Data(table, bg, l.Date.ToString("yyyy-MM-dd"));
                        Data(table, bg, l.Period > 0 ? $"{l.Period}" : "");
                        Data(table, bg, l.Subject);
                        Data(table, bg, l.Grade > 0 && l.Class > 0 ? $"{l.Grade}-{l.Class}" : "");
                        Data(table, bg, l.SectionName);
                        Data(table, bg, l.Topic);
                        Data(table, bg, l.Content);
                        r++;
                    }
                });

                ComposeFooter(page);
            });
        }).GeneratePdf(filePath);

        return filePath;
    }

    // ────────────────────────────────────────────────────
    //  공통 셀 구성
    // ────────────────────────────────────────────────────

    private static void ComposeHeader(TableDescriptor table, params string[] titles)
    {
        table.Header(header =>
        {
            var style = TextStyle.Default.FontSize(HeaderFontSize).Bold().FontColor(Colors.White);
            foreach (var t in titles)
            {
                header.Cell()
                    .Background(Colors.Blue.Darken2)
                    .BorderBottom(1).BorderColor(Colors.White)
                    .Padding(4).AlignCenter().AlignMiddle()
                    .Text(t).Style(style);
            }
        });
    }

    private static void Data(TableDescriptor table, string bg, string? text)
    {
        table.Cell()
            .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Background(bg)
            .Padding(3)
            .Text(text ?? string.Empty).FontSize(FontSize);
    }

    private static void ComposeFooter(PageDescriptor page)
    {
        page.Footer().Row(row =>
        {
            row.RelativeItem().AlignLeft()
                .Text($"출력일시: {DateTime.Now:yyyy년 MM월 dd일 HH:mm}")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
            row.AutoItem().AlignRight().Text(t =>
            {
                t.CurrentPageNumber().FontSize(8);
                t.Span(" / ").FontSize(8);
                t.TotalPages().FontSize(8);
            });
        });
    }
}
