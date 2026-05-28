using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using SaemDesk.Board.Models;
using SaemDesk.Board.Repositories;
using SaemDesk.Views.Dialogs;
using SaemDesk; // Settings 접근용
using BoardDb = SaemDesk.Board.BoardDatabase; // 모호성 해소

namespace SaemDesk.Views.Controls;

/// <summary>
/// 포스트잇 스타일 메모보드.
/// - Masonry 2열 레이아웃 (코드비하인드에서 LeftCol / RightCol 에 카드 배분).
/// - 제목: 카드 내 TextBox 직접 편집 → blur 시 DB 저장.
/// - 본문: HTML→Plain 미리보기 텍스트. 상세 편집 버튼 → PostEditDialog(Jodit).
/// - ShowFilter=False 이면 카테고리 칩 영역 숨김.
/// - FixedCategory 지정 시 해당 카테고리만 로드, 추가 시 카테고리 고정.
/// </summary>
public partial class MemoBoard : UserControl
{
    // ────────────────────────────────────────────────
    // StyledProperty
    // ────────────────────────────────────────────────

    public static readonly StyledProperty<bool> ShowFilterProperty =
        AvaloniaProperty.Register<MemoBoard, bool>(nameof(ShowFilter), defaultValue: true);

    public static readonly StyledProperty<string> FixedCategoryProperty =
        AvaloniaProperty.Register<MemoBoard, string>(nameof(FixedCategory), defaultValue: string.Empty);

    public static readonly StyledProperty<string> FixedSubjectProperty =
        AvaloniaProperty.Register<MemoBoard, string>(nameof(FixedSubject), defaultValue: string.Empty);

    public bool ShowFilter
    {
        get => GetValue(ShowFilterProperty);
        set => SetValue(ShowFilterProperty, value);
    }

    public string FixedCategory
    {
        get => GetValue(FixedCategoryProperty);
        set => SetValue(FixedCategoryProperty, value);
    }

    public string FixedSubject
    {
        get => GetValue(FixedSubjectProperty);
        set => SetValue(FixedSubjectProperty, value);
    }

    // ────────────────────────────────────────────────
    // 내부 상태
    // ────────────────────────────────────────────────

    private readonly List<Post> _items = new();
    private string _currentCategory = string.Empty;

    // 카드 UI 추적: Post.No → (카드 Border, 제목 TextBox)
    private readonly Dictionary<int, (Border card, TextBox titleBox)> _cardMap = new();

    // 칩 버튼 목록 (active 클래스 토글용)
    private readonly List<Button> _chips = new();

    // ────────────────────────────────────────────────
    // 생성자 / 초기화
    // ────────────────────────────────────────────────

    public MemoBoard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // ShowFilter 적용
        FilterPanel.IsVisible = ShowFilter;
        HeaderTitle.IsVisible = !ShowFilter;

        // FixedCategory 적용
        if (!string.IsNullOrEmpty(FixedCategory))
        {
            _currentCategory = FixedCategory;
            FilterPanel.IsVisible = false; // 카테고리 고정 시 필터 칩 불필요
        }

        // 칩 목록 수집
        _chips.AddRange(new[] { ChipAll, ChipLesson, ChipClass, ChipWork, ChipSelf });

