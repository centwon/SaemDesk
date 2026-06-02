using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Controls;

/// <summary>
/// RichEditorView 를 모달 다이얼로그 창으로 감싼 래퍼 — JoditEditorWin 대체.
/// HTML 문자열 in/out (<see cref="Html"/>), 확인/취소 결과 반환.
/// </summary>
public partial class RichEditorWindow : Window
{
    private bool _result;

    /// <summary>확인: true, 취소/X: false</summary>
    public bool IsSuccess => _result;

    /// <summary>에디터 내용 HTML get/set.</summary>
    public string Html
    {
        get => editor.Editor.ToHtml();
        set => editor.Editor.LoadHtml(value);
    }

    public RichEditorWindow() : this("편집기") { }

    public RichEditorWindow(string title) : this(title, string.Empty) { }

    public RichEditorWindow(string title, string initialHtml)
    {
        InitializeComponent();
        Title = title;
        editor.Editor.LoadHtml(initialHtml); // RichEditor 는 WebView 초기화 대기 불필요
    }

    /// <summary>창 크기 조절.</summary>
    public void SetSize(int width, int height)
    {
        Width  = width;
        Height = height;
    }

    /// <summary>모달로 표시하고 확인 여부 반환.</summary>
    public async Task<bool> ShowDialogAsync(Window owner)
    {
        await ShowDialog<bool>(owner);
        return _result;
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)     { _result = true;  Close(true); }
    private void BtnCancel_Click(object? sender, RoutedEventArgs e) { _result = false; Close(false); }
}
