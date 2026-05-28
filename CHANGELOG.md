# SaemDesk 변경 이력

모든 주목할 만한 변경 사항은 이 문서에 기록됩니다.
형식: [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/) 기준, 버전 표기는 [SemVer](https://semver.org/lang/ko/) 를 따릅니다.

---

## [Unreleased]

### Fixed — Board(게시판) 버그 수정 및 구조 개선 (2026-05-23)

#### BoardDatabase 이중화 문제 해결
- **`BoardDatabase.cs` (루트)** — 구버전 스키마(`Subject`, `Comment`, `PostFile`, `RefNo`, `ReplyOrder`, `Depth`, `HasFile`, `HasComment` 누락) 를 완전한 스키마로 교체. DB 초기화 진입점 역할 유지.
- **`Board/BoardDatabase.cs`** — 중복된 `InitAsync()` 제거. 경로 헬퍼·팩토리 역할만 유지. `DbPath`를 `Settings.Board_DB.Value` 기반으로 통일.
- **`App.axaml.cs`** — `_ = InitAsync()` (fire-and-forget) → `.GetAwaiter().GetResult()` 동기 보장으로 변경. 앱 시작 시 `UserDataPath` / `BoardDatabase.DbPath` 로그 추가.
- **`SchoolDatabase.cs`** — DB 파일 존재 + `School_Inited=true` 시 `DatabaseInitializer` 스킵. 매 실행마다 테이블 생성 로그가 출력되던 문제 해결.

#### PostEditPage / PostEditDialog 통합 (방향 A)
- **`MemoBoard.axaml.cs`** — `PostEditDialog` 의존성 제거. `OpenEditAsync`를 `PostEditPage` + 익명 `Window` 호스팅 방식으로 교체. `Saved`/`Cancelled` 이벤트 연결.
- **`Board/Views/Pages/PostEditPage.axaml.cs`** — `ContentEditor.Text` → `await ContentEditor.GetHtmlAsync()` (WebView 미초기화 폴백 포함).
- `Views/Dialogs/PostEditDialog.axaml/.cs` — **직접 삭제 필요** (ViewLocator 미등록 확인됨).

#### PostFileListBox 개선
- **`Board/Views/Controls/PostFileListBox.axaml`** — 드롭 영역과 파일 목록을 단일 `Border` 안에 통합. `WrapPanel` 기반 칩 레이아웃 적용. 파일 없을 때 자동 축소, 많을 때 `MaxHeight=120` 초과 시 스크롤바 표시.
- **파일 아이템 구조** — `[☐ 체크박스] [파일명 (용량) 버튼]` 형태로 단순화. 버튼 클릭 시 파일 열기. 읽기 모드에서는 체크박스 숨김.
- **버튼 스타일** — `fileBtn` 클래스: `#E3F0FB` 연파랑 배경, `#1A5C9A` 텍스트, 호버/클릭 색상 단계적 적용.
- **`FileBoxItem`** — `ObservableObject` 상속으로 변경 (`IsSelected`, `ShowCheckBox` 바인딩 실시간 반영). `Label` 프로퍼티 추가 (`파일명 (용량)` 형식).
- **DataTemplate DataType** — `PostFile` → `ctrl:FileBoxItem` 으로 수정. 바인딩 경로 교정 (`PostFile.FileName` 등).

#### RosterTableDialog 개선
- **`Views/Dialogs/RosterTableDialog.axaml`** — 수업 패널 필터를 `YearSemesterPicker + CoursePicker` 한 줄 가로 배치로 변경. 학기 `ComboBox` 및 `YearLabel` 제거.
- **`Views/Dialogs/RosterTableDialog.axaml.cs`** — `SemesterComboBox_SelectionChanged` → `OnYearSemesterChanged` 교체. scopeLabel에 학기 포함.
- 이동수업(선택형, `!IsClassType`) 단일 테이블에 **학년·학급·번호·이름** 칼럼 추가.

#### CoursePicker 개선
- **`Views/Controls/CoursePicker.axaml.cs`** — `IncludeAllRoom` 프로퍼티 추가 (기본 `false`). 강의실 2개 이상 + `IncludeAllRoom=true` 시 "전체" 첫 항목 삽입. `SelectedRoom`을 `Tag as string` 기반으로 변경 ("전체" 선택 시 `null` 반환). `FetchStudentsAsync` — 강의실 필터 적용 (null=전체, 특정값=해당 강의실만). 비학급형 전체 조회를 `GetBySchoolAndYearAsync`로 변경.

### Added — AnnualLessonPlanPage (연간 수업 계획) 이식 (Chunk E)

#### 신규 파일
- **`Views/Pages/AnnualLessonPlanPage.axaml/.cs`** — NewSchool AnnualLessonPlanPage 완전 이식. 3탭 구조 (단원 관리 / 시수 관리 / 단원 배치).
- **`ViewModels/Pages/AnnualLessonPlanPageVM.cs`** — ViewLocator AOT 등록용 셸 ViewModel.
- **`Repositories/ScheduleUnitMapRepository.cs`** — 수업-단원 매핑 CRUD (SchedulingEngine 의존성).
- **`Services/ScheduleShiftService.cs`** — 배치 Undo/Redo 서비스.

#### 페이지 기능
- **탭1 단원 관리**: 소단원 추가(`CourseSectionDialog` 연동), 단원 목록(연번·단원번호·소단원명·유형·페이지·시수), 개별/전체 삭제, CSV 가져오기/내보내기/템플릿 다운로드, 엑셀 내보내기(`ReportExportService`). 단원 수/차시 통계 표시.
- **탭2 시수 관리**: 시간표 정보 카드 + NEIS 학사일정 카드 + 통계 요약 (총 주차/수업일/총 시수/단원시수). 주차별 학급별 시수 테이블(셀 클릭 → `NumericUpDown` 인라인 편집). 수동 편집값 accent 색상 강조. 합계 행 자동 갱신. 시수 과부족 InfoBorder 경고.
- **탭3 단원 배치**: 좌측 단원 목록 요약 (유형별 통계 배지). 우측 배치 컨트롤 — 학급 선택 ComboBox + 기간 CalendarDatePicker + 🚀 자동 배치 실행 버튼 (`SchedulingEngine` 연동). 배치 결과 주차별 그룹화 목록. 단원 변경/배치 삭제 수동 편집. Undo(↩)/Redo(↪) 버튼.
- **NEIS 학사일정**: 페이지 로드 시 자동 조회, 🔄 새로고침 버튼. 공휴일/방학 반영한 수업일 계산.

#### 수업 선택
- 헤더 우측 ComboBox에서 수업 선택 → 탭2·탭3 자동 갱신. 부제목에 `학년도·학기·과목명` 표시.

#### WinUI3 → Avalonia 12 변환
- `Pivot/PivotItem` → `TabControl/TabItem`
- `ListView` → `ListBox`
- `NumberBox` → `NumericUpDown`
- `CalendarDatePicker.Date` → `.SelectedDate`
- `FontIcon Glyph` → 이모지 `TextBlock`
- `Visibility="Collapsed"` → `IsVisible="False"`
- `StorageProvider (WinRT)` → `App.FilePicker`
- `ContentDialog` → `DialogService.ShowConfirmAsync / ShowInfoAsync / ShowCustomAsync`
- `ThemeResource` → `DynamicResource`

#### 기타 변경
- **`MainWindowViewModel`** — "연간 수업 계획" 네비게이션 항목을 `LessonsPageVM` → `AnnualLessonPlanPageVM`으로 수정.
- **`App.axaml.cs`** — `ViewLocator.Register<AnnualLessonPlanPageVM>` 추가.
- **`Services/DialogService.cs`** — `ShowInfoAsync(message)` / `ShowCustomAsync(title, content)` 추가.

---

## [0.2.0-alpha] — 2026-04-30

Phase 5/6 마무리 + Chunk D/F 청크 + Phase 5 기능 보강.
(상세 내역은 git log 참조)

### 진행 상황
- ✅ Phase 4 — 공통 컨트롤
- ✅ Phase 5 — 본 이식 + 보강 3차
- ✅ Phase 6 — Native AOT 검증
- ✅ Chunk D — 동아리 / Chunk F — 업무 / TeacherTimetablePage
- ✅ Chunk E — AnnualLessonPlanPage (연간 수업 계획)
- ⏳ Phase 7 — 마무리 (아이콘·리포 연결·v0.2.0 태그·릴리스)
- ⏳ 시나리오 스모크 테스트 (DB CRUD / Google OAuth / Jodit / Excel·PDF / NEIS / 좌석)

---

## [0.1.0-alpha] — 2026-04-26

NewSchool (WinUI3 + .NET 10) 에서 SaemDesk (Avalonia 12 + .NET 10 + Native AOT) 로 마이그레이션 첫 알파.
(상세 내역은 git log 참조)
