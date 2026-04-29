using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 범용 확인/취소 다이얼로그.
/// 시그니처:
///   new ConfirmDialog("제목", "메시지")              — 확인/취소 버튼
///   new ConfirmDialog("메시지", "확인버튼", "취소버튼") — 커스텀 버튼 레이블
/// </summary>
public partial class ConfirmDialog : Window
{
    public bool Result { get; private set; }

    // ────────────────────────────────────────────────────
    //  생성자
    // ────────────────────────────────────────────────────

    /// <summary>기본: 제목 + 메시지, 버튼 레이블은 "확인"/"취소"</summary>
    public ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        Title              = title;
        MessageText.Text   = message;
        ConfirmButton.Content = "확인";
        CancelButton.Content  = "취소";
    }

    /// <summary>
    /// 커스텀 버튼 레이블. 첫 인자가 메시지, 이후 confirm/cancel 레이블.
    /// 예: new ConfirmDialog("저장하시겠습니까?", "저장", "건너뛰기")
    /// </summary>
    public ConfirmDialog(string message, string confirmLabel, string cancelLabel)
    {
        InitializeComponent();
        Title                 = "확인";
        MessageText.Text      = message;
        ConfirmButton.Content = confirmLabel;
        CancelButton.Content  = cancelLabel;
    }

    // ────────────────────────────────────────────────────
    //  ShowDialog 헬퍼 — 결과를 bool로 반환
    // ────────────────────────────────────────────────────

    public async Task<bool> ShowDialogAsync(Window owner)
    {
        await ShowDialog(owner);
        return Result;
    }

    // ────────────────────────────────────────────────────
    //  이벤트
    // ────────────────────────────────────────────────────

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
