using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.HtmlEditor.TestApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ShowHtmlBtn.Click += OnShowHtml;
        LoadSampleBtn.Click += OnLoadSample;

        // 시작 시 샘플 로드
        LoadSample();
    }

    private void OnShowHtml(object? sender, RoutedEventArgs e)
    {
        HtmlOutput.Text = Editor.Html;
    }

    private void OnLoadSample(object? sender, RoutedEventArgs e)
    {
        LoadSample();
    }

    private void LoadSample()
    {
        Editor.Html = """
            <h1 style="text-align: center">SaemDesk HtmlEditor 테스트</h1>
            <p>일반 텍스트 단락입니다. <strong>굵은 글씨</strong>와 <em>기울임 글씨</em>를 포함합니다.</p>
            <p><span style="color: #FF0000">빨간색 텍스트</span>와 <span style="font-size: 20px">큰 글씨</span>도 지원합니다.</p>
            <p style="text-align: center">가운데 정렬된 단락</p>
            <p style="text-align: right">오른쪽 정렬된 단락</p>
            <h2>표 테스트</h2>
            <table>
              <tr><td><strong>이름</strong></td><td><strong>과목</strong></td><td><strong>점수</strong></td></tr>
              <tr><td>김철수</td><td>국어</td><td>95</td></tr>
              <tr><td>이영희</td><td>수학</td><td>88</td></tr>
              <tr><td>박민수</td><td>영어</td><td>92</td></tr>
            </table>
            <h2>링크 테스트</h2>
            <p><a href="https://avaloniaui.net">Avalonia 공식 사이트</a>를 방문하세요.</p>
            """;
    }
}
