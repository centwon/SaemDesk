# SaemDesk 마이그레이션 계획서

> NewSchool (WinUI3 + .NET 10) → SaemDesk (Avalonia 12 + .NET 10 + Native AOT)  
> 작성일: 2026-04-22 / Windows 전용 우선, 차후 크로스플랫폼 확장 예정

---

## 1. 프로젝트 개요

### 목표
- WinUI3 의존성 완전 제거
- Avalonia 12 기반 크로스플랫폼 UI 레이어
- Native AOT 퍼블리시 지원 (빠른 시작, 작은 배포 파일)
- Windows 전용 코드는 인터페이스로 격리 → 차후 크로스플랫폼 전환 용이

### 현재 스택 (NewSchool)
| 항목 | 기술 |
|------|------|
| UI | WinUI 3 (83개 XAML) |
| Framework | .NET 10, Windows 10.0.19041+ |
| DB | SQLite 3개 (ADO.NET 직접) |
| 외부 서비스 | Google Calendar REST API, NEIS HTTP API |
| 에디터 | WebView2 + Jodit (리치 텍스트) |
| 출력 | QuestPDF, MiniExcel, HTML |
| 암호화 | DPAPI (Windows 전용) |
| IME | imm32.dll P/Invoke |

### 목표 스택 (SaemDesk)
| 항목 | 기술 |
|------|------|
| UI | Avalonia 12 + CommunityToolkit.Mvvm |
| Framework | .NET 10 |
| DB | SQLite 3개 (ADO.NET 직접, 변경 없음) |
| 외부 서비스 | 동일 (변경 없음) |
| 에디터 | Avalonia.WebView + Jodit |
| 출력 | QuestPDF, MiniExcel, HTML (변경 없음) |
| 암호화 | ICryptoService → Windows: DPAPI, 미래: AES/OS Keychain |
| IME | IKoreanImeService → Windows: imm32.dll P/Invoke |
| AOT | PublishAot=true, CompiledBindings=true |

---

## 2. 핵심 교체 항목

| 기존 (WinUI3) | 대체 (Avalonia 12) | 비고 |
|--------------|-------------------|------|
| `Windows.Storage.Pickers` | `IStorageProvider` (Avalonia 내장) | |
| `Microsoft.UI.Xaml.Controls.WebView2` | `Avalonia.WebView` | Jodit 에디터 유지 |
| `ProtectedData` (DPAPI) | `ICryptoService` 인터페이스 분리 | Windows 구현체 유지 |
| `DispatcherQueue` | `Dispatcher.UIThread` | |
| `ContentDialog` | 커스텀 다이얼로그 기반 클래스 | |
| `AppWindow`, `WindowNative` | `ToplevelHandle` (Avalonia 12) | |
| `imm32.dll` P/Invoke | `IKoreanImeService` 인터페이스 분리 | Windows 구현체 유지 |
| WinUI Print API | QuestPDF (이미 사용 중) | 변경 없음 |
| `x:Bind` (WinUI) | CompiledBinding (Avalonia) | |
| `ObservableObject` (WinUI) | `CommunityToolkit.Mvvm` | 이미 사용 중 |

---

## 3. 폴더 구조

```
SaemDesk/
├── Assets/
│   ├── Fonts/
│   ├── Icons/
│   └── Jodit/                        # editor.html, jodit.fat.min.js/css, purify.min.js
│
├── Models/                            # Phase 1 — 변경 없음
│   └── (NewSchool Models 그대로 복사)
│
├── Repositories/                      # Phase 1 — 변경 없음
│   └── (NewSchool Repositories 그대로 복사)
│
├── Services/                          # Phase 1 (대부분 변경 없음)
│   ├── Platform/                      # Phase 2 — 플랫폼 추상화
│   │   ├── ICryptoService.cs
│   │   ├── IKoreanImeService.cs
│   │   ├── IFilePickerService.cs
│   │   └── Windows/
│   │       ├── WindowsDpApiCryptoService.cs
│   │       ├── WindowsKoreanImeService.cs
│   │       └── WindowsFilePickerService.cs
│   └── (나머지 Services 그대로 복사)
│
├── Google/                            # Phase 1 — 변경 없음
├── Scheduler/                         # Phase 5 — UI 있음
├── Board/                             # Phase 5 — UI 있음
├── Helpers/                           # Phase 1 — 변경 없음
├── Logging/                           # Phase 1 — 변경 없음
├── Caching/                           # Phase 1 — 변경 없음
│
├── ViewModels/                        # Phase 3~5
├── Views/                             # Phase 3~5 (기존 Pages/Dialogs/Controls)
│   ├── Pages/
│   ├── Dialogs/
│   └── Controls/
│
├── Converters/                        # Phase 3
├── Collections/                       # Phase 1 — OptimizedObservableCollection
│
├── App.axaml / App.axaml.cs          # Phase 3
├── MainWindow.axaml / .cs            # Phase 3
├── Settings.cs                        # Phase 2
├── DatabaseInitializer.cs            # Phase 1
├── TrimmerRoots.xml                   # AOT
└── SaemDesk.csproj
```

