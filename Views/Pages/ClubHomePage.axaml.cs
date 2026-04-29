using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.ViewModels.Pages;

namespace SaemDesk.Views.Pages;

/// <summary>
/// 동아리 홈 페이지 — Avalonia 12 이식.
/// 원본: NewSchool.Pages.ClubHomePage (WinUI3).
/// 구성: 좌(동아리 선택 + 부원 명단) / 우(자료실 플레이스홀더).
/// </summary>
public partial class ClubHomePage : UserControl
{
    private ClubHomePageVM VM => (ClubHomePageVM)DataContext!;

    public ClubHomePage()
    {
        InitializeComponent();
        DataContext = new ClubHomePageVM();
    }

    private async void OnClubChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (VM.SelectedClub is null) return;

        TxtClubInfo.Text   = VM.SelectedClub.ActivityRoom ?? "";
        TxtMemberCount.Text = "";

        await VM.LoadMembersAsync();

        TxtMemberCount.Text     = $"부원 {VM.Members.Count}명";
        EmptyMemberState.IsVisible = VM.Members.Count == 0;
        MemberListView.IsVisible   = VM.Members.Count > 0;
    }

    private void OnActivityClick(object? sender, RoutedEventArgs e)
    {
        // TODO: ClubActivityPage 열기
    }
}
