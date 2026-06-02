using System.IO;
using System.Text;
using AvaloniaRichEditor.Documents;
using AvaloniaRichEditor.Formatters;

namespace SaemDesk.Helpers;

/// <summary>
/// 게시글 리치 콘텐츠 변환 — HTML ↔ ardx(BLOB) + 검색용 plaintext.
/// 마이그레이션과 신규 저장 경로가 공유하는 단일 변환 지점.
/// </summary>
public static class RichContent
{
    /// <summary>HTML → (ardx 패키지 바이트, 검색용 plaintext). 이미지 디코딩 때문에 UI 스레드에서 호출.</summary>
    public static (byte[] Ardx, string PlainText) FromHtml(string? html)
        => FromDocument(HtmlDocumentFormatter.ParseHtml(html ?? string.Empty));

    /// <summary>
    /// 문서 → (ardx 패키지 바이트, 검색용 plaintext). 직렬화가 brush(SolidColorBrush.Color 등)
    /// 같은 스레드 친화적 객체를 읽으므로 <b>반드시 문서를 만든 UI 스레드에서 동기 호출</b>한다.
    /// (RichEditor.SavePackageAsync 는 백그라운드 스레드라 색 있는 문서에서 예외 발생.)
    /// </summary>
    public static (byte[] Ardx, string PlainText) FromDocument(FlowDocument doc)
    {
        using var ms = new MemoryStream();
        DocumentPackage.Save(doc, ms);
        return (ms.ToArray(), PlainText(doc));
    }

    /// <summary>문서의 텍스트만 추출 (문단·표 셀을 줄바꿈으로 구분) — 검색·미리보기용.</summary>
    public static string PlainText(FlowDocument doc)
    {
        var sb = new StringBuilder();

        void AddPara(Paragraph p)
        {
            if (sb.Length > 0) sb.Append('\n');
            foreach (var inline in p.Inlines)
                if (inline is Run { Text: { Length: > 0 } t }) sb.Append(t);
        }

        foreach (var block in doc.Blocks)
        {
            if (block is Paragraph p) AddPara(p);
            else if (block is TableBlock tb)
                foreach (var (_, _, cell) in tb.LogicalCells()) AddPara(cell);
        }
        return sb.ToString();
    }
}
