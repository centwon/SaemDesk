using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SaemDesk.Models;
using SaemDesk.Services.Platform;
using SaemDesk.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SaemDesk.Services;

/// <summary>
/// 학생카드 PDF 생성 서비스 — IFilePickerService 기반으로 플랫폼 독립적.
/// </summary>
public class StudentCardPrintService
{
    private const float LabelFontSize = 10f;
    private const float ValueFontSize = 10f;
    private const float SectionTitleFontSize = 12f;
    private const float LabelWidth = 65f;
    private static readonly string LabelColor = Colors.Grey.Darken2;
    private static readonly string BorderColor = Colors.Grey.Lighten1;

    public async Task<string?> GenerateStudentCardPdfAsync(
        StudentCardViewModel viewModel,
        IFilePickerService filePicker,
        bool includeDetailInfo = true,
        List<StudentLogViewModel>? studentLogs = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        ConfigureKoreanFont();

        var grade = viewModel.Enrollment?.Grade ?? 0;
        var classNo = viewModel.Enrollment?.Class ?? 0;
        var number = viewModel.Enrollment?.Number ?? 0;
        var year = viewModel.Enrollment?.Year ?? Settings.WorkYear.Value;

        var suggestedFileName = $"학생정보_{grade}학년{classNo}반_{number}번_{viewModel.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        var filePath = await filePicker.SaveFileAsync(suggestedFileName, ".pdf");
        if (filePath is null) return null;

        await Task.Run(() =>
            GeneratePdfDocument(filePath, viewModel, year, grade, classNo, number, includeDetailInfo, studentLogs));

        return filePath;
    }

