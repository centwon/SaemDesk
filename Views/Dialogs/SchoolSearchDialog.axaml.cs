using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.Views.Dialogs;

/// <summary>NEIS 학교 검색 다이얼로그.</summary>
public partial class SchoolSearchDialog : Window, IDialogResult<School?>
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    /// <summary>선택된 학교. null이면 취소.</summary>
    public School? Result { get; private set; }

    public SchoolSearchDialog()
    {
        InitializeComponent();

        // ItemsSource를 code-behind에서 설정 (AOT 안전)
        ResultsList.SelectionChanged += OnResultSelectionChanged;
    }

    // ── 검색 ──────────────────────────────────────────────────
    private async void OnSearch(object? sender, RoutedEventArgs e) => await RunSearchAsync();

    private async void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        string keyword = SearchBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyword))
        {
            StatusText.Text = "학교명을 입력하세요.";
            return;
        }

        SearchButton.IsEnabled = false;
        Loading.IsVisible     = true;
        StatusText.Text        = "검색 중…";
        SelectButton.IsEnabled = false;
        ResultsList.ItemsSource = null;

        try
        {
            string apiKey = Settings.NeisApiKey.Value;
            string url    = $"https://open.neis.go.kr/hub/schoolInfo" +
                            $"?KEY={Uri.EscapeDataString(apiKey)}" +
                            $"&Type=xml&pSize=100" +
                            $"&SCHUL_NM={Uri.EscapeDataString(keyword)}";

            string xml    = await _http.GetStringAsync(url);
            var schools   = ParseSchools(xml);

            ResultsList.ItemsSource = schools;
            StatusText.Text = schools.Count > 0
                ? $"검색 결과: {schools.Count}개 — 학교를 선택 후 [선택] 버튼을 누르세요."
                : "검색 결과가 없습니다.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"검색 실패: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
            Loading.IsVisible      = false;
        }
    }

    // ── XML 파싱 ───────────────────────────────────────────────
    private static List<School> ParseSchools(string xml)
    {
        var list = new List<School>();
        try
        {
            var doc  = XDocument.Parse(xml);
            foreach (var row in doc.Descendants("row"))
            {
                list.Add(new School
                {
                    SchoolCode         = row.Element("SD_SCHUL_CODE")?.Value  ?? string.Empty,
                    SchoolName         = row.Element("SCHUL_NM")?.Value       ?? string.Empty,
                    ATPT_OFCDC_SC_CODE = row.Element("ATPT_OFCDC_SC_CODE")?.Value ?? string.Empty,
                    ATPT_OFCDC_SC_NAME = row.Element("ATPT_OFCDC_SC_NAME")?.Value ?? string.Empty,
                    SchoolType         = row.Element("SCHUL_KND_SC_NM")?.Value ?? string.Empty,
                    Address            = row.Element("ORG_RDNMA")?.Value      ?? string.Empty,
                    Phone              = row.Element("ORG_TELNO")?.Value      ?? string.Empty,
                    Fax                = row.Element("ORG_FAXNO")?.Value      ?? string.Empty,
                    Website            = row.Element("HMPG_ADRES")?.Value     ?? string.Empty,
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SchoolSearchDialog] XML 파싱 오류: {ex.Message}");
        }
        return list;
    }

    // ── 선택 상태 ──────────────────────────────────────────────
    private void OnResultSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = ResultsList.SelectedItem is School;
    }

    // ── 버튼 ───────────────────────────────────────────────────
    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        Result = ResultsList.SelectedItem as School;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
