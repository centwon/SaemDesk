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
using SaemDesk.Helpers;
using SaemDesk.Services;
using SaemDesk.Views.Dialogs;

namespace SaemDesk.Board.Views.Pages;

/// <summary>
/// 게시글 작성/수정 페이지 — NewSchool PostEditPage 이식 (Frame → 이벤트).
/// RichEditorView + 카테고리/주제 ComboBox + 첨부파일.
/// </summary>
public partial class PostEditPage : UserControl
{
    private Post?                _post;
    private bool                 _isEdit;
    private PostEditPageParameter? _param;
    private List<string>         _allCategories = new();
    private List<string>         _allSubjects   = new();
    private string               _originalCategory = "";

    // 콤보박스 마지막에 넣는 "직접 추가" 항목 + 재진입 가드
    private const string AddNewLabel = "＋ 직접 추가…";
    private bool _suppressCategoryChanged;
    private bool _suppressSubjectChanged;
    private int  _lastCategoryIndex = -1;
    private int  _lastSubjectIndex  = -1;

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
                if (_post.ContentArdx is { Length: > 0 } ardx)
                {
                    using var ms = new MemoryStream(ardx);
                    await ContentEditor.Editor.LoadPackageAsync(ms);
                }
                else
                {
                    ContentEditor.Editor.LoadHtml(_post.Content); // 폴백: 미변환 구 HTML/plaintext
                }

                SelectCategory(_post.Category);
                await LoadSubjectsAsync(_post.Category);
                SelectSubject(_post.Subject);

