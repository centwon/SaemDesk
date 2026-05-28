using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace SaemDesk.Views.Controls;

/// <summary>
/// Jodit HTML 에디터 — Avalonia.Controls.NativeWebView 기반.
/// WinUI3 JoditEditor(WebView2 CoreWebView2 직접 접근) → Avalonia NativeWebView API 이식.
///
/// ⚠ Phase 4 주의사항:
///   - NativeWebView.InvokeScript / WebMessageReceived API 사용
///   - Jodit 에셋은 Settings.UserDataPath/Jodit/ 에 추출 후 file:// 로 탐색
/// </summary>
public partial class JoditEditor : UserControl, IDisposable
{
    // ────────────────────────────────────────────────────
    //  에디터 모드
    // ────────────────────────────────────────────────────

    public enum EditorMode { ReadOnly, Simple, Full }

    // ────────────────────────────────────────────────────
    //  StyledProperty
    // ────────────────────────────────────────────────────

    public static readonly StyledProperty<EditorMode> ModeProperty =
        AvaloniaProperty.Register<JoditEditor, EditorMode>(nameof(Mode), defaultValue: EditorMode.Simple);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<JoditEditor, string>(nameof(Text), defaultValue: string.Empty);

    public EditorMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public string     Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>에디터 내용이 변경될 때 발생 (JS → C# 이벤트).</summary>
    public event EventHandler<string>? TextChanged;

    // ────────────────────────────────────────────────────
    //  내부 필드
    // ────────────────────────────────────────────────────

    private bool _isInitialized;
    private bool _isUpdatingFromEditor;
    private bool _disposed;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>Jodit 에셋이 추출된 로컬 디렉토리 경로.</summary>
    private static string JoditDir =>
        Path.Combine(Settings.UserDataPath, "Jodit");

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    public JoditEditor()
    {
        InitializeComponent();

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;

        ModeProperty.Changed.AddClassHandler<JoditEditor>(async (s, _) =>
            await s.ApplyModeAsync());

        TextProperty.Changed.AddClassHandler<JoditEditor>(async (s, _) =>
        {
            if (!s._isUpdatingFromEditor)
                await s.SetEditorTextAsync(s.Text);
        });
    }

    // ────────────────────────────────────────────────────
    //  Loaded / Unloaded
    // ────────────────────────────────────────────────────

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitialized || _disposed) return;
        await InitAsync();
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Dispose();
    }

    // ────────────────────────────────────────────────────
    //  초기화
    // ────────────────────────────────────────────────────

    private async Task InitAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized || _disposed) return;

            // 1. Jodit 에셋 추출
            await ExtractJoditAssetsAsync();

            // 2. WebView 이벤트 연결
            WebViewControl.NavigationCompleted += OnNavigationCompleted;
            WebViewControl.WebMessageReceived  += OnWebMessageReceived;

            // 3. editor.html 로드
            var htmlPath = Path.Combine(JoditDir, "editor.html");
            WebViewControl.Source = new Uri($"file:///{htmlPath.Replace('\\', '/')}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JoditEditor] InitAsync 오류: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ────────────────────────────────────────────────────
    //  WebView 이벤트
    // ────────────────────────────────────────────────────

    private async void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            // ⚠ _isInitialized를 먼저 설정해야 ApplyModeAsync/SetEditorTextAsync가 동작
            _isInitialized = true;

            // 모드 적용
            await ApplyModeAsync();

            // 초기 텍스트 설정
            if (!string.IsNullOrEmpty(Text))
                await SetEditorTextAsync(Text);

            // 로딩 오버레이 숨기기
            if (LoadingOverlay is not null)
                LoadingOverlay.IsVisible = false;

            Debug.WriteLine("[JoditEditor] 초기화 완료");
        });
    }

    private void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        string? raw = e.Body;
        if (raw is null) return;

        // JS가 JSON.stringify({ type, content, ... }) 으로 전송
        try
        {
            var msg = JsonSerializer.Deserialize(raw, JoditEditorJsonContext.Default.JoditMessage);
            if (msg is null) return;

            switch (msg.Type)
            {
                case "contentChanged":
                    string html = msg.Content ?? "";
                    Dispatcher.UIThread.Post(() =>
                    {
                        _isUpdatingFromEditor = true;
                        Text = html;
                        _isUpdatingFromEditor = false;
                        TextChanged?.Invoke(this, html);
                    });
                    break;

                case "ready":
                    Debug.WriteLine("[JoditEditor] JS ready 수신");
                    break;

                case "contentHeight":
                    // 필요 시 높이 조정에 활용
                    break;
            }
        }
        catch (JsonException)
        {
            // JSON 파싱 실패 → 레거시: raw를 그대로 HTML로 처리
            Dispatcher.UIThread.Post(() =>
            {
                _isUpdatingFromEditor = true;
                Text = raw;
                _isUpdatingFromEditor = false;
                TextChanged?.Invoke(this, raw);
            });
        }
    }

    // ────────────────────────────────────────────────────
    //  JS 통신
    // ────────────────────────────────────────────────────

    private async Task ApplyModeAsync()
    {
        if (!_isInitialized) return;
        string modeStr = Mode.ToString();
        await SafeExecAsync($"setEditorMode('{modeStr}')");
    }

    private async Task SetEditorTextAsync(string html)
    {
        if (!_isInitialized) return;
        // HTML 이스케이프 후 JS 문자열로 전달
        string escaped = html.Replace("\\", "\\\\")
                             .Replace("'", "\\'")
                             .Replace("\n", "\\n")
                             .Replace("\r", "");
        await SafeExecAsync($"if(typeof editor !== 'undefined') editor.value = '{escaped}';");
    }

    private async Task SafeExecAsync(string script)
    {
        try
        {
            await WebViewControl.InvokeScript(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JoditEditor] JS 실행 오류: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────────────

    public async Task<string> GetHtmlAsync()
    {
        if (!_isInitialized) return string.Empty;
        try
        {
            var raw = await WebViewControl.InvokeScript("editor.value");
            return JsonSerializer.Deserialize(raw ?? "\"\"", JoditEditorJsonContext.Default.String) ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    public async Task PrintAsync()
    {
        if (!_isInitialized) return;
        await SafeExecAsync("printContent()");
    }

    /// <summary>Jodit 에디터의 현재 커서 위치에 HTML을 삽입합니다.</summary>
    public async Task InsertHtmlAsync(string html)
    {
        if (!_isInitialized) return;
        string escaped = html.Replace("\\", "\\\\")
                             .Replace("'", "\\'")
                             .Replace("\"", "\\\"")
                             .Replace("\n", "\\n")
                             .Replace("\r", "")
                             .Replace("</", "<\\/");
        await SafeExecAsync($"editor.selection.insertHTML('{escaped}');");
    }

    // ────────────────────────────────────────────────────
    //  Jodit 에셋 추출 (avares → 로컬 파일)
    // ────────────────────────────────────────────────────

    private static async Task ExtractJoditAssetsAsync()
    {
        Directory.CreateDirectory(JoditDir);

        string[] assets =
        [
            "editor.html",
            "jodit.fat.min.js",
            "jodit.fat.min.css",
            "purify.min.js",
        ];

        foreach (var asset in assets)
        {
            var dest = Path.Combine(JoditDir, asset);
            if (File.Exists(dest)) continue;   // 이미 추출됨

            var uri = new Uri($"avares://SaemDesk/Assets/Jodit/{asset}");
            await using var stream = AssetLoader.Open(uri);
            await using var file   = File.Create(dest);
            await stream.CopyToAsync(file);
        }
    }

    // ────────────────────────────────────────────────────
    //  IDisposable
    // ────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        WebViewControl.NavigationCompleted -= OnNavigationCompleted;
        WebViewControl.WebMessageReceived  -= OnWebMessageReceived;

        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
