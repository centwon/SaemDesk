using Avalonia;
using Avalonia.Controls;

namespace SaemDesk.HtmlEditor;

public partial class HtmlEditorControl : UserControl
{
    /// <summary>HTML 내용 (양방향 바인딩 가능).</summary>
    public static readonly StyledProperty<string> HtmlProperty =
        AvaloniaProperty.Register<HtmlEditorControl, string>(nameof(Html), defaultValue: "");

    public string Html
    {
        get => GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    public HtmlEditorControl()
    {
        InitializeComponent();
        Toolbar.Bind(EditArea);

        EditArea.DocumentChanged += OnDocumentChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HtmlProperty && !_internalUpdate)
        {
            string html = change.GetNewValue<string>() ?? "";
            var doc = HtmlParser.Parse(html);
            EditArea.LoadDocument(doc);
        }
    }

    private bool _internalUpdate;

    private void OnDocumentChanged()
    {
        _internalUpdate = true;
        try
        {
            Html = HtmlSerializer.Serialize(EditArea.Document);
        }
        finally
        {
            _internalUpdate = false;
        }
    }
}
