using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SaemDesk.Models;

namespace SaemDesk.Views.Controls;

/// <summary>
/// 학생 좌석 카드 — Avalonia 12 이식 (NewSchool PhotoCard 동등).
/// 토큰 기반 사진 비동기 로딩 경합 회피, 미사용/지정/미표시 토글 지원.
/// ListStudent 에서 드래그한 Enrollment 도 수신 가능.
/// </summary>
public partial class PhotoCard : UserControl
{
    public static readonly DataFormat<StudentCardData> StudentDragFormat =
        DataFormat.CreateInProcessFormat<StudentCardData>("saemdesk.studentcard");

    // ListStudent 드래그 포맷 참조 (수신 전용)
    public static readonly DataFormat<Enrollment> EnrollmentDragFormat =
        ListStudent.EnrollmentDragFormat;

    public int No  { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }

    private int _photoLoadToken = 0;

    private bool _isShowPhoto = false;
    public bool IsShowPhoto
    {
        get => _isShowPhoto;
        set => ShowPhoto(value);
    }

    private StudentCardData? _studentData;
    public StudentCardData? StudentData
    {
        get => _studentData;
        set
        {
            _studentData = value;
            SetStudent(value);
            OnStudentChanged();
        }
    }

    private bool _isUnUsed;
    public bool IsUnUsed
    {
        get => _isUnUsed;
        set
        {
            _isUnUsed = value;
            SetUnUsedStyle(value);
            UpdateMenuCheck(MenuSeatDisable, value, "미사용 좌석");
            UnUsedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _isHidden;
    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            _isHidden = value;
            Opacity = value ? 0.35 : 1.0;
            UpdateMenuCheck(MenuSeatHidden, value, "미표시 좌석");
            HiddenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _isFixed;
    public bool IsFixed
    {
        get => _isFixed;
        set
        {
            _isFixed = value;
            UpdateMenuCheck(MenuSeatFixed, value, "지정 좌석");
            FixedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double CardWidth
    {
        get => Width;
        set => SetSize(value, null);
    }

    public double CardHeight
    {
        get => Height;
        set => SetSize(null, value);
    }

    public event EventHandler<StudentCardEventArgs>? StudentChanged;
    public event EventHandler? UnUsedChanged;
    public event EventHandler? FixedChanged;
    public event EventHandler? HiddenChanged;

    /// <summary>
    /// 드래그가 아닌 단순 클릭으로 판정되었을 때 SeatsPage 가 구독하는 이벤트.
    /// PointerReleased 시점에 발화 — 드래그 중이면 발화하지 않는다.
    /// </summary>
    public event EventHandler? Clicked;

    // ── 드래그/클릭 판별용 내부 상태 ────────────────────
    private Point?                  _dragStartPoint;
    private PointerPressedEventArgs? _dragPressArgs;
    private bool                    _isDragging;
    private const double            DragThreshold = 4.0;

    public PhotoCard()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(InputElement.PointerPressedEvent,  OnPointerPressed,  RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerMovedEvent,    OnPointerMoved,    RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
    }

    #region Drag / Click 분리

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        _dragStartPoint = e.GetCurrentPoint(this).Position;
        _dragPressArgs  = e;
        _isDragging     = false;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStartPoint is null || _dragPressArgs is null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = null; _dragPressArgs = null;
            return;
        }

        var delta = e.GetCurrentPoint(this).Position - _dragStartPoint.Value;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;

        // 임계값 초과 → 드래그 확정
        if (StudentData == null) { _dragStartPoint = null; _dragPressArgs = null; return; }

        var pressArgs = _dragPressArgs;
        _isDragging     = true;
        _dragStartPoint = null;
        _dragPressArgs  = null;

        try
        {
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(StudentDragFormat, StudentData));
            await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch { /* 드래그 취소 무시 */ }
        finally { _isDragging = false; }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasClick = _dragStartPoint is not null && !_isDragging;
        _dragStartPoint = null;
        _dragPressArgs  = null;

        // 드래그로 이어지지 않은 경우에만 클릭 이벤트 발화
        if (wasClick) Clicked?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region DragOver — 수신 포맷 판별

    /// <summary>
    /// SeatsPage 의 Card_DragOver 에서 호출.
    /// StudentDragFormat(카드→카드) 또는 EnrollmentDragFormat(명렬→카드) 모두 허용.
    /// </summary>
    public static bool AcceptsDragData(DragEventArgs e)
        => e.DataTransfer.Contains(StudentDragFormat)
        || e.DataTransfer.Contains(EnrollmentDragFormat);

    #endregion

    #region Student Management

    /// <summary>학생 정보 교체 (이벤트 발생 없이)</summary>
    public void ReplaceStudent(StudentCardData? data)
    {
        _studentData = data;
        SetStudent(data);
    }

    private void SetStudent(StudentCardData? data)
    {
        int myToken = ++_photoLoadToken;

        if (data == null)
        {
            Photo.Source  = null;
            TBName.Text   = string.Empty;
            return;
        }

        TBName.Text = $"{data.Name}({data.Number})";

        if (_isShowPhoto)
            _ = LoadPhotoAsync(data.PhotoPath, myToken);
        else
            Photo.Source = null;
    }

    private void OnStudentChanged()
    {
        if (IsUnUsed) return;
        StudentChanged?.Invoke(this, new StudentCardEventArgs(Row, Col, _studentData));
    }

    #endregion

    #region Photo Management

    private void ShowPhoto(bool show)
    {
        _isShowPhoto = show;

        PhotoControl.IsVisible = show;
        if (Content is Border b && b.Child is Grid g && g.RowDefinitions.Count >= 1)
            g.RowDefinitions[0].Height = show ? GridLength.Auto : new GridLength(0);

        SetSize(Width, null);

        if (show && _studentData != null && !string.IsNullOrEmpty(_studentData.PhotoPath))
        {
            int myToken = ++_photoLoadToken;
            _ = LoadPhotoAsync(_studentData.PhotoPath, myToken);
        }
    }

    private async Task LoadPhotoAsync(string photoPath, int token)
    {
        if (token != _photoLoadToken) return;

        if (string.IsNullOrWhiteSpace(photoPath))
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (token == _photoLoadToken) Photo.Source = null; });
            return;
        }

        try
        {
            string fullPath = Path.IsPathRooted(photoPath)
                ? photoPath
                : Path.Combine(AppContext.BaseDirectory, photoPath);

            if (!File.Exists(fullPath))
            {
                await Dispatcher.UIThread.InvokeAsync(() => { if (token == _photoLoadToken) Photo.Source = null; });
                return;
            }

            Bitmap? bitmap = await Task.Run(() =>
            {
                if (token != _photoLoadToken) return null;
                using var fs = File.OpenRead(fullPath);
                return Bitmap.DecodeToWidth(fs, 400);
            });

            if (bitmap == null) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token != _photoLoadToken) return;
                Photo.Source = bitmap;
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (token == _photoLoadToken) Photo.Source = null; });
        }
    }

    #endregion

    #region Size Management

    private void SetSize(double? width, double? height)
    {
        if (width is double w && !double.IsNaN(w))
        {
            Width = w;
            PhotoControl.Width  = w - 2;
            PhotoControl.Height = IsShowPhoto ? PhotoControl.Width / 3 * 4 : 0;
            NameBox.Height      = PhotoControl.Width / 3;
            Height              = PhotoControl.Height + 2 + NameBox.Height;
            return;
        }

        if (height is double h && !double.IsNaN(h))
        {
            Height              = h;
            PhotoControl.Height = (h - 2) / 5 * 4;
            NameBox.Height      = PhotoControl.Height / 4;
            PhotoControl.Width  = PhotoControl.Height / 4 * 3;
            Width               = PhotoControl.Width + 2;
        }
    }

    #endregion

    #region Style

    private void SetUnUsedStyle(bool value)
    {
        if (value)
        {
            StudentData = null;
            Photo.Source = null;
            TBName.Text  = string.Empty;
            BrdrOutLine.BorderBrush = new SolidColorBrush(Colors.AntiqueWhite);
        }
        else
        {
            BrdrOutLine.BorderBrush = new SolidColorBrush(Colors.CornflowerBlue);
        }
    }

    #endregion

    #region Context Menu

    private static void UpdateMenuCheck(MenuItem? mi, bool isChecked, string baseText)
    {
        if (mi == null) return;
        mi.Header = isChecked ? "✓ " + baseText : baseText;
    }

    private void MenuSeatDisable_Click(object? sender, RoutedEventArgs e)
    {
        IsUnUsed = !IsUnUsed;
        BrdrOutLine.BorderBrush = IsUnUsed
            ? new SolidColorBrush(Colors.AntiqueWhite)
            : new SolidColorBrush(Colors.CornflowerBlue);
    }

    private void MenuSeatFixed_Click(object? sender, RoutedEventArgs e)
    {
        if (StudentData is null) { IsFixed = false; return; }
        IsFixed = !IsFixed;
    }

    private void MenuSeatHidden_Click(object? sender, RoutedEventArgs e) => IsHidden = !IsHidden;

    #endregion
}