---

## 4. NuGet 패키지 계획

```xml
<!-- UI -->
<PackageReference Include="Avalonia" Version="12.0.1" />
<PackageReference Include="Avalonia.Desktop" Version="12.0.1" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.1" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="12.0.1" />
<PackageReference Include="Avalonia.WebView.Desktop" Version="???" />   <!-- Jodit 에디터용 -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.1" />

<!-- DB -->
<PackageReference Include="Microsoft.Data.Sqlite.Core" Version="10.0.x" />
<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.x" />

<!-- 출력 -->
<PackageReference Include="QuestPDF" Version="2026.x.x" />
<PackageReference Include="MiniExcel" Version="1.43.x" />

<!-- Google OAuth (토큰 암호화) -->
<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.x" />

<!-- Dev 전용 -->
<PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.x.x" Condition="'$(Configuration)'=='Debug'" />
```

> **확인 필요:** `Avalonia.WebView.Desktop` 버전 및 AOT 지원 여부

---

## 5. XAML 마이그레이션 치환 규칙

WinUI3 XAML을 Avalonia XAML로 변환할 때 적용할 일괄 치환 목록.

### 네임스페이스
```
xmlns:local="using:..."           →  xmlns:local="clr-namespace:..."
xmlns:controls="using:..."        →  xmlns:controls="clr-namespace:..."
mc:Ignorable="d"                  →  제거
d:DesignHeight / d:DesignWidth    →  제거
```

### 컨트롤 매핑
| WinUI3 | Avalonia |
|--------|----------|
| `Page` | `UserControl` |
| `Grid`, `StackPanel`, `Border` | 동일 |
| `TextBlock` | 동일 |
| `TextBox` | 동일 |
| `Button`, `ToggleButton` | 동일 |
| `CheckBox`, `RadioButton` | 동일 |
| `ComboBox` | `ComboBox` |
| `ListView` | `ListBox` |
| `GridView` | `ItemsControl` + `WrapPanel` |
| `NavigationView` | `SplitView` + 커스텀 |
| `ContentDialog` | 커스텀 `DialogBase` Window |
| `TeachingTip` | `ToolTip` 또는 커스텀 |
| `NumberBox` | `NumericUpDown` |
| `DatePicker` | `CalendarDatePicker` |
| `Expander` | `Expander` (Avalonia 내장) |
| `InfoBar` | 커스텀 `InfoBar` UserControl |
| `ProgressRing` | `ProgressBar IsIndeterminate="True"` |
| `Flyout` | `Popup` 또는 `FlyoutBase` |
| `MenuFlyout` | `MenuFlyout` (Avalonia 내장) |
| `AppBarButton` | `Button` + 아이콘 |
| `CommandBar` | `DockPanel` + `Button` 조합 |
| `ScrollViewer` | `ScrollViewer` |
| `WebView2` | `WebView` (Avalonia.WebView) |
| `Image` | `Image` |
| `Canvas` | `Canvas` |

### 바인딩
```
x:Bind Path, Mode=OneWay              →  {Binding Path}  또는  CompiledBinding
x:Bind Command                        →  {Binding Command}
Binding RelativeSource={RelativeSource Self}  →  동일
{ThemeResource ...}                   →  {DynamicResource ...}
{StaticResource ...}                  →  {StaticResource ...}
```