                var files = await svc.GetPostFilesByPostAsync(param.PostNo);
                FileListBox.LoadFiles(files, _post.Category);
            }
        }
        else
        {
            // 신규 모드 — 에디터 초기화
            _isEdit = false;
            PageTitle.Text = "새 글 쓰기";
            TxtTitle.Text      = string.Empty;
            ContentEditor.Editor.Clear();
            CBoxSubject.SelectedIndex = -1;
            FileListBox.LoadFiles(new List<PostFile>(), param?.DefaultCategory ?? string.Empty);
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
            foreach (var d in BoardDefaults.Categories)
                if (!_allCategories.Contains(d)) _allCategories.Add(d);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostEditPage] 카테고리: {ex.Message}"); }

        _suppressCategoryChanged = true;
        CBoxCategory.ItemsSource = BuildWithAddItem(_allCategories);
        _suppressCategoryChanged = false;
    }

    // 실제 항목 + 맨 끝 "직접 추가" 항목으로 콤보 ItemsSource 구성
    private static List<string> BuildWithAddItem(List<string> items)
        => new(items) { AddNewLabel };

    private async Task LoadSubjectsAsync(string category)
    {
        try
        {
            using var svc = BoardService.Create();
            var subs = await svc.GetSubjectsAsync(category);
            _allSubjects = subs.Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (BoardDefaults.Topics.TryGetValue(category, out var defaults))
                foreach (var t in defaults)
                    if (!_allSubjects.Contains(t)) _allSubjects.Insert(0, t);
        }
        catch (Exception ex) { Debug.WriteLine($"[PostEditPage] 주제: {ex.Message}"); }

        _suppressSubjectChanged = true;
        CBoxSubject.ItemsSource = BuildWithAddItem(_allSubjects);
        _suppressSubjectChanged = false;
        _lastSubjectIndex = -1;
    }

    private void SelectCategory(string category)
    {
        int idx = _allCategories.IndexOf(category);
        CBoxCategory.SelectedIndex = idx >= 0 ? idx : -1;
        if (idx < 0) CBoxCategory.Text = category;
        _lastCategoryIndex = CBoxCategory.SelectedIndex;
    }

    private void SelectSubject(string subject)
    {
        int idx = _allSubjects.IndexOf(subject);
        CBoxSubject.SelectedIndex = idx >= 0 ? idx : -1;
        if (idx < 0) CBoxSubject.Text = subject;
        _lastSubjectIndex = CBoxSubject.SelectedIndex;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────

    private async void OnCategoryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressCategoryChanged) return;
        if (CBoxCategory.SelectedItem is not string sel) return;

        // "직접 추가" 선택 → 새 카테고리 입력받아 목록에 추가하고 선택
        if (sel == AddNewLabel)
        {
            string? name = await DialogService.ShowInputAsync(
                "카테고리 추가", "새 카테고리 이름을 입력하세요.");

            if (string.IsNullOrWhiteSpace(name))
            {
                // 취소 → 이전 선택 복원
                _suppressCategoryChanged = true;
                CBoxCategory.SelectedIndex = _lastCategoryIndex;
                _suppressCategoryChanged = false;
                return;
            }

            if (!_allCategories.Contains(name)) _allCategories.Add(name);

            _suppressCategoryChanged = true;
            CBoxCategory.ItemsSource   = BuildWithAddItem(_allCategories);
            CBoxCategory.SelectedIndex = _allCategories.IndexOf(name);
            _suppressCategoryChanged = false;
            _lastCategoryIndex = CBoxCategory.SelectedIndex;

            if (_post is not null) _post.Category = name;
            FileListBox.Category = name;
            await LoadSubjectsAsync(name);
            return;
        }

        _lastCategoryIndex = CBoxCategory.SelectedIndex;
        if (_post is not null) _post.Category = sel;
        FileListBox.Category = sel;
        await LoadSubjectsAsync(sel);
    }

    private async void OnSubjectChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSubjectChanged) return;
        if (CBoxSubject.SelectedItem is not string sel) return;

        // "직접 추가" 선택 → 새 주제 입력받아 목록에 추가하고 선택
        if (sel == AddNewLabel)
        {
            string? name = await DialogService.ShowInputAsync(
                "주제 추가", "새 주제 이름을 입력하세요.");

            if (string.IsNullOrWhiteSpace(name))
            {
                _suppressSubjectChanged = true;
                CBoxSubject.SelectedIndex = _lastSubjectIndex;
                _suppressSubjectChanged = false;
                return;
            }

            if (!_allSubjects.Contains(name)) _allSubjects.Add(name);

            _suppressSubjectChanged = true;
            CBoxSubject.ItemsSource   = BuildWithAddItem(_allSubjects);
            CBoxSubject.SelectedIndex = _allSubjects.IndexOf(name);
            _suppressSubjectChanged = false;
            _lastSubjectIndex = CBoxSubject.SelectedIndex;
            return;
        }

        _lastSubjectIndex = CBoxSubject.SelectedIndex;
    }

    private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        => Cancelled?.Invoke(this, EventArgs.Empty);

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text)) { Debug.WriteLine("제목 없음"); return; }

        BtnSave.IsEnabled = false;
        try
        {
            if (_post is null) return;

            _post.Title    = TxtTitle.Text ?? "";

            // 리치 콘텐츠 정본은 ardx BLOB, 검색용은 plaintext.
            using (var ms = new MemoryStream())
            {
                await ContentEditor.Editor.SavePackageAsync(ms);
                _post.ContentArdx = ms.ToArray();
            }
            _post.Content = ContentEditor.Editor.Document is { } doc ? RichContent.PlainText(doc) : string.Empty;

            if (string.IsNullOrWhiteSpace(_post.Content) && ContentEditor.Editor.GetImageCount() == 0)
            { await DialogService.ShowInfoAsync("내용이 비어 있어 저장하지 않았습니다."); return; }
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
            if (postNo <= 0) { await DialogService.ShowInfoAsync("DB 저장에 실패했습니다 (postNo<=0)."); return; }

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
        catch (Exception ex)
        {
            Debug.WriteLine($"[PostEditPage] 저장 예외: {ex}");
            await DialogService.ShowInfoAsync($"저장 중 오류: {ex.GetType().Name}\n{ex.Message}");
        }
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

    // ── 명렬표 삽입 (NewSchool InsertRosterButton_Click 동등) ─────────────

    private async void BtnInsertRoster_Click(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var dlg = new RosterTableDialog();

        // 카테고리에 따라 기본 스코프 설정
        string cat = CBoxCategory.SelectedItem as string ?? "";
        dlg.SetScope(cat switch
        {
            "수업"   => "Course",
            "동아리" => "Club",
            _       => "Class",
        });

        await dlg.ShowDialog(owner);

        if (!dlg.IsSuccess || string.IsNullOrEmpty(dlg.GeneratedHtml)) return;

        // RichEditor에 HTML 표 삽입
        ContentEditor.Editor.InsertHtml(dlg.GeneratedHtml);

        // 제목이 비어 있으면 표 제목으로 자동 채움
        if (string.IsNullOrWhiteSpace(TxtTitle.Text) && !string.IsNullOrEmpty(dlg.TableTitle))
            TxtTitle.Text = dlg.TableTitle;
    }
}
