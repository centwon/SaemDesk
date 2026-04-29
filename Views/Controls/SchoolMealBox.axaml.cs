using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Models;

namespace SaemDesk.Views.Controls;

/// <summary>급식 정보 표시 — Avalonia 12 이식. CalendarDatePicker + ◀▶ 이동.</summary>
public partial class SchoolMealBox : UserControl
{
    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<SchoolMealBox, DateTime?>(nameof(SelectedDate), DateTime.Today);

    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    private ObservableCollection<SchoolMeal> _meals = new();

    public ObservableCollection<SchoolMeal> Meals
    {
        get => _meals;
        set
        {
            _meals = value ?? new ObservableCollection<SchoolMeal>();
            MealsRepeater.ItemsSource = _meals;
        }
    }

    public SchoolMealBox()
    {
        InitializeComponent();
        MealsRepeater.ItemsSource = _meals;
        DatePicker.SelectedDate = DateTime.Today;

        SelectedDateProperty.Changed.AddClassHandler<SchoolMealBox>(async (s, e) =>
        {
            if (e.NewValue is DateTime d)
            {
                if (s.DatePicker.SelectedDate != d) s.DatePicker.SelectedDate = d;
                await s.LoadMealsAsync(d);
            }
        });
    }

    private void DatePicker_DateChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (DatePicker.SelectedDate is DateTime d) SelectedDate = d;
    }

    public async Task LoadMealsAsync(DateTime date)
    {
        try
        {
            var meals = await Functions.GetSchoolMealsAsync(date, mmealScCode: "");
            _meals.Clear();
            if (meals != null)
                foreach (var m in meals) _meals.Add(m);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SchoolMealBox] LoadMealsAsync 오류: {ex.Message}");
            _meals.Clear();
        }
    }

    public void SetMeals(List<SchoolMeal> meals)
    {
        _meals.Clear();
        if (meals != null) foreach (var m in meals) _meals.Add(m);
    }

    private void PreviousDayButton_Click(object? sender, RoutedEventArgs e)
        => SelectedDate = (SelectedDate ?? DateTime.Today).AddDays(-1);

    private void NextDayButton_Click(object? sender, RoutedEventArgs e)
        => SelectedDate = (SelectedDate ?? DateTime.Today).AddDays(1);
}