### 이벤트 / 코드비하인드
```
this.InitializeComponent();           →  동일
DispatcherQueue.TryEnqueue(...)       →  Dispatcher.UIThread.InvokeAsync(...)
ContentDialog.ShowAsync()            →  커스텀 DialogService.ShowAsync()
FileOpenPicker / FileSavePicker      →  StorageProvider.OpenFilePickerAsync()
ApplicationData.Current.LocalFolder  →  Environment.GetFolderPath(...)
```

---

## 6. 플랫폼 추상화 인터페이스 설계

### ICryptoService
```csharp
public interface ICryptoService
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
}
// Windows 구현: ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser)
// 미래 크로스플랫폼: AES-GCM + PBKDF2 (기기 고유 키)
```

### IFilePickerService
```csharp
public interface IFilePickerService
{
    Task<string?> OpenFileAsync(string[] extensions);
    Task<string?> SaveFileAsync(string defaultName, string[] extensions);
    Task<string?> OpenFolderAsync();
}
// Avalonia 구현: TopLevel.GetTopLevel(view).StorageProvider
```

### IKoreanImeService
```csharp
public interface IKoreanImeService
{
    void EnableKorean(nint windowHandle);
    void DisableKorean(nint windowHandle);
}
// Windows 구현: imm32.dll P/Invoke 기존 코드 그대로
// 미래 크로스플랫폼: no-op 또는 OS별 구현
```

---

## 7. Native AOT 주의사항

### 필수 유지사항
- `AvaloniaUseCompiledBindingsByDefault=true` — 리플렉션 바인딩 금지
- `{Binding}` 대신 `{CompiledBinding}` 사용 (또는 기본값 유지)
- `JsonSerializerContext` 소스 생성 — 신규 JSON 직렬화 타입 추가 시 반드시 등록
- `ViewLocator` — `Activator.CreateInstance` 대신 수동 딕셔너리 등록

### AOT 비호환 패키지 사전 검증 목록
| 패키지 | AOT 지원 | 비고 |
|--------|---------|------|
| Avalonia 12 | ✅ | CompiledBindings 필수 |
| CommunityToolkit.Mvvm | ✅ | Source Generator 사용 |
| Microsoft.Data.Sqlite | ✅ | ADO.NET 직접 |
| QuestPDF | ⚠️ 확인 필요 | 내부 리플렉션 가능성 |
| MiniExcel | ⚠️ 확인 필요 | |
| Avalonia.WebView | ⚠️ 확인 필요 | |
| System.Security.Cryptography.ProtectedData | ✅ | Windows 전용 |

### TrimmerRoots.xml 관리
- AOT 빌드에서 잘리는 타입 발생 시 `TrimmerRoots.xml`에 추가
- Phase 6에서 일괄 정리

---

## 8. 단계별 작업 목록

### Phase 0 — 프로젝트 기반 정비
- [ ] `SaemDesk.csproj` NuGet 패키지 추가 (`Avalonia.WebView.Desktop` 버전 확인 후)
- [ ] 폴더 구조 생성 (`Models`, `Repositories`, `Services`, `Views`, ...)
- [ ] `ViewLocator` AOT 안전 방식으로 재작성
- [ ] `DialogService` 기반 클래스 설계 (ContentDialog 대체)
- [ ] `App.axaml` 테마/리소스 기본 설정

### Phase 1 — 코어 레이어 이식 (UI 없음)
복사 후 네임스페이스만 `NewSchool` → `SaemDesk`로 변경.

- [ ] `Models/` 전체 (38개)
- [ ] `Repositories/` 전체 (20개)
- [ ] `Services/` 비즈니스 로직 (UI 미의존 서비스)
- [ ] `Google/` 전체 (5개)
- [ ] `Helpers/` 전체
- [ ] `Logging/FileLogger.cs`
- [ ] `Caching/CacheManager.cs`
- [ ] `Collections/OptimizedObservableCollection.cs`
- [ ] `DatabaseInitializer.cs` (3개 DB)
- [ ] `Settings.cs` (DPAPI 부분 `ICryptoService`로 교체)
- [ ] **빌드 확인** (경고 0)

### Phase 2 — 플랫폼 추상화 레이어
- [ ] `ICryptoService` + `WindowsDpApiCryptoService`
- [ ] `IFilePickerService` + `AvaloniaFilePickerService`
- [ ] `IKoreanImeService` + `WindowsKoreanImeService`
- [ ] DI 컨테이너 설정 (`App.axaml.cs`에서 등록)
- [ ] **빌드 확인** (경고 0)

