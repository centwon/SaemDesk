using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SaemDesk.Board.Models;

namespace SaemDesk.Board.Views.Controls;

/// <summary>
/// 게시글 첨부파일 목록 컨트롤.
/// 편집 모드: [☐] [파일명 (12KB)] — 체크박스 표시, − 버튼으로 선택 삭제
/// 읽기 모드: [파일명 (12KB)]     — 체크박스 숨김, 버튼 클릭으로 파일 열기
/// </summary>
public partial class PostFileListBox : UserControl
{
    public ObservableCollection<FileBoxItem> FileBoxes     { get; } = new();
    public ObservableCollection<PostFile>    FilesToDelete { get; } = new();
    public event EventHandler? FileBoxesChanged;

    private string _category  = string.Empty;
    private bool   _isReadOnly;

    public string Category   { get => _category;   set => _category = value; }
    public bool   IsReadOnly { get => _isReadOnly; set { _isReadOnly = value; UpdateReadOnlyState(); } }
    public int    FileCount  => FileBoxes.Count;

    public PostFileListBox()
    {
        InitializeComponent();
        FileItems.ItemsSource = FileBoxes;

        DragDrop.SetAllowDrop(DropArea, true);
        DropArea.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropArea.AddHandler(DragDrop.DropEvent,     OnDrop);
    }

    public void LoadFiles(List<PostFile> files, string category, bool readOnly = false)
    {
        _category  = category;
        IsReadOnly = readOnly;
        FileBoxes.Clear();
        foreach (var f in files)
            FileBoxes.Add(new FileBoxItem { PostFile = f, Category = category, ShowCheckBox = !readOnly });
        FileBoxesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── 헤더 버튼 ─────────────────────────────────────────

    private async void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        if (_isReadOnly) return;
        await AddFilesAsync();
    }

    private void BtnRemove_Click(object? sender, RoutedEventArgs e)
    {
        if (_isReadOnly) return;
        for (int i = FileBoxes.Count - 1; i >= 0; i--)
        {
            if (!FileBoxes[i].IsSelected) continue;
            if (string.IsNullOrEmpty(FileBoxes[i].OrgFilePath) && FileBoxes[i].PostFile is not null)
                FilesToDelete.Add(FileBoxes[i].PostFile!);
            FileBoxes.RemoveAt(i);
        }
        FileBoxesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BtnFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path = Path.Combine(BoardDatabase.DataDir, _category);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Debug.WriteLine($"[PostFileListBox] 폴더: {ex.Message}"); }
    }

    // ── 파일 버튼 클릭 → 열기 ────────────────────────────

    private void OnFileButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is FileBoxItem item && item.PostFile is not null)
            OpenFile(item.PostFile);
    }

    private void OpenFile(PostFile pf)
    {
        try
        {
            string p = BoardDatabase.GetFilePath(pf.FileName, _category);
            if (File.Exists(p))
                Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
        }
        catch (Exception ex) { Debug.WriteLine($"[PostFileListBox] 열기: {ex.Message}"); }
    }

    // ── 드롭 ─────────────────────────────────────────────

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool hasFiles = e.DataTransfer.Formats.Any(f => f == DataFormat.File);
        e.DragEffects = (!_isReadOnly && hasFiles)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_isReadOnly) return;
        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;
        foreach (var f in files)
            if (f is IStorageFile sf) await AddStorageFileAsync(sf);
    }

    // ── 파일 추가 ─────────────────────────────────────────

    private async Task AddFilesAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { AllowMultiple = true, Title = "파일 선택" });
        foreach (var f in files)
            await AddStorageFileAsync(f);
    }

    private async Task AddStorageFileAsync(IStorageFile file)
    {
        var props = await file.GetBasicPropertiesAsync();
        FileBoxes.Add(new FileBoxItem
        {
            PostFile     = new PostFile { FileName = file.Name, FileSize = (long)(props.Size ?? 0), DateTime = DateTime.Now },
            Category     = _category,
            OrgFilePath  = file.Path.LocalPath,
            ShowCheckBox = !_isReadOnly,
        });
        FileBoxesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateReadOnlyState()
    {
        BtnAdd.IsVisible    = !_isReadOnly;
        BtnRemove.IsVisible = !_isReadOnly;
        BtnFolder.IsVisible = !_isReadOnly;
        HeaderText.IsVisible = !_isReadOnly;
        foreach (var item in FileBoxes)
            item.ShowCheckBox = !_isReadOnly;
    }
}

public class FileBoxItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public PostFile? PostFile    { get; set; }
    public string    Category    { get; set; } = string.Empty;
    public string    OrgFilePath { get; set; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _showCheckBox = true;
    public bool ShowCheckBox
    {
        get => _showCheckBox;
        set => SetProperty(ref _showCheckBox, value);
    }

    /// <summary>버튼 표시 텍스트: 파일명 (용량)</summary>
    public string Label => PostFile is null
        ? string.Empty
        : $"{PostFile.FileName}  ({PostFile.FileSizeDisplay})";
}
