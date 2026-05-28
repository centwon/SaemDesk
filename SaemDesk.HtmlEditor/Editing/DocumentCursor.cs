namespace SaemDesk.HtmlEditor;

/// <summary>문서 내 위치. BlockIndex = 블록 번호, Offset = 블록 내 문자 오프셋.</summary>
public readonly record struct DocPosition(int BlockIndex, int Offset);

/// <summary>선택 범위. Start == End 이면 캐럿만 존재.</summary>
public sealed class DocSelection
{
    public DocPosition Start { get; set; }
    public DocPosition End { get; set; }

    public bool IsEmpty => Start == End;
    public bool IsSingleBlock => Start.BlockIndex == End.BlockIndex;

    /// <summary>정규화: Start ≤ End 순서로 반환.</summary>
    public (DocPosition first, DocPosition last) Ordered()
    {
        if (Start.BlockIndex < End.BlockIndex) return (Start, End);
        if (Start.BlockIndex > End.BlockIndex) return (End, Start);
        return Start.Offset <= End.Offset ? (Start, End) : (End, Start);
    }
}