### Phase 3 — UI 셸
- [ ] `App.axaml` — Fluent 테마, 전역 리소스, 초기화 시퀀스
- [ ] `MainWindow.axaml` — SplitView 기반 네비게이션
- [ ] 네비게이션 서비스 (`INavigationService`)
- [ ] `Converters/` 전체 이식
- [ ] 글로벌 스타일 (색상, 폰트, 컨트롤 템플릿)
- [ ] **빌드 + 런타임 확인** (창 뜨는지)

### Phase 4 — ViewModel
> Controls/Pages 작업 전에 ViewModel을 먼저 완성해 바인딩 오류를 컴파일 타임에 잡는다.

- [ ] `ViewModelBase.cs` (공통 베이스)
- [ ] `MainWindowViewModel` (네비게이션 상태)
- [ ] `StudentListItemViewModel`, `StudentCardViewModel`
- [ ] `StudentLogViewModel`, `StudentSpecialViewModel`
- [ ] `TimetableViewModel`, `SchoolScheduleViewModel`
- [ ] `ClassDiaryViewModel`, `AnnualLessonPlanViewModel`
- [ ] `SemesterIndexConverter` (ViewModel 보조)
- [ ] `ViewModels/Pages/` 하위 페이지별 ViewModel 전체
- [ ] **빌드 확인** (경고 0)

### Phase 5 — 컨트롤 (의존성 순서)
> 단순(독립) → 복잡(ViewModel 의존) 순서로 진행. 각 컨트롤 완성 후 단독 테스트.

#### 5-1. 독립 컨트롤 (ViewModel 없음)
- [x] `InfoBar` — WinUI InfoBar 대체, 가장 먼저
- [x] `MonthPicker`
- [x] `CompactTimePicker`
- [x] `DayCell`
- [x] **단독 테스트**

#### 5-2. ViewModel 의존 컨트롤
- [x] `MemoBoard` (MemoBoard + MemoBoardConverters)
- [x] `KAgendaControl`
- [x] `ClassFilterBar` — 학년도·학기·학년·반 필터, Students 이벤트, ShowSemester/IncludeAllClass 속성
- [x] `CoursePicker` — 과목·강의실 선택
- [x] `StudentLogBox` — 누가기록 입력 폼 UserControl (SetContext/LoadLog/BuildLog/Validate API)
- [x] **단독 테스트**

#### 5-3. WebView 컨트롤 (별도 검증)
- [x] `JoditEditor` (Avalonia.WebView + Jodit) — AOT 호환성 포함 검증
- [x] **단독 테스트**: 텍스트 입력·저장·로드 시나리오

### Phase 6 — 페이지 (단순 → 복잡 순서)
> 페이지 완성 후 각 기능 시나리오 테스트. 다이얼로그는 Phase 7에서 추가.

#### 6-1. 단순 페이지 (외부 의존 없음)
- [x] `HelpPage`
- [x] `SettingsPage` (AppSettingsPage)
- [x] `TodayPage`
- [x] **런타임 확인**

#### 6-2. 학생 관리
- [x] `StudentsPage` (StudentManagementPage)
- [x] `StudentLogPage`
- [x] `StudentSpecPage`
- [x] `AddStudentsPage`
- [x] `SchoolWorkPage`
- [x] **시나리오 테스트**: 학생 CRUD, 학생부 NEIS 바이트 계산

#### 6-3. 수업 / 시간표
- [x] `LessonsPage`, `LessonHomePage`, `LessonActivityPage`
- [x] `CourseManagementPage`
- [x] `DiaryPage` (ClassDiaryPage)
- [x] `ProgressMatrixPage`
- [x] `TeacherTimetablePage`
- [x] `SchoolScheduleManagementPage`
- [x] `SeatsPage`
- [x] **시나리오 테스트**: 수업 CRUD, 시간표 저장/복원, 자리 배치

#### 6-4. 동아리
- [x] `ClubHomePage`, `ClubManagementPage`, `ClubActivityPage`
- [x] **시나리오 테스트**

#### 6-5. 스케줄러 (Google Calendar)
- [x] `CalendarHomePage`, `CalendarPage`
- [x] `SchedulerPage` (KCalendar)
- [x] **시나리오 테스트**: Google OAuth 로그인 + Calendar 동기화