    private void GeneratePdfDocument(string filePath, StudentCardViewModel vm, int year, int grade, int classNo, int number, bool includeDetailInfo, List<StudentLogViewModel>? studentLogs)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.Header().Element(c => ComposeHeader(c, year, grade, classNo, number));
                page.Content().Element(content => ComposeContent(content, vm, includeDetailInfo, studentLogs));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf(filePath);
    }

    private void ComposeHeader(IContainer container, int year, int grade, int classNo, int number)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("학생 정보 카드")
                .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().PaddingTop(4).AlignCenter()
                .Text($"{year}학년도 {grade}학년 {classNo}반 {number}번")
                .FontSize(12).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Colors.Blue.Medium);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4).AlignCenter()
                .Text($"출력일시: {DateTime.Now:yyyy년 MM월 dd일 HH:mm}")
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    private void ComposeContent(IContainer container, StudentCardViewModel vm, bool includeDetailInfo, List<StudentLogViewModel>? studentLogs)
    {
        container.Column(column =>
        {
            column.Item().PaddingTop(15).Element(c => ComposeBasicInfo(c, vm));

            if (includeDetailInfo)
            {
                column.Item().PaddingTop(12).Element(c => ComposeGuardianInfo(c, vm));
                column.Item().PaddingTop(12).Element(c => ComposeFamilyInfo(c, vm));
                column.Item().PaddingTop(12).Element(c => ComposeCareerInfo(c, vm));
                column.Item().PaddingTop(12).Element(c => ComposeHealthInfo(c, vm));
                column.Item().PaddingTop(12).Element(c => ComposeDetailMemo(c, vm));
            }

            if (studentLogs != null && studentLogs.Count > 0)
                column.Item().PaddingTop(12).Element(c => ComposeStudentLogs(c, studentLogs));
        });
    }

    private void ComposeBasicInfo(IContainer container, StudentCardViewModel vm)
    {
        container.Column(col =>
        {
            SectionTitle(col, "기본 정보", Colors.Blue.Darken2);

            col.Item().Border(0.5f).BorderColor(BorderColor).Row(row =>
            {
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(LabelWidth);
                        c.RelativeColumn(2);
                        c.ConstantColumn(50);
                        c.RelativeColumn(1);
                        c.ConstantColumn(40);
                        c.RelativeColumn(0.5f);
                    });

                    LabelCell(table, "이름");     ValueCell(table, vm.Student?.Name);
                    LabelCell(table, "성별");     ValueCell(table, vm.Student?.Sex);
                    LabelCell(table, "나이");     ValueCell(table, vm.Age > 0 ? $"{vm.Age}세" : "");
                    LabelCell(table, "생년월일"); ValueCellSpan(table, vm.Student?.BirthDate?.ToString("yyyy년 M월 d일") ?? "", 5);
                    LabelCell(table, "전화번호"); ValueCellSpan(table, vm.Student?.Phone, 2);
                    LabelCell(table, "이메일");   ValueCellSpan(table, vm.Student?.Email, 2);
                    LabelCell(table, "주소");     ValueCellSpan(table, vm.Student?.Address, 5);
                    LabelCell(table, "메모");     ValueCellSpan(table, vm.Student?.Memo, 5);
                });

                row.ConstantItem(100).Border(0.5f).BorderColor(BorderColor)
                    .Padding(4).AlignCenter().AlignMiddle()
                    .Element(c => ComposePhoto(c, vm));
            });
        });
    }

    private void ComposePhoto(IContainer container, StudentCardViewModel vm)
    {
        string? photoPath = vm.Student?.Photo;
        if (string.IsNullOrEmpty(photoPath)) { NoPhoto(container); return; }

        string fullPath = Path.IsPathRooted(photoPath)
            ? photoPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, photoPath);

        if (!File.Exists(fullPath)) { NoPhoto(container); return; }

        try { container.Image(fullPath).FitArea(); }
        catch { container.AlignCenter().AlignMiddle().Text("사진 오류").FontSize(9).FontColor(Colors.Red.Medium); }
    }

    private static void NoPhoto(IContainer container)
        => container.AlignCenter().AlignMiddle().Text("사진 없음").FontSize(9).FontColor(Colors.Grey.Medium);

    private void ComposeGuardianInfo(IContainer container, StudentCardViewModel vm)
    {
        container.Column(col =>
        {
            SectionTitle(col, "보호자 정보", Colors.Green.Darken2);
            col.Item().Border(0.5f).BorderColor(BorderColor).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(LabelWidth); c.RelativeColumn(1);
                    c.ConstantColumn(50);         c.RelativeColumn(1);
                    c.ConstantColumn(50);         c.RelativeColumn(1);
                });
                LabelCell(table, "보호자"); ValueCell(table, vm.Detail?.GuardianName);
                LabelCell(table, "관계");   ValueCell(table, vm.Detail?.GuardianRelation);
                LabelCell(table, "연락처"); ValueCell(table, vm.Detail?.GuardianPhone);
            });
        });
    }

    private void ComposeFamilyInfo(IContainer container, StudentCardViewModel vm)
    {
        container.Column(col =>
        {
            SectionTitle(col, "가족 정보", Colors.Green.Darken2);
            col.Item().Border(0.5f).BorderColor(BorderColor).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(LabelWidth); c.RelativeColumn(1);
                    c.ConstantColumn(50);         c.RelativeColumn(2);
                });
                LabelCell(table, "아버지");   ValueCell(table, vm.Detail?.FatherName);
                LabelCell(table, "연락처");   ValueCell(table, vm.Detail?.FatherPhone);
                LabelCell(table, "어머니");   ValueCell(table, vm.Detail?.MotherName);
                LabelCell(table, "연락처");   ValueCell(table, vm.Detail?.MotherPhone);
                LabelCell(table, "가족구성"); ValueCellSpan(table, vm.Detail?.FamilyInfo, 3);
            });
        });
    }

    private void ComposeCareerInfo(IContainer container, StudentCardViewModel vm)
    {
        container.Column(col =>
        {
            SectionTitle(col, "진로 및 관심사", Colors.Orange.Darken2);
            col.Item().Border(0.5f).BorderColor(BorderColor).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(LabelWidth); c.RelativeColumn(1);
                    c.ConstantColumn(LabelWidth); c.RelativeColumn(1);
                });
                LabelCell(table, "진로희망"); ValueCell(table, vm.Detail?.CareerGoal);
                LabelCell(table, "특기");     ValueCell(table, vm.Detail?.Talents);
                LabelCell(table, "관심/취미"); ValueCellSpan(table, vm.Detail?.Interests, 3);
            });
        });
    }

    private void ComposeHealthInfo(IContainer container, StudentCardViewModel vm)
    {
        container.Column(col =>
        {
            SectionTitle(col, "건강 정보", Colors.Red.Darken2);
            col.Item().Border(0.5f).BorderColor(BorderColor).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(LabelWidth); c.RelativeColumn(1);
                    c.ConstantColumn(LabelWidth); c.RelativeColumn(1);
                });
                LabelCell(table, "건강정보"); ValueCellSpan(table, vm.Detail?.HealthInfo, 3);
                LabelCell(table, "알레르기"); ValueCell(table, vm.Detail?.Allergies);
                LabelCell(table, "특수교육"); ValueCell(table, vm.Detail?.SpecialNeeds);
            });
        });
    }

    private void ComposeDetailMemo(IContainer container, StudentCardViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Detail?.Memo)) return;
        container.Column(col =>
        {
            SectionTitle(col, "상세 메모", Colors.Grey.Darken2);
            col.Item().Border(0.5f).BorderColor(BorderColor).Padding(8)
                .Text(vm.Detail?.Memo ?? "").FontSize(ValueFontSize);
        });
    }

    private void ComposeStudentLogs(IContainer container, List<StudentLogViewModel> logs)
    {
        container.Column(column =>
        {
            SectionTitle(column, "누가기록", Colors.Purple.Darken2);
            column.Item().Border(0.5f).BorderColor(BorderColor).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(65);
                    columns.ConstantColumn(65);
                    columns.RelativeColumn();
                });
                table.Header(header =>
                {
                    foreach (var title in new[] { "날짜", "카테고리", "과목", "내용" })
                        header.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(BorderColor)
                            .Padding(5).AlignCenter().Text(title).FontSize(LabelFontSize).SemiBold();
                });
                foreach (var log in logs)
                {
                    TableDataCell(table, log.Date.ToString("MM/dd"));
                    TableDataCell(table, log.Category.ToString());
                    TableDataCell(table, log.SubjectName ?? "");
                    TableDataCell(table, log.Log ?? "");
                }
            });
        });
    }

    private static void SectionTitle(ColumnDescriptor col, string title, string color)
        => col.Item().Background(Colors.Grey.Lighten4).BorderBottom(1.5f).BorderColor(color)
            .Padding(6).Text(title).FontSize(SectionTitleFontSize).Bold().FontColor(color);

    private static void LabelCell(TableDescriptor table, string label)
        => table.Cell().Border(0.5f).BorderColor(BorderColor).Background(Colors.Grey.Lighten4)
            .Padding(5).AlignRight().AlignMiddle().Text(label).FontSize(LabelFontSize).SemiBold().FontColor(LabelColor);

    private static void ValueCell(TableDescriptor table, string? value)
        => table.Cell().Border(0.5f).BorderColor(BorderColor).Padding(5).AlignMiddle()
            .Text(value ?? "").FontSize(ValueFontSize);

    private static void ValueCellSpan(TableDescriptor table, string? value, uint colSpan)
        => table.Cell().ColumnSpan(colSpan).Border(0.5f).BorderColor(BorderColor)
            .Padding(5).AlignMiddle().Text(value ?? "").FontSize(ValueFontSize);

    private static void TableDataCell(TableDescriptor table, string text)
        => table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .Padding(4).Text(text).FontSize(9);

    private void ConfigureKoreanFont()
    {
        var fontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!QuestPDF.Settings.FontDiscoveryPaths.Contains(fontsPath))
            QuestPDF.Settings.FontDiscoveryPaths.Add(fontsPath);
    }

    public async Task<string?> GenerateClassCardsPdfFromDbAsync(int year, int grade, int classNo)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        ConfigureKoreanFont();

        var students = await LoadClassStudentsAsync(year, grade, classNo);
        if (students.Count == 0) return null;

        var dir = Path.Combine(Settings.UserDataPath, "Prints");
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"학생카드_{grade}학년{classNo}반_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                foreach (var vm in students)
                {
                    var g = vm.Enrollment?.Grade ?? grade;
                    var c = vm.Enrollment?.Class ?? classNo;
                    var n = vm.Enrollment?.Number ?? 0;
                    var y = vm.Enrollment?.Year ?? year;

                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);
                        page.PageColor(Colors.White);
                        page.Header().Element(hc => ComposeHeader(hc, y, g, c, n));
                        page.Content().Element(cc => ComposeContent(cc, vm, includeDetailInfo: true, studentLogs: null));
                        page.Footer().Element(ComposeFooter);
                    });
                }
            }).GeneratePdf(filePath);
        });

        return filePath;
    }

    internal static async Task<List<StudentCardViewModel>> LoadClassStudentsAsync(int year, int grade, int classNo)
    {
        string schoolCode = Settings.SchoolCode.Value;
        using var enrollmentService = new EnrollmentService();
        var enrollments = await enrollmentService.GetClassRosterAsync(schoolCode, year, grade, classNo);
        if (enrollments.Count == 0) return [];

        var ids = enrollments.Select(e => e.StudentID).ToList();
        using var studentService = new StudentService(SchoolDatabase.DbPath);
        var students = (await studentService.GetStudentsByIdsAsync(ids))
            .ToDictionary(s => s.StudentID, s => s);

        using var detailService = new StudentDetailService(SchoolDatabase.DbPath);
        var details = (await detailService.GetByStudentIdsAsync(ids))
            .ToDictionary(d => d.StudentID, d => d);

        var list = new List<StudentCardViewModel>();
        foreach (var e in enrollments.OrderBy(x => x.Number))
        {
            students.TryGetValue(e.StudentID, out var st);
            details.TryGetValue(e.StudentID, out var d);
            var vm = new StudentCardViewModel();
            vm.LoadFromModels(e, st, d);
            list.Add(vm);
        }
        return list;
    }
}
