using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SaemDesk.Helpers;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학생 특이사항 편집 — Avalonia 12 이식.
/// NEIS 바이트 카운터(유형별 한도), 저장/삭제, 맞춤법 검사 링크.
/// </summary>
public partial class StudentSpecBox : UserControl
{
    private StudentSpecial? _special;
    private bool _isModified;
    private string _originalContent = string.Empty;

    public StudentSpecial? Special
    {
        get => _special;
        set { _special = value; LoadSpecial(); }
    }

    public bool IsModified => _isModified;

    public StudentSpecBox()
    {
        InitializeComponent();
    }

    private void LoadSpecial()
    {
        if (_special == null) { ClearUI(); return; }

        TxtYear.Text = _special.Year.ToString();
        TxtType.Text = _special.Type;
        TxtSubject.Text = _special.SubjectName;

        bool showSemester = _special.Type == "교과활동";
        TxtSemester.IsVisible = showSemester;
        TxtSemesterLabel.IsVisible = showSemester;

        TxtStudent.Text = _special.StudentID;

        TxtContent.Text = _special.Content ?? string.Empty;
        _originalContent = TxtContent.Text;
        _isModified = false;

        TxtContent.IsReadOnly = _special.IsFinalized;
        BtnSave.IsEnabled = !_special.IsFinalized;
        BtnDelete.IsEnabled = !_special.IsFinalized;

        UpdateByteInfo();
    }

    private void ClearUI()
    {
        TxtYear.Text = DateTime.Today.Year.ToString();
        TxtType.Text = "";
        TxtSubject.Text = "";
        TxtStudent.Text = "";
        TxtContent.Text = "";
        TxtByteInfo.Text = "0 / 1500 Byte (0자)";
        TxtByteInfo.Foreground = Brushes.Black;
        _originalContent = string.Empty;
        _isModified = false;
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_special == null) return;
        _isModified = TxtContent.Text != _originalContent;
        UpdateByteInfo();
    }

    private async void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_isModified || _special == null) return;
        // 자동 저장 (확인 다이얼로그 없이 즉시 저장 — 변경 검출됨)
        await SaveAsync();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_special == null) { Debug.WriteLine("저장할 내용 없음"); return; }
        if (!_isModified)     { Debug.WriteLine("변경사항 없음");   return; }
        await SaveAsync();
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_special == null) return;

        try
        {
            if (_special.No > 0)
            {
                using var svc = new StudentSpecialService();
                await svc.DeleteAsync(_special.No);
            }

            TxtContent.Text = string.Empty;
            _originalContent = string.Empty;
            _isModified = false;
            UpdateByteInfo();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StudentSpecBox] 삭제 실패: {ex.Message}");
        }
    }

    private void OnSpellCheckClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            const string url = "https://nara-speller.co.kr/speller";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StudentSpecBox] 브라우저 실행 실패: {ex.Message}");
        }
    }

    private async Task SaveAsync()
    {
        if (_special == null) return;
        try
        {
            _special.Content = TxtContent.Text ?? string.Empty;
            using var svc = new StudentSpecialService();

            if (_special.No > 0) await svc.UpdateAsync(_special);
            else                 _special.No = await svc.CreateAsync(_special);

            _originalContent = TxtContent.Text ?? string.Empty;
            _isModified = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StudentSpecBox] 저장 실패: {ex.Message}");
        }
    }

    private void UpdateByteInfo()
    {
        if (_special == null) return;

        string text = TxtContent.Text ?? string.Empty;
        int currentBytes = NeisHelper.CountByte(text);
        int maxBytes = NeisHelper.GetMaxBytes(_special.Type);

        TxtByteInfo.Text = $"{currentBytes} / {maxBytes} Byte ({text.Length}자)";
        TxtByteInfo.Foreground = currentBytes > maxBytes ? Brushes.Red : Brushes.Black;
    }

    public async Task<bool> ForceSaveAsync()
    {
        if (!_isModified || _special == null) return false;
        try { await SaveAsync(); return true; }
        catch { return false; }
    }

    public void DiscardChanges()
    {
        if (_special == null) return;
        TxtContent.Text = _originalContent;
        _isModified = false;
        UpdateByteInfo();
    }
}