        await ReloadAsync();
    }

    // ────────────────────────────────────────────────
    // 필터 칩
    // ────────────────────────────────────────────────

    private async void OnChipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        foreach (var chip in _chips)
            chip.Classes.Remove("active");
        btn.Classes.Add("active");

        _currentCategory = btn.Tag as string ?? string.Empty;
        await ReloadAsync();
    }

    // ────────────────────────────────────────────────
    // 추가
    // ────────────────────────────────────────────────

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        var seed = new Post
        {
            DateTime = DateTime.Now,
            User     = Settings.UserName.Value,
            Category = !string.IsNullOrEmpty(FixedCategory) ? FixedCategory
                     : !string.IsNullOrEmpty(_currentCategory) ? _currentCategory
                     : "개인",
        };
        await OpenEditAsync(seed, isNew: true);
    }

    // ────────────────────────────────────────────────
    // 편집 다이얼로그
    // ────────────────────────────────────────────────

    private async Task OpenEditAsync(Post post, bool isNew = false)
    {
        var owner = (Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null) return;

        var dlg = new MemoEditDialog();
        if (isNew)
        {
            string cat = !string.IsNullOrEmpty(FixedCategory) ? FixedCategory
                       : !string.IsNullOrEmpty(_currentCategory) ? _currentCategory
                       : post.Category;
            dlg.InitForNew(cat, FixedSubject);
        }
        else
        {
            dlg.InitForEdit(post);
        }

        await dlg.ShowDialog(owner);

        if (dlg.SavedPost is { } saved)
        {
            using var repo = new PostRepository(BoardDb.DbPath);
            if (isNew)
                await repo.CreateAsync(saved);
            else
                await repo.UpdateAsync(saved);
            await ReloadAsync();
        }
    }

    // ────────────────────────────────────────────────
    // 제목 인라인 저장 (blur)
    // ────────────────────────────────────────────────

    private async void OnTitleLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || tb.Tag is not Post post) return;
        string newTitle = tb.Text?.Trim() ?? string.Empty;
        if (newTitle == post.Title) return;

        post.Title = newTitle;
        try
        {
            using var repo = new PostRepository(BoardDb.DbPath);
            await repo.UpdateAsync(post);
        }
        catch { /* 무시 */ }
    }

    // ────────────────────────────────────────────────
    // 삭제
    // ────────────────────────────────────────────────

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Post post) return;
        try
        {
            using var repo = new PostRepository(BoardDb.DbPath);
            await repo.DeleteAsync(post.No);
            _items.Remove(post);
            _cardMap.Remove(post.No);
            RebuildColumns();
            EmptyState.IsVisible = _items.Count == 0;
        }
        catch { /* 무시 */ }
    }

    // ────────────────────────────────────────────────
    // 데이터 로드
    // ────────────────────────────────────────────────

    private async Task ReloadAsync()
    {
        try
        {
            LoadingBar.IsVisible = true;
            using var repo = new PostRepository(BoardDb.DbPath);
            var rows = await repo.GetForMemoAsync(
                category: _currentCategory,
                subject:  FixedSubject);

            _items.Clear();
            _items.AddRange(rows);

            _cardMap.Clear();
            RebuildColumns();

            EmptyState.IsVisible = _items.Count == 0;
        }
        catch
        {
            EmptyState.IsVisible = true;
        }
        finally
        {
            LoadingBar.IsVisible = false;
        }
    }

    // ────────────────────────────────────────────────
    // Masonry 2열 재구성
    // ────────────────────────────────────────────────

    private void RebuildColumns()
    {
        LeftCol.Children.Clear();
        RightCol.Children.Clear();

        for (int i = 0; i < _items.Count; i++)
        {
            var card = BuildCard(_items[i]);
            if (i % 2 == 0)
                LeftCol.Children.Add(card);
            else
                RightCol.Children.Add(card);
        }
    }

    // ────────────────────────────────────────────────
    // 포스트잇 카드 생성
    // ────────────────────────────────────────────────

    private Border BuildCard(Post post)
    {
        // 카테고리별 배경/줄 색상
        var bgBrush     = CategoryBgConverter.GetBrush(post.Category);
        var stripeBrush = CategoryStripeConverter.GetBrush(post.Category);
        var badgeBrush  = CategoryColorConverter.GetBrush(post.Category);

        // 제목 TextBox
        var titleBox = new TextBox
        {
            Text            = post.Title,
            FontSize        = 13,
            FontWeight      = FontWeight.Medium,
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(0),
            PlaceholderText  = "제목",
            TextWrapping    = Avalonia.Media.TextWrapping.Wrap,
            AcceptsReturn   = false,
            Tag             = post,
        };
        titleBox.LostFocus += OnTitleLostFocus;

        // 본문 미리보기 (이미지 + 링크 + 텍스트)
        var bodyContent = SimpleHtmlRenderer.Render(post.Content);
        bodyContent.Margin = new Thickness(0, 4, 0, 0);
        bodyContent.IsVisible = !string.IsNullOrEmpty(post.Content);

        // 카테고리 배지
        var badge = new Border
        {
            Background    = badgeBrush,
            CornerRadius  = new CornerRadius(10),
            Padding       = new Thickness(6, 1),
            Child         = new TextBlock
            {
                Text       = post.Category,
                FontSize   = 10,
                Foreground = Brushes.White,
                FontWeight = FontWeight.Medium,
            },
        };

        // 날짜
        var dateBlock = new TextBlock
        {
            Text       = post.DateTime.ToString("MM/dd"),
            FontSize   = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        // 삭제 버튼
        var delBtn = new Button
        {
            Content         = "✕",
            FontSize        = 10,
            Padding         = new Thickness(3, 0),
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground      = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            Tag             = post,
        };
        ToolTip.SetTip(delBtn, "삭제");
        delBtn.Click += OnDeleteClick;

        // 편집 버튼
        var editBtn = new Button
        {
            Content         = "✏ 편집",
            FontSize        = 10,
            Padding         = new Thickness(6, 2),
            Background      = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            CornerRadius    = new CornerRadius(3),
            Foreground      = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            Tag             = post,
        };
        ToolTip.SetTip(editBtn, "상세 편집 (HTML)");
        editBtn.Click += async (_, _) => await OpenEditAsync(post);

        // 헤더 행: 배지 + 날짜 + 삭제
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin            = new Thickness(0, 0, 0, 6),
        };
        header.Children.Add(badge);
        Grid.SetColumn(badge, 0);
        header.Children.Add(dateBlock);
        Grid.SetColumn(dateBlock, 2);
        header.Children.Add(delBtn);
        Grid.SetColumn(delBtn, 3);

        // 푸터: 편집 버튼 우측 정렬
        var footer = new Panel
        {
            Margin = new Thickness(0, 6, 0, 0),
        };
        var footerStack = new StackPanel
        {
            Orientation           = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment   = Avalonia.Layout.HorizontalAlignment.Right,
        };
        footerStack.Children.Add(editBtn);
        footer.Children.Add(footerStack);

        // 카드 내부 콘텐츠
        var inner = new StackPanel
        {
            Margin = new Thickness(10, 8, 10, 8),
        };
        inner.Children.Add(header);
        inner.Children.Add(titleBox);
        inner.Children.Add(bodyContent);
        inner.Children.Add(footer);

        // 상단 줄 (카테고리 색)
        var stripe = new Border
        {
            Height     = 5,
            Background = stripeBrush,
        };

        // 카드 외부 Border
        var card = new Border
        {
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Background   = bgBrush,
            Tag          = post,
            Child        = new StackPanel
            {
                Children = { stripe, inner },
            },
        };

        _cardMap[post.No] = (card, titleBox);
        return card;
    }
}
