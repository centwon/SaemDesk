using Avalonia.Controls;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class UnifiedExportPage : UserControl
{
    private UnifiedExportPageVM VM => (UnifiedExportPageVM)DataContext!;

    public UnifiedExportPage()
    {
        InitializeComponent();
        FilterBar.SelectionChanged += OnFilterChanged;
    }

    private void OnFilterChanged(object? sender, FilterChangedEventArgs e)
    {
        VM.Year    = e.Year;
        VM.Grade   = e.Grade;
        VM.ClassNo = e.Class;
    }
}