#### 6-6. 게시판 (JoditEditor 포함)
- [x] `BoardPage` (PostListPage)
- [x] PostDetail, PostEdit (JoditEditor 사용)
- [x] **시나리오 테스트**: 게시글 CRUD, 에디터 입력/저장

#### 6-7. 내보내기
- [x] `UnifiedExportPage`
- [x] **시나리오 테스트**: Excel/PDF 내보내기

### Phase 7 — 다이얼로그 (페이지 완성 후)
> 다이얼로그는 컨트롤·페이지에 의존하므로 반드시 Phase 6 완료 후 진행.

#### 7-1. 단순 다이얼로그 (독립적)
- [x] `ConfirmDialog`
- [x] `SchoolSearchDialog`
- [x] `ExportDialog`
- [x] `CalendarSettingsDialog`

#### 7-2. 중간 다이얼로그 (ViewModel 의존)
- [x] `StudentEditDialog`
- [x] `StudentDetailDialog`
- [x] `StudentLogEditDialog`
- [x] `StudentLogBatchDialog`
- [x] `CourseEditDialog`
- [x] `LessonLogEditDialog`
- [x] `TimetableEditDialog`
- [x] `DiaryEditDialog`

#### 7-3. 복잡 다이얼로그 (JoditEditor / 복합 의존)
- [x] `PostDetailDialog`
- [x] `PostEditDialog` (JoditEditor 포함)
- [x] `UnifiedItemDialog` (스케줄러 복합)
- [x] **전체 통합 테스트**

### Phase 8 — Native AOT 검증
- [x] `dotnet publish -r win-x64 -c Release` 빌드
- [x] Trimming 경고 분석 및 `TrimmerRoots.xml` 보완
- [x] QuestPDF, MiniExcel, Avalonia.WebView AOT 이슈 해결
- [x] 시나리오 테스트 (전체 통합 테스트와 연계):
  - [x] DB CRUD (학생/수업/게시판)
  - [ ] Google OAuth 로그인 + Calendar 동기화 — ⏸ 보류 (차후 진행)
  - [x] Jodit 에디터 텍스트 입력/저장
  - [x] Excel/PDF 내보내기
  - [x] 학생부 NEIS 바이트 계산
  - [x] 자리 배치 저장/복원

### Phase 9 — 마무리
- [x] 앱 아이콘 (`Assets/icon.ico`)
- [x] `app.manifest` (DPI aware, Windows 10+ 지원)
- [x] 버전 정보 (`AssemblyInfo`, `FileVersion`) — v1.0.0
- [x] `CHANGELOG.md` 업데이트
- [ ] GitHub 리포 연결 (`git remote add origin`)
- [x] `v1.0.0` 태그

---

## 9. 예상 일정

| Phase | 내용 | 예상 기간 |
|-------|------|---------|
| 0 | 프로젝트 기반 정비 | 0.5일 |
| 1 | 코어 레이어 이식 | 2일 |
| 2 | 플랫폼 추상화 | 1일 |
| 3 | UI 셸 | 1일 |
| 4 | ViewModel | 1~2일 |
| 5 | 컨트롤 | 2~3일 |
| 6 | 페이지 | 5~7일 |
| 7 | 다이얼로그 | 2~3일 |
| 8 | AOT 검증 | 1~2일 |
| 9 | 마무리 | 0.5일 |
| **합계** | | **약 3.5주** |

---

## 10. 크로스플랫폼 전환 시 추가 작업 (미래)

Phase 2에서 인터페이스를 분리해두면, 크로스플랫폼 전환 시 아래만 추가하면 됩니다.

| 항목 | 추가 작업 |
|------|---------|
| DPAPI | `LinuxCryptoService`, `MacCryptoService` 구현 (AES-GCM) |
| IME | no-op 또는 OS별 구현 (Avalonia가 자체 처리하면 불필요) |
| 파일 경로 | `%USERPROFILE%` → `Environment.GetFolderPath` (이미 크로스플랫폼) |
| WebView | 플랫폼별 WebView 백엔드 (`Avalonia.WebView` 지원) |
| 빌드 타겟 | `linux-x64`, `osx-arm64` 퍼블리시 프로파일 추가 |

**예상 추가 기간: 2~3일**
