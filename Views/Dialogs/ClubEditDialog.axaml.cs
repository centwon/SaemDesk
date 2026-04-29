using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Views.Dialogs;

/// <summary>
/// 동아리 추가/수정 다이얼로그.
/// 원본: NewSchool.Dialogs.ClubEditDialog (WinUI3 ContentDialog).
/// </summary>
public partial class ClubEditDialog : Window
{
    private readonly Club?   _existing;
    private readonly string  _schoolCode;
    private readonly string  _teacherId;
    private readonly int     _year;
    private readonly bool    _isEdit;

    public bool IsSuccess { get; private set; }

    /// <summary>새 동아리 추가</summary>
    public ClubEditDialog(string schoolCode, string teacherId, int year)
    {
        InitializeComponent();
        _schoolCode = schoolCode;
        _teacherId  = teacherId;
        _year       = year;
        _isEdit     = false;
        Title       = "동아리 추가";
    }

    /// <summary>기존 동아리 수정</summary>
    public ClubEditDialog(Club club)
    {
        InitializeComponent();
        _existing   = club;
        _schoolCode = club.SchoolCode;
        _teacherId  = club.TeacherID;
        _year       = club.Year;
        _isEdit     = true;
        Title       = "동아리 수정";

        TxtClubName.Text      = club.ClubName;
        TxtActivityRoom.Text  = club.ActivityRoom;
        TxtRemark.Text        = club.Remark;
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        ErrorBar.IsVisible = false;

        if (string.IsNullOrWhiteSpace(TxtClubName.Text))
        {
            ErrorText.Text      = "동아리명을 입력해 주세요.";
            ErrorBar.IsVisible  = true;
            return;
        }

        try
        {
            var club = _isEdit ? _existing! : new Club();

            club.SchoolCode    = _schoolCode;
            club.TeacherID     = _teacherId;
            club.Year          = _year;
            club.ClubName      = TxtClubName.Text.Trim();
            club.ActivityRoom  = TxtActivityRoom.Text?.Trim() ?? "";
            club.Remark        = TxtRemark.Text?.Trim()       ?? "";
            club.UpdatedAt     = DateTime.Now;

            using var repo = new ClubRepository(SchoolDatabase.DbPath);
            if (_isEdit)
                await repo.UpdateAsync(club);
            else
            {
                club.CreatedAt = DateTime.Now;
                await repo.CreateAsync(club);
            }

            IsSuccess = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text     = $"저장 오류: {ex.Message}";
            ErrorBar.IsVisible = true;
            Debug.WriteLine($"[ClubEditDialog] Save: {ex.Message}");
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
