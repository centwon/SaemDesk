using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaemDesk.Board.Models;
using SaemDesk.Board.Services;

namespace SaemDesk.Board.Views.Pages;

/// <summary>
/// 게시글 작성/수정 페이지 — NewSchool PostEditPage 이식 (Frame → 이벤트).
/// JoditEditor + 카테고리/주제 ComboBox + 첨부파일.
/// </summary>
public partial class PostEditPage : UserControl
{
    private Post?                _post;
    private bool                 _isEdit;
    private PostEditPageParameter? _param;
    private List<string>         _allCategories = new();
    private List<string>         _allSubjects   = new();
    private string               _originalCategory = "";

    // 기본 카테고리 목록
    private static readonly List<string> DefaultCategories =
        new() { "업무", "수업", "학급", "동아리", "개인", "기타" };

    // 카테고리별 기본 주제
    private static readonly Dictionary<string, List<string>> DefaultTopics = new()
    {
        ["학급"] = new() { "통계", "학급 자료", "학생 자료", "학급 안내" },
        ["수업"] = new() { "통계", "수업 자료", "과제" },
        ["동아리"] = new() { "통계", "동아리 자료", "활동 안내" },
    };

    // ── 이벤트 ────────────────────────────────────────────
    public event EventHandler?                       Saved;
    public event EventHandler?                       Cancelled;

    public PostEditPage()
    {
        InitializeComponent();
    }

    // ── 공개 초기화 ───────────────────────────────────────

    public async Task InitAsync(PostEditPageParameter? param = null)
    {
        _param = param;
        await LoadCategoriesAsync();

        if (param is { PostNo: > 0 })
        {
            _isEdit = true;
            PageTitle.Text = "게시글 수정";

            using var svc = BoardService.Create();
            _post = await svc.GetPostAsync(param.PostNo, incrementReadCount: false);

            if (_post is not null)
            {
                _originalCategory = _post.Category;
                TxtTitle.Text = _post.Title;
                ContentEditor.Text = _post.Content;

                SelectCategory(_post.Category);
                await LoadSubjectsAsync(_post.Category);
                SelectSubject(_post.Subject);

                var files = await svc.GetPostFilesByPostAsync(param.PostNo);
                FileListBox.LoadFiles(files, _post.Category);
            }
        }
        else
        {
            // 신규 모드
            _isEdit = false;
            PageTitle.Text = "새 글 쓰기";
            _post = new Post
            {
                DateTime = DateTime.Now,
                User     = Settings.UserName.Value.Length > 0 ? Settings.UserName.Value : "익명",
            };

            if (param is not null)
            {
                if (!string.IsNullOrEmpty(param.DefaultCategory))
                {
                    _post.Category = param.DefaultCategory;
                    SelectCategory(param.DefaultCategory);
                    await LoadSubjectsAsync(param.DefaultCategory);
                }
                if (!string.IsNullOrEmpty(param.DefaultSubject))
                {
                    _post.Subject = param.DefaultSubject;
                    SelectSubject(param.DefaultSubject);
                }
            }
        }

        // 카테고리 변경 허용 여부
        if (param is not null)
            CBoxCategory.IsEnabled = param.AllowCategoryChange;
    }

    // ── 카테고리/주제 로드 ────────────────────────────────

    private async Task LoadCategoriesAsync()
    {
        try
        {
            using var svc = BoardService.Create();
            var cats = await svc.GetCategoriesAsync();
            _allCategories = cats.Where(c => !string.IsNullOrEmpty(c)).ToList();
            foreach (var d in DefaultCategories)
                if (!_allCategories.Contains(d)) _allCategories.Add(d);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostEditPage] 카테고리: {ex.Message}"); }

        CBoxCategory.ItemsSource = _allCategories;
    }

    private async Task LoadSubjectsAsync(string category)
    {
        try
        {
            using var svc = BoardService.Create();
            var subs = await svc.GetSubjectsAsync(category);
            _allSubjects = subs.Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (DefaultTopics.TryGetValue(category, out var defaults))
                foreach (var t in defaults)
                    if (!_allSubjects.Contains(t)) _allSubjects.Insert(0, t);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostEditPage] 주제: {ex.Message}"); }

        CBoxSubject.ItemsSource = _allSubjects;
    }

