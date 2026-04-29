using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SaemDesk.Models;

/// <summary>
/// 게시판 게시글 모델.
/// NewSchool Board.Post 의 Avalonia AOT 호환 단순화 버전 (댓글·답글 트리·첨부 제거).
/// </summary>
public class Post : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _no = -1;
    public int No
    {
        get => _no;
        set { if (_no != value) { _no = value; OnPropertyChanged(); } }
    }

    private string _user = string.Empty;
    public string User
    {
        get => _user;
        set { if (_user != value) { _user = value ?? string.Empty; OnPropertyChanged(); } }
    }

    private DateTime _dateTime = DateTime.Now;
    public DateTime DateTime
    {
        get => _dateTime;
        set
        {
            if (_dateTime != value)
            {
                _dateTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DateTimeDisplay));
            }
        }
    }

    private string _category = string.Empty;
    public string Category
    {
        get => _category;
        set { if (_category != value) { _category = value ?? string.Empty; OnPropertyChanged(); } }
    }

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value ?? string.Empty; OnPropertyChanged(); } }
    }

    private string _content = string.Empty;
    /// <summary>Jodit가 생성한 HTML 본문.</summary>
    public string Content
    {
        get => _content;
        set { if (_content != value) { _content = value ?? string.Empty; OnPropertyChanged(); } }
    }

    private int _readCount;
    public int ReadCount
    {
        get => _readCount;
        set { if (_readCount != value) { _readCount = value; OnPropertyChanged(); } }
    }

    private bool _isCompleted;
    /// <summary>완료 여부 (메모/할일용).</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set { if (_isCompleted != value) { _isCompleted = value; OnPropertyChanged(); } }
    }

    public string DateTimeDisplay => DateTime.ToString("yyyy-MM-dd HH:mm");

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
