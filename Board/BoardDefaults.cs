using System.Collections.Generic;
using SaemDesk.Models;

namespace SaemDesk.Board;

/// <summary>
/// 게시판 기본 카테고리 / 카테고리별 기본 주제 — 목록·작성 페이지 공용 단일 출처.
/// </summary>
public static class BoardDefaults
{
    /// <summary>CategoryNames.All(수업/학급/업무/개인) + 게시판 전용 추가 항목.</summary>
    public static readonly IReadOnlyList<string> Categories =
        [.. CategoryNames.All, "동아리", "기타"];

    /// <summary>카테고리별 기본 주제 제안.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Topics =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["학급"]   = new[] { "통계", "학급 자료", "학생 자료", "학급 안내" },
            ["수업"]   = new[] { "통계", "수업 자료", "과제" },
            ["동아리"] = new[] { "통계", "동아리 자료", "활동 안내" },
        };
}
