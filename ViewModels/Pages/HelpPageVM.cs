namespace SaemDesk.ViewModels.Pages;

/// <summary>
/// 도움말 페이지 ViewModel — 정적 콘텐츠만 노출 (현재는 바인딩 데이터 없음).
/// </summary>
public partial class HelpPageVM : ViewModelBase
{
    public string AppVersion => AppInfo.Version;
}
