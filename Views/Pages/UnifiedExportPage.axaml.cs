using Avalonia.Controls;
using Avalonia.Input.Platform;
using SaemDesk.ViewModels.Pages;
using SaemDesk.Views.Controls;

namespace SaemDesk.Views.Pages;

public partial class UnifiedExportPage : UserControl
{
    private UnifiedExportPageVM VM => (UnifiedExportPageVM)DataContext!;

    public UnifiedExportPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SubscribeVM();

        YearSemPicker.YearSemesterChanged += OnYearSemesterChanged;
        ClassFilter.ClassChanged += OnClassFilterChanged;
    }

    private void SubscribeVM()
    {
        if (DataContext is UnifiedExportPageVM vm)
        {
            vm.ClipboardTextReady += OnClipboardTextReady;
            // RichEditor 는 바인딩 가능한 HTML 프로퍼티가 없으므로 PreviewHtml 변경 시 LoadHtml
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UnifiedExportPageVM.PreviewHtml))
                    PreviewEditor.LoadHtml(vm.PreviewHtml);
            };
        }
    }

    private async void OnClipboardTextReady(object? sender, string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    private async void OnYearSemesterChanged(object? sender, YearSemesterChangedEventArgs e)
    {
        VM.Year = e.Year;
        await ClassFilter.LoadAsync(e.Year, e.Semester);
    }

    private void OnClassFilterChanged(object? sender, ClassChangedEventArgs e)
    {
        VM.Year    = e.Year;
        VM.Grade   = e.Grade;
        VM.ClassNo = e.Class;
    }
}
