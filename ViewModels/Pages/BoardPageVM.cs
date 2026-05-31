namespace SaemDesk.ViewModels.Pages;

/// <summary>게시판 페이지 파라미터 — NewSchool PostListPageParameter 과 동등.</summary>
public sealed class BoardPageParameter
{
    /// <summary>상단에 표시할 제목 (null 이면 기본 "게시판").</summary>
    public string? Title { get; init; }

    /// <summary>카테고리 변경 허용 여부. 아카이브는 true, 학급/수업 게시판은 false.</summary>
    public bool AllowCategoryChange { get; init; } = true;

    /// <summary>주제(Subject) 필터 표시 여부. 아카이브는 true.</summary>
    public bool ShowSubjectFilter { get; init; }

    /// <summary>특정 카테고리로 고정 (AllowCategoryChange=false 일 때 사용).</summary>
    public string? FixedCategory { get; init; }
}

/// <summary>
/// 게시판 페이지 VM — 실제 목록/상세/작성은 BoardPage 컨테이너가
/// PostListPage·PostDetailPage·PostEditPage 로 처리한다. 이 VM 은 파라미터 전달만 담당.
/// </summary>
public sealed class BoardPageVM : ViewModelBase
{
    /// <summary>현재 적용된 파라미터 — BoardPage.axaml.cs 에서 참조.</summary>
    public BoardPageParameter? Parameter { get; }

    public BoardPageVM() : this(null) { }

    public BoardPageVM(BoardPageParameter? param) => Parameter = param;
}
