using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    // 반응형 열 — 카드 최소 폭 400 기준. 폭에 400짜리가 몇 장 들어가는지로 열 수 결정(최대 3열).
    private const double CardMinWidth = 400;
    private const int    MaxColumns   = 3;
    private int _columnCount = 1;

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

        // 폭 변화에 따라 열 개수 재계산
        ListScroll.SizeChanged += OnListSizeChanged;
        _columnCount = ComputeColumnCount(ListScroll.Bounds.Width);

        await ReloadAsync();
    }

    private void OnListSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        int cols = ComputeColumnCount(e.NewSize.Width);
        if (cols == _columnCount) return;
        _columnCount = cols;
        RebuildColumns();
    }

    // 사용 가능한 폭 ÷ 400 (최소 1열, 최대 3열).
    private static int ComputeColumnCount(double width)
        => Math.Clamp((int)(width / CardMinWidth), 1, MaxColumns);

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
            // 확인(완료) 체크된 메모는 표시하지 않는다.
            _items.AddRange(rows.Where(r => !r.IsCompleted));

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
        int cols = Math.Max(1, _columnCount);

        // 열 정의: "*" 사이에 너비 8 간격. 예) 3열 → "*,8,*,8,*"
        ColumnsGrid.Children.Clear();
        ColumnsGrid.ColumnDefinitions = new ColumnDefinitions(
            string.Join(",8,", Enumerable.Repeat("*", cols)));

        var panels = new StackPanel[cols];
        for (int i = 0; i < cols; i++)
        {
            var sp = new StackPanel { Spacing = 8 };
            Grid.SetColumn(sp, i * 2); // 별(*) 열은 0,2,4… (홀수 인덱스는 간격)
            ColumnsGrid.Children.Add(sp);
            panels[i] = sp;
        }

        // 카드를 열에 순환 배치
        for (int idx = 0; idx < _items.Count; idx++)
            panels[idx % cols].Children.Add(BuildCard(_items[idx]));
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

        // ① 확인 체크 — 체크 시 메모를 완료 처리하고 숨긴다.
        var checkBox = new CheckBox
        {
            IsChecked         = post.IsCompleted,
            MinWidth          = 0,
            Padding           = new Thickness(0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Tag               = post,
        };
        ToolTip.SetTip(checkBox, "확인 — 체크하면 목록에서 숨겨집니다");
        checkBox.IsCheckedChanged += OnCheckChanged;

        // ② 카테고리 배지
        var badge = new Border
        {
            Background         = badgeBrush,
            CornerRadius       = new CornerRadius(8),
            Padding            = new Thickness(7, 1),
            Margin             = new Thickness(6, 0, 0, 0),
            VerticalAlignment  = Avalonia.Layout.VerticalAlignment.Center,
            Child              = new TextBlock
            {
                Text              = post.Category,
                FontSize          = 10,
                Foreground        = Brushes.White,
                FontWeight        = FontWeight.Medium,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        };

        // ③ 제목 (인라인 편집)
        var titleBox = new TextBox
        {
            Text              = post.Title,
            FontSize          = 13,
            FontWeight        = FontWeight.Medium,
            Background        = Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Padding           = new Thickness(0),
            Margin            = new Thickness(6, 0, 0, 0),
            PlaceholderText   = "제목",
            TextWrapping      = Avalonia.Media.TextWrapping.NoWrap,
            AcceptsReturn     = false,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Tag               = post,
        };
        titleBox.LostFocus += OnTitleLostFocus;

        // ④ 날짜
        var dateBlock = new TextBlock
        {
            Text              = post.DateTime.ToString("MM/dd"),
            FontSize          = 10,
            Foreground        = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            Margin            = new Thickness(6, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        // ⑤ 편집 버튼
        var editBtn = new Button
        {
            Content           = "✏",
            FontSize          = 11,
            Padding           = new Thickness(5, 1),
            Margin            = new Thickness(4, 0, 0, 0),
            Background        = Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Foreground        = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Tag               = post,
        };
        ToolTip.SetTip(editBtn, "상세 편집 (HTML)");
        editBtn.Click += async (_, _) => await OpenEditAsync(post);

        // ⑥ 삭제 버튼
        var delBtn = new Button
        {
            Content           = "✕",
            FontSize          = 11,
            Padding           = new Thickness(5, 1),
            Background        = Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Foreground        = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Tag               = post,
        };
        ToolTip.SetTip(delBtn, "삭제");
        delBtn.Click += OnDeleteClick;

        // 헤더 행: 확인체크 / 카테고리 / 제목 / 날짜 / 편집 / 삭제
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
        };
        header.Children.Add(checkBox);  Grid.SetColumn(checkBox, 0);
        header.Children.Add(badge);     Grid.SetColumn(badge, 1);
        header.Children.Add(titleBox);  Grid.SetColumn(titleBox, 2);
        header.Children.Add(dateBlock); Grid.SetColumn(dateBlock, 3);
        header.Children.Add(editBtn);   Grid.SetColumn(editBtn, 4);
        header.Children.Add(delBtn);    Grid.SetColumn(delBtn, 5);

        // 본문 미리보기 (이미지 + 링크 + 텍스트)
        var bodyContent = SimpleHtmlRenderer.Render(post.Content);
        bodyContent.Margin = new Thickness(0, 4, 0, 0);
        bodyContent.IsVisible = !string.IsNullOrEmpty(post.Content);

        // 카드 내부 콘텐츠
        var inner = new StackPanel
        {
            Margin = new Thickness(10, 6, 8, 8),
        };
        inner.Children.Add(header);
        inner.Children.Add(bodyContent);

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

    // ────────────────────────────────────────────────
    // 확인 체크 → 완료 처리 후 숨김
    // ────────────────────────────────────────────────

    private async void OnCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not Post post) return;
        if (cb.IsChecked != true || post.IsCompleted) return;

        post.IsCompleted = true;
        try
        {
            using var repo = new PostRepository(BoardDb.DbPath);
            await repo.UpdateIsCompletedAsync(post.No, true);
        }
        catch { /* 무시 */ }

        _items.Remove(post);
        _cardMap.Remove(post.No);
        RebuildColumns();
        EmptyState.IsVisible = _items.Count == 0;
    }
}
