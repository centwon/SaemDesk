using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Controls;

/// <summary>
/// JoditEditor 를 모달 다이얼로그 창으로 감싼 래퍼.
/// NewSchool.Controls.JoditEditorWin(WinUI3) 를 Avalonia 12 용으로 이식.
/// 
/// WinUI3 의 TaskCompletionSource 기반 ShowDialogAsync 패턴을 유지.
/// Avalonia Window.ShowDialog&lt;T&gt; 를 내부적으로 사용.
/// </summary>
public partial class JoditEditorWin : Window
{
    private bool _result;

    // ────────────────────────────────────────────────────
    //  Properties
    // ────────────────────────────────────────────────────

    public JoditEditor.EditorMode EditorMode
    {
        get => joditEditor.Mode;
        set => joditEditor.Mode = value;
    }

    public string Text
    {
        get => joditEditor.Text;
        set => joditEditor.Text = value;
    }

    /// <summary>확인: true, 취소/X: false</summary>
    public bool IsSuccess => _result;

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public JoditEditorWin() : this("편집기") { }

    public JoditEditorWin(string title) : this(title, string.Empty) { }

    public JoditEditorWin(string title, string initialText)
        : this(title, initialText, JoditEditor.EditorMode.Full) { }

    public JoditEditorWin(string title, string initialText, JoditEditor.EditorMode mode)
    {
        InitializeComponent();
        Title = title;
        joditEditor.Mode = mode;

        // InitializeComponent 이후에 Text 설정 (WebView 초기화 대기)
        Opened += async (_, _) =>
        {
            await System.Threading.Tasks.Task.Delay(300); // 에디터 로딩 대기
            joditEditor.Text = initialText;
        };
    }

    // ────────────────────────────────────────────────────
    //  크기 / 위치
    // ────────────────────────────────────────────────────

    /// <summary>창 크기 조절 (NewSchool SetSize() 호환).</summary>
    public void SetSize(int width, int height)
    {
        Width  = width;
        Height = height;
    }

    // ────────────────────────────────────────────────────
    //  Dialog
    // ────────────────────────────────────────────────────

    /// <summary>
    /// 모달로 표시하고 결과(확인 여부) 반환.
    /// NewSchool ShowDialogAsync(Window parent) 와 동일한 시그니처.
    /// </summary>
    public async Task<bool> ShowDialogAsync(Window owner)
    {
        await ShowDialog<bool>(owner);
        return _result;
    }

    // ────────────────────────────────────────────────────
    //  이벤트 핸들러
    // ────────────────────────────────────────────────────

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        _result = true;
        Close(true);
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
    {
        _result = false;
        Close(false);
    }

    // ────────────────────────────────────────────────────
    //  Public Helpers
    // ────────────────────────────────────────────────────

    public async Task<string> GetHtmlAsync() => await joditEditor.GetHtmlAsync();
    public async Task PrintAsync()           => await joditEditor.PrintAsync();
    public bool IsEditorInitialized          => joditEditor.IsInitialized;
}