    private void SelectCategory(string category)
    {
        int idx = _allCategories.IndexOf(category);
        CBoxCategory.SelectedIndex = idx >= 0 ? idx : -1;
        if (idx < 0) CBoxCategory.Text = category;
    }

    private void SelectSubject(string subject)
    {
        int idx = _allSubjects.IndexOf(subject);
        CBoxSubject.SelectedIndex = idx >= 0 ? idx : -1;
        if (idx < 0) CBoxSubject.Text = subject;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CBoxCategory.SelectedItem is string cat)
        {
            if (_post is not null) _post.Category = cat;
            FileListBox.Category = cat;
            await LoadSubjectsAsync(cat);
        }
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        => Cancelled?.Invoke(this, EventArgs.Empty);

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        if (!Validate()) return;

        BtnSave.IsEnabled = false;
        try
        {
            if (_post is null) return;

            _post.Title    = TxtTitle.Text ?? "";
            _post.Content  = ContentEditor.Text;
            _post.DateTime = DateTime.Now;

            // 카테고리
            if (CBoxCategory.SelectedItem is string cat)      _post.Category = cat;
            else if (!string.IsNullOrWhiteSpace(CBoxCategory.Text)) _post.Category = CBoxCategory.Text.Trim();

            // 주제
            if (CBoxSubject.SelectedItem is string sub)       _post.Subject = sub;
            else if (!string.IsNullOrWhiteSpace(CBoxSubject.Text))  _post.Subject = CBoxSubject.Text.Trim();

            using var svc = BoardService.Create();

            // 카테고리 변경 시 파일 이동
            if (_isEdit && !string.IsNullOrEmpty(_originalCategory) && _originalCategory != _post.Category)
                await MoveFilesAsync(_originalCategory, _post.Category, _post.No, svc);

            int postNo = await svc.SavePostAsync(_post);
            if (postNo <= 0) return;

            // 삭제할 파일 처리
            foreach (var del in FileListBox.FilesToDelete)
                await svc.DeletePostFileAsync(del.No, _post.Category);

            // 신규 파일 저장
            foreach (var box in FileListBox.FileBoxes)
            {
                if (string.IsNullOrEmpty(box.OrgFilePath)) continue;
                var pf = await SaveFileAsync(box.OrgFilePath, postNo, _post.Category);
                if (pf is not null) await svc.AddPostFileAsync(pf);
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostEditPage] 저장: {ex.Message}"); }
        finally { BtnSave.IsEnabled = true; }
    }

    // ── 파일 관련 헬퍼 ────────────────────────────────────

    private async Task MoveFilesAsync(string oldCat, string newCat, int postNo, BoardService svc)
    {
        try
        {
            BoardDatabase.EnsureCategoryDirectory(newCat);
            var files = await svc.GetPostFilesByPostAsync(postNo);
            foreach (var f in files)
            {
                var oldPath = BoardDatabase.GetFilePath(f.FileName, oldCat);
                var newPath = BoardDatabase.GetFilePath(f.FileName, newCat);
                if (File.Exists(oldPath) && !File.Exists(newPath))
                    File.Move(oldPath, newPath);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[PostEditPage] 파일 이동: {ex.Message}"); }
    }

    private static async Task<PostFile?> SaveFileAsync(string srcPath, int postNo, string category)
    {
        try
        {
            BoardDatabase.EnsureCategoryDirectory(category);
            var ts   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var ext  = Path.GetExtension(srcPath);
            var name = Path.GetFileNameWithoutExtension(srcPath);
            var dst  = BoardDatabase.GetFilePath($"{ts}_{name}{ext}", category);
            await Task.Run(() => File.Copy(srcPath, dst, true));
            return new PostFile
            {
                Post     = postNo,
                FileName = Path.GetFileName(dst),
                FileSize = new FileInfo(dst).Length,
                DateTime = DateTime.Now,
            };
        }
        catch (Exception ex) { Debug.WriteLine($"[PostEditPage] 파일 저장: {ex.Message}"); return null; }
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text))        { Debug.WriteLine("제목 없음"); return false; }
        if (string.IsNullOrWhiteSpace(ContentEditor.Text))   { Debug.WriteLine("내용 없음"); return false; }
        return true;
    }
}
