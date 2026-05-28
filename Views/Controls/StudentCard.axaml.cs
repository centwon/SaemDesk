using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Services;
using SaemDesk.ViewModels;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학생 카드 — Avalonia 12 이식. DataContext = <see cref="StudentCardViewModel"/>.
/// </summary>
public partial class StudentCard : UserControl
{
    public StudentCard()
    {
        InitializeComponent();
        // StudentCardViewModel을 DataContext로 설정
        // (App.FilePicker를 PhotoService에 주입하여 사진 선택 활성화)
        DataContext = new StudentCardViewModel(
            new StudentService(SchoolDatabase.DbPath),
            new StudentDetailService(SchoolDatabase.DbPath),
            new EnrollmentService(),
            new PhotoService(Settings.UserDataPath, App.FilePicker));
        Unloaded += (_, _) => (DataContext as StudentCardViewModel)?.Dispose();
    }

    public StudentCardViewModel? ViewModel => DataContext as StudentCardViewModel;

    private async void AddPhoto_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null) await ViewModel.AddPhotoAsync();
    }

    private async void DeletePhoto_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null) await ViewModel.DeletePhotoAsync();
    }
}
