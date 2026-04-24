using CommunityToolkit.Mvvm.ComponentModel;

namespace SaemDesk.ViewModels.Pages;

/// <summary>설정 페이지.</summary>
public partial class SettingsPageVM : ViewModelBase
{
    [ObservableProperty]
    private string _appVersion = "v0.1.0-alpha";
}
