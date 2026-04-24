using System;

namespace SaemDesk.Models;

/// <summary>
/// 좌석 배치 PDF 렌더링용 학생 데이터 (UI 독립).
/// 원본: NewSchool.Controls.PhotoCard.xaml.cs → Models로 분리.
/// </summary>
public class StudentCardData
{
    public string StudentID  { get; set; } = string.Empty;
    public string Name       { get; set; } = string.Empty;
    public int    Number     { get; set; }
    public int    Grade      { get; set; }
    public int    Class      { get; set; }
    public string PhotoPath  { get; set; } = string.Empty;
    /// <summary>성별 ("남"/"여") — 남녀 교차 짝 옵션용</summary>
    public string Sex        { get; set; } = string.Empty;

    public static StudentCardData FromEnrollment(Enrollment enrollment, Student student) => new()
    {
        StudentID = enrollment.StudentID,
        Name      = student.Name,
        Number    = enrollment.Number,
        Grade     = enrollment.Grade,
        Class     = enrollment.Class,
        PhotoPath = student.Photo ?? string.Empty,
        Sex       = student.Sex  ?? string.Empty,
    };
}

/// <summary>좌석 카드 변경 이벤트 인자</summary>
public class StudentCardEventArgs : EventArgs
{
    public int              Row         { get; }
    public int              Col         { get; }
    public StudentCardData? StudentData { get; }

    public StudentCardEventArgs(int row, int col, StudentCardData? studentData)
    {
        Row = row; Col = col; StudentData = studentData;
    }
}
