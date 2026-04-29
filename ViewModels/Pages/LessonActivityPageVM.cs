using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaemDesk.Models;
using SaemDesk.Services;

namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 수업 활동 기록 페이지 ViewModel.
/// 원본 NewSchool LessonActivityPage — 수업(Course) + 강의실(Room) 선택 후
/// 해당 수강생 ListStudent + LogListViewer 표시.
/// </summary>
public partial class LessonActivityPageVM : ViewModelBase
{
    public ObservableCollection<Course> Courses { get; } = new();
    public ObservableCollection<string> Rooms   { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCourse))]
    private Course? _selectedCourse;

    [ObservableProperty] private string? _selectedRoom;

    public bool HasCourse => SelectedCourse is not null;

    public LessonActivityPageVM()
    {
        _ = LoadCoursesAsync();
    }

    [RelayCommand]
    private async Task LoadCoursesAsync()
    {
        try
        {
            using var svc = new CourseService();
            var list = await svc.GetMyCoursesAsync();

            Courses.Clear();
            foreach (var c in list) Courses.Add(c);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LessonActivityPageVM] LoadCourses: {ex.Message}");
        }
    }

    partial void OnSelectedCourseChanged(Course? value)
    {
        Rooms.Clear();
        if (value is null) return;
        foreach (var r in value.RoomList ?? Enumerable.Empty<string>())
            Rooms.Add(r);
        if (Rooms.Count > 0) SelectedRoom = Rooms[0];
    }
}
