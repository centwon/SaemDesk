using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SaemDesk.Board.Models;

/// <summary>Post 모델 — NewSchool.Board.Post 에서 WinUI3 의존성 제거.</summary>
public class Post : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int    _no       = -1;
    private string _user     = string.Empty;
    private DateTime _dateTime = DateTime.Now;
    private string _category = string.Empty;
    private string _subject  = string.Empty;
    private string _title    = string.Empty;
    private string _content  = string.Empty;
    private int    _refNo;
    private int    _replyOrder;
    private int    _depth;
    private int    _readCount;
    private bool   _hasFile;
    private bool   _hasComment;
    private bool   _isCompleted;

    public int    No          { get => _no;          set => SetField(ref _no,          value); }
    public string User        { get => _user;        set => SetField(ref _user,        value); }
    public DateTime DateTime  { get => _dateTime;    set => SetField(ref _dateTime,    value); }
    public string Category    { get => _category;    set => SetField(ref _category,    value); }
    public string Subject     { get => _subject;     set => SetField(ref _subject,     value); }
    public string Title       { get => _title;       set => SetField(ref _title,       value); }
    /// <summary>검색·미리보기용 plaintext (구버전: HTML). 실제 리치 콘텐츠는 <see cref="ContentArdx"/>.</summary>
    public string Content     { get => _content;     set => SetField(ref _content,     value); }
    /// <summary>리치 콘텐츠 정본 — AvaloniaRichEditor ardx 패키지(BLOB). 단일 글 조회 시에만 로드.</summary>
    public byte[]? ContentArdx { get; set; }
    public int    RefNo       { get => _refNo;       set => SetField(ref _refNo,       value); }
    public int    ReplyOrder  { get => _replyOrder;  set => SetField(ref _replyOrder,  value); }
    public int    Depth       { get => _depth;       set => SetField(ref _depth,       value); }
    public int    ReadCount   { get => _readCount;   set => SetField(ref _readCount,   value); }
    public bool   HasFile     { get => _hasFile;     set { SetField(ref _hasFile,     value); OnPropertyChanged(nameof(FileIconVisible));    } }
    public bool   HasComment  { get => _hasComment;  set { SetField(ref _hasComment,  value); OnPropertyChanged(nameof(CommentIconVisible)); } }
    public bool   IsCompleted { get => _isCompleted; set => SetField(ref _isCompleted, value); }

    // UI 바인딩용
    public bool   FileIconVisible    => HasFile;
    public bool   CommentIconVisible => HasComment;
    public string DateTimeDisplay    => DateTime.ToString("M/d HH:mm");

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
