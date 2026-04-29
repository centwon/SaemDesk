using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SaemDesk.Board.Models;

/// <summary>Comment 모델 — NewSchool.Board.Comment 에서 WinUI3 의존성 제거.</summary>
public class Comment : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int      _no;
    private int      _post;
    private string   _user      = string.Empty;
    private DateTime _dateTime  = DateTime.Now;
    private int      _replyOrder;
    private string   _content   = string.Empty;
    private bool     _hasFile;
    private string   _fileName  = string.Empty;
    private int      _fileSize;

    public int      No          { get => _no;          set => SetField(ref _no,          value); }
    public int      Post        { get => _post;        set => SetField(ref _post,        value); }
    public string   User        { get => _user;        set => SetField(ref _user,        value); }
    public DateTime DateTime    { get => _dateTime;    set => SetField(ref _dateTime,    value); }
    public int      ReplyOrder  { get => _replyOrder;  set => SetField(ref _replyOrder,  value); }
    public string   Content     { get => _content;     set => SetField(ref _content,     value); }
    public bool     HasFile     { get => _hasFile;     set { SetField(ref _hasFile,  value); OnPropertyChanged(nameof(FileIconVisible)); } }
    public string   FileName    { get => _fileName;    set => SetField(ref _fileName,    value); }
    public int      FileSize    { get => _fileSize;    set => SetField(ref _fileSize,    value); }

    public bool FileIconVisible => HasFile;

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
