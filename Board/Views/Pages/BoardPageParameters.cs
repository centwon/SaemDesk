using SaemDesk.Board.Models;

namespace SaemDesk.Board.Views.Pages;

/// <summary>PostListPage 파라미터 — NewSchool PostListPageParameter 동등.</summary>
public sealed class PostListPageParameter
{
    public string        Title              { get; set; } = "";
    public string        Category           { get; set; } = "";
    public string        Subject            { get; set; } = "";
    public bool          AllowCategoryChange { get; set; } = true;
    public bool          ShowSubjectFilter  { get; set; }
    public bool          AllowViewModeChange { get; set; } = true;
    public bool          IsEmbedded         { get; set; }
    public BoardViewMode ViewMode           { get; set; } = BoardViewMode.Default;
}

/// <summary>PostDetailPage 파라미터 — NewSchool PostDetailPageParameter 동등.</summary>
public sealed class PostDetailPageParameter
{
    public int                  PostNo         { get; set; }
    public PostListPageParameter? BoardParameter { get; set; }
}

/// <summary>PostEditPage 파라미터 — NewSchool PostEditPageParameter 동등.</summary>
public sealed class PostEditPageParameter
{
    public int    PostNo             { get; set; }
    public string DefaultCategory   { get; set; } = "";
    public string DefaultSubject    { get; set; } = "";
    public bool   AllowCategoryChange { get; set; } = true;
}
