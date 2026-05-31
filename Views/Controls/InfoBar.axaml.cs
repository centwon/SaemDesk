using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SaemDesk.Views.Controls;

public enum InfoBarSeverity { Informational, Success, Warning, Error }

/// <summary>
/// WinUI InfoBar 대체 — 심각도별 배경색 + 닫기 버튼.
/// </summary>
public partial class InfoBar : UserControl
{
    // ────────────────────────────────────────────────────
    //  Avalonia StyledProperty 정의
    // ────────────────────────────────────────────────────

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<InfoBar, bool>(nameof(IsOpen), defaultValue: true);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InfoBar, string>(nameof(Title), defaultValue: string.Empty);

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<InfoBar, string>(nameof(Message), defaultValue: string.Empty);

    public static readonly StyledProperty<InfoBarSeverity> SeverityProperty =
        AvaloniaProperty.Register<InfoBar, InfoBarSeverity>(nameof(Severity), defaultValue: InfoBarSeverity.Informational);

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<InfoBar, bool>(nameof(IsClosable), defaultValue: true);

    // ────────────────────────────────────────────────────
    //  CLR 프로퍼티
    // ────────────────────────────────────────────────────

    public bool           IsOpen    { get => GetValue(IsOpenProperty);    set => SetValue(IsOpenProperty, value); }
    public string         Title     { get => GetValue(TitleProperty);     set => SetValue(TitleProperty, value); }
    public string         Message   { get => GetValue(MessageProperty);   set => SetValue(MessageProperty, value); }
    public InfoBarSeverity Severity { get => GetValue(SeverityProperty);  set => SetValue(SeverityProperty, value); }
    public bool           IsClosable{ get => GetValue(IsClosableProperty);set => SetValue(IsClosableProperty, value); }

    public bool HasTitle => !string.IsNullOrEmpty(Title);

    // ────────────────────────────────────────────────────
    //  생성자 + 프로퍼티 변경 핸들러
    // ────────────────────────────────────────────────────

    public InfoBar()
    {
        InitializeComponent();
        SeverityProperty.Changed.AddClassHandler<InfoBar>((s, _) => s.ApplySeverity());
        TitleProperty.Changed.AddClassHandler<InfoBar>((s, _) => s.ApplyText());
        MessageProperty.Changed.AddClassHandler<InfoBar>((s, _) => s.ApplyText());

        // UserControl: named elements are available after InitializeComponent()
        ApplySeverity();
        ApplyText();
    }

    // ────────────────────────────────────────────────────
    //  비주얼 업데이트
    // ────────────────────────────────────────────────────

    private void ApplySeverity()
    {
        if (Root is null) return;

        var (bg, fg, icon) = Severity switch
        {
            InfoBarSeverity.Success       => ("#DFF6DD", "#107C10", "✔"),
            InfoBarSeverity.Warning       => ("#FFF4CE", "#835B00", "⚠"),
            InfoBarSeverity.Error         => ("#FDE7E9", "#D83B01", "✖"),
            _  /* Informational */        => ("#EFF6FC", "#004578", "ℹ"),
        };

        Root.Background = SolidColorBrush.Parse(bg);
        Root.BorderBrush = SolidColorBrush.Parse(fg);
        Root.BorderThickness = new Thickness(1);

        if (IconText is not null)
        {
            IconText.Text       = icon;
            IconText.Foreground = SolidColorBrush.Parse(fg);
        }
        if (TitleText is not null)  TitleText.Foreground  = SolidColorBrush.Parse(fg);
        if (MessageText is not null) MessageText.Foreground = SolidColorBrush.Parse(fg);
    }

    private void ApplyText()
    {
        if (TitleText is not null)
        {
            TitleText.Text      = Title;
            TitleText.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (MessageText is not null) MessageText.Text = Message;
    }

    // ────────────────────────────────────────────────────
    //  이벤트
    // ────────────────────────────────────────────────────

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => IsOpen = false;
}
