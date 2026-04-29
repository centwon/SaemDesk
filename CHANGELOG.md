# SaemDesk 변경 이력

모든 주목할 만한 변경 사항은 이 문서에 기록됩니다.
형식: [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/) 기준, 버전 표기는 [SemVer](https://semver.org/lang/ko/) 를 따릅니다.

---

## [Unreleased]

### Changed — StudentLogPage UI 정리
- **PDF 인쇄 구현** (`BtnPrint_Click`) — `StudentCardViewModel.LoadStudentAsync` → `StudentLogPrintService.GenerateStudentLogPdf` → `Process.Start`.
- **일괄 출력 구현** (`BtnBatchExport_Click` / `BatchExportAsync`) — `BatchExportFilterDialog` → `EnrollmentService.GetClassRosterAsync` → 학기·카테고리·키워드 필터 → PDF(`StudentLogPrintService`) 또는 Excel(`StudentLogExportService`) → `Process.Start`.
- **글꼴 크기 슬라이더** — `BtnFontResize` Flyout(세로 Slider 6~30) 추가. `LogListViewer.LogFontSizeProperty`(StyledProperty) → 기록 내용 TextBox FontSize 바인딩.
- **조회 버튼 제거** — `FilterBar.SelectionChanged`에서 자동 로드하므로 제거. `OnLoadStudents` 핸들러 삭제.
- **카테고리 콤보박스** — 레이블 제거, `VerticalAlignment="Center"`로 다른 필터와 수직 정렬 통일.
- **구분선** — `<Separator/>` → 세로 `Border(Width=1)` 로 교체.
- **일괄 입력·출력 버튼** — 이모지 아이콘(📝 📤 `FontSize=20`) + 툴팁 전용으로 변경.
- **버튼 스타일 통일** — 추가·저장·상세·인쇄·삭제 버튼에 이모지(FontSize=16) + 텍스트 조합 적용.
- **글꼴 크기 버튼 아이콘** — `Aa` 텍스트 조합 → 🔠 이모지(`FontSize=20`)로 교체.
- **본문 여백** — `Grid Margin="12,0,12,12"` 추가. 필터바~본문 간격 `RowDefinition Height="12"`.
- **ListStudent 너비** — 120 → 160px, 간격 16 → 8px.

### Changed — ListStudent 컨트롤 버그 수정 및 개선
- **아이템 체크박스 미표시 수정** — `ContainerPrepared`/`UpdateItemColumns` 방식 제거. `CheckColumnWidth` 등 StyledProperty 선언 후 `ItemGrid` `ColumnDefinition.Width`를 `{Binding #Root.CheckColumnWidth}`로 직접 바인딩. 컨테이너 생성 타이밍과 무관하게 항상 반영.
- **헤더 체크박스 해제 버그 수정** — `SelectedItems.Clear()` → `UnselectAll()` 메서드로 교체. `_suppressSelectAllEvent` 플래그로 재진입 차단.
- **헤더 체크박스 라벨 "전체" 제거** — 체크박스만 표시.
- **체크박스 위치 정렬** — 헤더 `Border Padding="12,8"` → `"4,8"`. 아이템 `ListBoxItem.Padding="4,2"`와 left 기준 일치.
- **행 클릭 토글** — 체크박스 모드에서 행 영역 클릭 시 ListBox 기본 단일선택 동작 차단, `IsSelected` 토글로 체크박스 클릭과 동일하게 동작.
- **`SelectionChangedNotify` 이벤트** 추가 — 다중선택 모드 전용 카운터 알림(`EventHandler<int>`).

### Changed — StudentLogBatchDialog 정리
- **전체 선택·해제 버튼 제거** — 헤더 체크박스가 담당하므로 `BtnSelectAll`/`BtnDeselectAll` 및 감싸는 `Grid` 제거.
- **헤더 "활동 기록 일괄 입력" TextBlock 제거** — 창 타이틀과 중복.
- **카운터 갱신** — `StudentSelected` → `SelectionChangedNotify` 이벤트로 교체(다중선택 모드 대응).
- **창 크기** — Height 690 → 780, MinHeight 600 → 680. ListStudent 너비 220 → 160px.

### Changed — StudentSpecPage UI 정리
- **조회 버튼 제거** — `FilterBar.SelectionChanged` + `OnCategoryChanged`에서 자동 로드. `OnQueryClick` 핸들러 삭제.
- **카테고리 변경 시 자동 재조회** — `OnCategoryChanged`에 `await LoadSpecsAsync(_currentStudents)` 추가(조회 버튼 제거로 인한 누락 복구).
- **헤더 구조 통일** — `StudentLogPage`와 동일하게 `Padding="0"` + `Grid Margin="8,4"`. `RowDefinition Height="12"` 간격 행 추가.
- **카테고리 콤보박스** — `VerticalAlignment="Center"`.
- **구분선** — `<Separator/>` → 세로 `Border(Width=1)`.
- **저장·삭제 버튼** — 이모지(`FontSize=16`) + 텍스트 조합.
- **일괄 입력·출력 버튼** — 이모지(📝 📤 `FontSize=20`) + 툴팁 전용.
- **글꼴 크기 버튼** — 🔠 이모지(`FontSize=20`) + Flyout 슬라이더 추가.
- **본문 여백** — `SpecListViewer Margin="12,0,12,12"`.

### Added — SpecListViewer 글꼴 크기 제어
- `SpecFontSizeProperty`(StyledProperty, 기본값 13) 추가.
- 기록 내용 `TextBox FontSize="13"` → `{Binding #Root.SpecFontSize}` 바인딩.
- `StudentSpecPage.SldFontSize_ValueChanged` → `SpecListViewer.SpecFontSize` 반영.

### Added — LogListViewer 글꼴 크기 제어
- `LogFontSizeProperty`(StyledProperty, 기본값 12) 추가.
- 주제·기록 내용 `TextBox FontSize="12"` → `{Binding #Root.LogFontSize}` 바인딩.
- `StudentLogPage.SldFontSize_ValueChanged` → `LogListViewer.LogFontSize` 반영.

### Added — 창 상태 영속화
- `Settings.cs` — `WindowX`, `WindowY`, `WindowIsMaximized`, `LastPage` 프로퍼티 추가. 기존 `WindowWidth`/`WindowHeight`와 함께 `Settings.db`에 저장·복원.
- `MainWindow.axaml.cs` — `RestoreWindowState()` / `SaveWindowState()` / `OnSizeChanged()` 구현. 앱 시작 시 이전 창 크기·위치·최대화 상태 복원. 최대화 상태로 닫힐 때도 복원 크기(`_restoredSize`) 보존. 멀티모니터 환경에서 `WindowX=-1`(초기값)이면 기본 위치 유지.

### Changed — StudentInfoPage 완성
- **필터**: `ClassFilterBar`에 `IncludeAllClass="False"` 적용 — 반 목록에서 "전체" 제거. 담임 설정(`Settings.HomeGrade` / `Settings.HomeRoom`)으로 초기화.
- **조회 버튼 제거**: 반 선택 시 자동 로드(`OnFilterBarChanged`). `BtnLoad_Click` 핸들러 삭제.
- **StudentCard 둥근 테두리**: 감싸는 `Border(CornerRadius=8, ClipToBounds=True)`로 래핑.
- **스크롤 구조 개편**: 우측 컬럼을 `ScrollViewer + StackPanel`로 변경 — StudentCard·누가기록을 하나의 스크롤 영역으로 통합.
- **StudentCard 하단 짤림 수정**: 내부 `ScrollViewer` → `StackPanel`로 교체. 외부 `ScrollViewer`가 스크롤 담당.
- **너비 고정**: `StackPanel Width="755"` (960창 - 180목록 - 17스크롤바 - 8여백). `MaxWidth` 제한 제거.
- **PDF 출력 구현**: `BtnPrint_Click` TODO 제거 → `StudentPrintOptionsDialog` → `StudentCardPrintService` 연결. 사진 경로 기준 `AppDomain.BaseDirectory` → `Settings.UserDataPath` 수정.
- **엑셀 양식 다운로드 / 일괄입력**: `BtnDownloadTemplate_Click` / `BtnBulkImport_Click` 구현. `BulkStudentInfoPreviewDialog` 연결.
- **LogListViewer 기록 내용 컬럼**: 헤더·아이템 행의 마지막 컬럼 `SharedSizeGroup="ColLog"` 제거 → `Width="*"` 독립 적용. 남은 공간 전체 차지.

### Added — Phase 5 컨트롤 보강 (ClassFilterBar · CoursePicker · StudentLogBox)
- **ClassFilterBar** (`Views/Controls/ClassFilterBar.axaml`) — 학년도·학기·학년·반 4단 필터 컨트롤. `ShowSemester=False` 시 `WorkSemester` fallback 적용. `IncludeAllClass` 속성으로 반 전체 허용 여부 제어 (학년 전체는 항상 불허). `Students` 이벤트로 선택된 수강생 목록을 구독자에게 전달.
- **CoursePicker** (`Views/Controls/CoursePicker.axaml`) — 과목·강의실 선택 컨트롤. ClassFilterBar와 독립적으로 동작.
- **StudentLogBox** (`Views/Controls/StudentLogBox.axaml`) — 학생 누가기록 입력 폼 UserControl. `SetContext(year,sem,cat)` / `LoadLog(log)` / `BuildLog(studentId, sourceLog?)` / `Validate()` / `LockCategory()` / `SetSubjectName()` / `ShowError()` API 제공. 저장·취소 버튼 없음(다이얼로그가 담당).

### Added — Phase 5 다이얼로그 보강
- **StudentLogEditDialog** — StudentLogBox 재사용. 개별 누가기록 작성/수정 다이얼로그.
- **StudentLogBatchDialog** — ListStudent(체크박스)+StudentLogBox 조합. 다중 학생 일괄 입력.
- **StudentLogDialog** — 기존 4모드 다이얼로그를 새 StudentLogBox API로 교체 완료.

### Changed — 페이지 적용
- `StudentLogPage`, `StudentSpecPage`, `DiaryPage`, `StudentInfoExportPage`, `SeatsPage` — `ClassFilterBar.Students` 이벤트 직접 구독 방식으로 통일.
- `EnrollmentRepository.GetByClassAsync` — `semester` 파라미터 추가.

### Changed — StudentInfoPage UI 개선
- 상단 헤더의 누가기록 버튼 3개(＋기록·저장·삭제)를 누가기록 박스 헤더 우측으로 이동. 상단 헤더는 학생카드 관련(💾 저장·📄 PDF·초기화)만 유지.

### Changed — StudentDetailDialog 재설계 (읽기 전용 뷰어)
- `StudentDetailDialog` — 편집 가능한 폼에서 **읽기 전용 뷰어**로 재설계.
  - 탭1 "학생 정보": 직접 폼 필드 18개 제거 → `StudentCard` 컨트롤 임베드 (`IsEnabled=False`).
  - 탭2 "활동 기록": 커스텀 ListBox 제거 → `LogListViewer` 임베드 (`StudentInfoMode=HideAll`).
  - 헤더: 학생이름+학번 → **이름(TitleText) + 학년반번(SubTitleText)** 으로 변경.
  - 하단 버튼: 저장/PDF/추가/수정/삭제 전부 제거 → **닫기만** 유지.
  - 생성자 시그니처: `(studentId, studentName)` → `(studentId, classInfo, studentName, year=0)`. `year=0` 이면 `Settings.WorkYear` fallback.
  - 코드비하인드 ~200줄 → ~70줄로 축소.
- `DialogService.ShowStudentDetailAsync` — 시그니처 `(id, name)` → `(id, classInfo, name, year=0)` 로 맞춤.
- `StudentLogPage`, `DiaryPage` — `StudentDetailDialog` 호출부를 새 시그니처(`GetClassInfo()`, `WorkYear`/`_currentYear` 전달)로 수정.

### Fixed — LogListViewer 열 정렬
- 헤더 Grid와 아이템 행 Grid의 열이 따로 노는 문제 수정. `Grid.IsSharedSizeScope="True"` + `SharedSizeGroup` 12개(`ColCheck`~`ColLog`)로 헤더·아이템 열 너비 자동 동기화.
- `SetHeaderColWidth()` — `w=0` 시 `MinWidth=0, MaxWidth=0` 함께 설정해 SharedSizeGroup 환경에서도 열이 완전히 collapse되도록 수정.

---

## [0.2.0-alpha] — 2026-04-30

Phase 5/6 마무리 + Chunk D/F 청크 + Phase 5 기능 보강.

### Added — Phase 5 잔여 본 이식
- **SchoolScheduleManagementPage** — 인라인 편집형(연도/기간 필터, 행사명·내용·수업공제일·G1~G6 학년체크), NEIS 동기화, 수동 추가, 변경 자동 추적, 일괄 저장/삭제. **Excel 내보내기 + Google Calendar 일괄 업로드 + 일괄 학년 적용**.
- **ProgressMatrixPage** — 수업 선택 → 단원×분반 매트릭스, 셀 클릭 토글, ProgressType 5색. **격차 분석 + 일정 동기화(LessonLog → 진도) + Excel + 셀 우클릭 메뉴 6항(완료/보강/병합/건너뜀/결강/비우기)**.
- **MemoBoard 컨트롤** — PostRepository 위임, 카테고리 필터·추가, 빈/로딩 상태. **카테고리 4색 칩, HTML→텍스트 인라인 미리보기, 더블클릭 PostEditDialog 편집, ▲/▼/🗑 항목별 액션**.

### Added — Chunk D 동아리 그룹
- `ClubHomePage` / VM — 담당 동아리 카드(이름·활동실·학년도·부원수·비고).
- `ClubManagementPage` / VM — 좌측 목록 + 우측 편집 폼 CRUD, Ctrl+S 저장.
- `ClubActivityPage` / VM — 동아리 선택 → 부원 목록 + StudentLog(카테고리=동아리활동, ClubNo 필터) 활동 기록, Enter 즉시 추가.
- 좌측 트리 ‘🎭 동아리’ 그룹 추가.

### Added — TeacherTimetablePage + Chunk F (업무)
- `TeacherTimetablePage` / VM + `PeriodRowVM` — Settings.UserName(교사 ID) 기반 5×7 시간표, `TimetableService.GetTeacherTimetableAsync` 위임. `SemesterIndexConverter` 추가.
- `SchoolWorkPage` / VM — 좌: 업무 메모(MemoBoard 임베드), 우: 업무 카테고리 게시글 목록.

### Added — 통합 내보내기 페이지
- `UnifiedExportPage` + VM — 학급(학년도/학년/반) × 4 데이터 × 4 형식 콤보 → `UnifiedExportService.ExportClassAsync` 위임. 좌석/학생카드 시 Excel 비활성, CSV 는 누가/학생부만.

### Build / Phase 6 AOT
- TrimmerRoots: QuestPDF · MiniExcel 보호.
- `dotnet publish -r win-x64 -c Release` Native AOT 단일 파일 33MB 검증, 출력 exe 4초 스모크 통과.
- 잔존 경고: IL3000(QuestPDF Assembly.Location, 자체 코드 영향 없음), IL2104/IL3053(외부 패키지 — TrimmerRoots 보호).
- 모든 chunk 종료 시점 빌드 0 경고 / 0 오류.

### 진행 상황
- ✅ Phase 4 — 공통 컨트롤
- ✅ Phase 5 — 본 이식 + 보강 3차
- ✅ Phase 6 — Native AOT 검증
- ✅ Chunk D — 동아리 / Chunk F — 업무 / TeacherTimetablePage
- ⏳ Phase 7 — 마무리(아이콘·리포 연결·v0.2.0 태그·릴리스)
- ⏳ Chunk E — AnnualLessonPlanPage(867 줄)
- ⏳ 시나리오 스모크 테스트(DB CRUD / Google OAuth / Jodit / Excel·PDF / NEIS / 좌석)

### Added — 네비게이션 / 셸
- **트리형 좌측 네비게이션** — 평면 ListBox → `TreeView` + `TreeDataTemplate(ItemsSource={Binding Children})`. `NavItem` 모델에 그룹/리프 구분 추가(그룹 노드는 클릭 시 페이지 전환 차단). NewSchool 의 부모-자식 메뉴 구조와 정렬.
- **달력(통합 월간 캘린더) 메뉴** 신설 — 기존 “학사일정” 페이지를 “학사일정 관리”로 이름 정정, 새 “달력” 슬롯 추가.

### Added — 통합 달력 (Kcalendar 포팅)
- **Cal-1 월간 표시** — `Scheduler/DayInfo.cs` + `Views/Controls/DayCell.axaml(.cs)` + `Views/Pages/CalendarHomePage.axaml(.cs)` + `ViewModels/Pages/CalendarHomePageVM.cs`. 6×7 셀, 학사일정·일정·할일 표시, 호버/오늘/공휴일/휴업 색상 구분, 타월(다른 달) 흐림.
- **Cal-2 통합 편집 다이얼로그** — `Views/Dialogs/UnifiedItemDialog.axaml(.cs)`. 할일/일정 RadioButton 모드 전환, 반복 옵션(매일·매주·매월·매년), Google 색상 12종, 즉시 Google Push.
- **Cal-3 KAgendaControl** — `Scheduler/AgendaItem.cs` + `Views/Controls/KAgendaControl.axaml(.cs)`. 재사용 컨트롤(필터 ComboBox + 할일/일정 토글), `FixedCalendarName` StyledProperty 로 카테고리 잠금. TodayPage·LessonHomePage 등에서 임베드.
- **Cal-4 셀 항목 직접 상호작용** — DayCell 안 일정 바/할일 행 클릭 → 해당 항목 편집, 할일 토글 버튼(완료/진행), 헤더 ⚙ → CalendarSettingsDialog.
- **Cal-4 화면 간 동기화** — `Scheduler/SchedulerEvents.ItemChanged` 정적 이벤트 버스. 달력 ↔ TodayPage 어젠다 자동 새로고침.
- **Cal-5 MonthPicker + “+N개 더” Flyout** — 헤더의 `yyyy년 M월` Button → Flyout 으로 연도 ◀▶ 12개월 그리드. 셀 항목이 4개 초과 시 “+N개 더…” 라벨 → Flyout 에 전체 리스트.
- **Cal-6 드래그-드롭** — `DataFormat.CreateInProcessFormat<KEvent>(...)` + `DragDrop.DoDragDropAsync`. 셀 → 셀 이동 시 `Start/End` 시프트 + DB 저장.

### Added — 학급 그룹 (Chunk B)
- **학생 누가기록** (`StudentLogPage`) — 기간/카테고리/검색 필터, 추가·편집·삭제.
- **학생부 특기사항** (`StudentSpecPage`) — 학년도/카테고리 필터, 카테고리 색상 배지, 확정 표시, 삭제.
- **자리 배정** (`SeatsPage`) — 좌측 명렬 + 우측 동적 좌석 격자. 줄/짝/잠금/저장/초기화. **드래그-드롭** 두 방향(명렬→좌석 / 좌석↔좌석). **이력 회피 자동배정**(`SmartArrangeAsync` — 80회 시도, `GetRecentPairsAsync` + `GetRecentPositionsAsync` 활용한 패널티 점수).
- **학생 추가** (`AddStudentsPage`) — 학년도/학년/반/번호/이름 수동 입력, 임시 목록 누적, 일괄 저장, **Excel(.xlsx) 가져오기 + 템플릿 다운로드** (MiniExcel).

### Added — 수업 그룹 (Chunk C)
- **수업홈** (`LessonHomePage`) — 내 수업 목록 + 최근 수업 기록 + 수업 카테고리 KAgendaControl 임베드.
- **수업 관리** (`CourseManagementPage`) — 학년도/학기/유형/검색 필터, 추가·편집·삭제. `CourseEditDialog` 연결(과목·학년·단위·유형·강의실·비고 + 강의실 4개 프리셋).
- **수업 누가 기록** (`LessonActivityPage`) — 학기/과목/학급/검색 필터, 추가·편집·삭제. `LessonLogEditDialog` 연결(날짜·교시·과목·학년·반·강의실·단원명·주제·내용·메모).

### Added — 페이지 진입점 / 도움말
- **HelpPage 추가** — 처음 시작/페이지별 가이드/Google Calendar 연동/NEIS API/FAQ 6개 섹션. 하단 내비게이션에 “도움말” 버튼.
- **CHANGELOG.md 도입** — 변경 이력 기록 시작.
- **AppInfo 단일 출처** — `AppInfo.Version` 으로 표시용 버전 통일. `SaemDesk.csproj` 의 `<Version>` 메타데이터와 동기화.
- **app.manifest** — Per-Monitor V2 DPI awareness + longPathAware 지정.

### Changed
- **NEIS API 키 입력 UI 제거** — Google OAuth 자격증명과 동일하게 secrets.json (neis_api_key) 빌드 시 주입 방식으로 일원화. `Settings.NeisApiKey` 는 더 이상 DB 에 영구 저장하지 않으며, 빌드 시 secrets.json 의 값이 그대로 유지됩니다.

### Added — 통합 내보내기 페이지
- `UnifiedExportPage` + `UnifiedExportPageVM` — 학급(학년도/학년/반) × 데이터 타입 4종 × 형식 4종 콤보 선택 → `UnifiedExportService.ExportClassAsync` 위임. 좌석/학생카드 선택 시 Excel 비활성, 누가/학생부만 CSV 가용.
- 좌측 트리에 **통합 내보내기** 항목 노출 + ViewLocator 등록.

### Added — Phase 5 잔여 placeholder
- `ProgressMatrixPage` / VM (수업 그룹 — 본 이식 대기).
- `SchoolScheduleManagementPage` / VM (편집용 — 기존 CalendarPage 는 보기 전용으로 분리).
- `Views/Controls/MemoBoard` 컨트롤 (Board 탭에서 사용 예정).

### Build
- TrimmerRoots.xml 에 QuestPDF · MiniExcel 어셈블리 보호 등록(IL2104 trim 경고 보강).
- `dotnet publish -r win-x64 -c Release` **Native AOT 재검증** — 단일 파일 33MB exe 생성, 출시 후 4초 스모크 테스트 통과 (정상 윈도우 표시).
- 잔존 경고: IL3000 (QuestPDF Assembly.Location — 자체 코드는 AppContext.BaseDirectory 사용으로 영향 없음), IL2104/IL3053 (QuestPDF·MiniExcel — TrimmerRoots 보호로 노이즈 처리).
- 모든 청크 종료 시점 빌드 0 경고 / 0 오류 유지.

### 진행 상황 (chunk 단위)
- ✅ Chunk A — 트리형 nav 셸 재편
- ✅ Cal-1 ~ Cal-6 — 통합 달력 풀-스택 (NewSchool Kcalendar 동등)
- ✅ Chunk B — 학급 그룹 핵심 4종 (학생 누가기록 / 학생부 특기사항 / 자리 배정 / 학생 추가)
- ✅ Chunk B-Add — Excel 가져오기 + 템플릿 다운로드
- ✅ Chunk C — 수업 그룹 핵심 3종 + 편집 다이얼로그 2종
- ✅ Chunk D — 통합 내보내기 페이지
- ✅ Phase 6 — Native AOT publish 재검증 통과
- ⏳ Phase 5 잔여 본 이식 (ProgressMatrix / SchoolScheduleManagement / MemoBoard)

### 미진행 (남은 청크)
- Chunk D — 동아리 그룹 (ClubHomePage / ClubManagementPage / ClubActivityPage)
- Chunk E — 대형 페이지 (AnnualLessonPlanPage 867 줄, ProgressMatrixPage)
- Chunk F — 업무 그룹 (PageSchoolWork)
- TeacherTimetablePage / SchoolScheduleManagementPage 본 이식
- 시나리오 스모크 테스트 (DB CRUD / Google OAuth / Jodit / Excel/PDF 내보내기 / NEIS / 좌석 저장복원)

---

## [0.1.0-alpha] — 2026-04-26

NewSchool (WinUI3 + .NET 10) 에서 SaemDesk (Avalonia 12 + .NET 10 + Native AOT) 로 마이그레이션 첫 알파.

### Added
#### Phase 0~3 — 기반
- Avalonia 12 + .NET 10 + Native AOT(`PublishAot=true`) 프로젝트 셸.
- 컴파일 바인딩 기본 활성화(`AvaloniaUseCompiledBindingsByDefault=true`).
- 네비게이션 셸(MainWindow + 상단/하단 NavItem + ViewLocator AOT 등록).
- 플랫폼 추상화: `IFilePickerService`, `ICryptoService`(DPAPI), `IKoreanImeService`.

#### Phase 4 — 공통 컨트롤
- `JoditEditor` 사용자 컨트롤(NativeWebView 기반, ReadOnly/Simple/Full 3 모드).
- `CompactTimePicker`(NumericUpDown 2개로 시:분 입력).
- `InfoBar`(상태 메시지 + 자동 사라짐).

#### Phase 5 — 페이지 / 다이얼로그
- **오늘** — 시간표·급식·일정 요약.
- **학생** — 학급 명렬, 학생 카드, 누가기록·학생부 특기사항. 좌석 배정.
- **수업** — 과목·시간표·차시 기록.
- **학급일지** — 날짜별 일지 작성.
- **게시판** — 카테고리·검색·Jodit HTML 에디터 기반 작성/조회/수정/삭제, 조회수 증가.
- **스케줄러** — 일정·할일 통합 관리.
- **학사일정** — NEIS 학사일정 다운로드 + 달력 표시.
- **설정** — 사용자/학교/학년도/수업 시간/NEIS API/일정·Google Calendar/앱 옵션.

#### 데이터 / 서비스
- SQLite + Microsoft.Data.Sqlite (학생/수업 DB와 게시판 DB 분리).
- `BaseRepository` + ReaderColumnCache 패턴.
- `SchoolService`(NEIS Open API 학교 검색·저장).
- `SchoolScheduleService`(NEIS 학사일정).
- `UnifiedExportService` — 학생 데이터 4종 × 4 형식(Excel/PDF/HTML/CSV) 통합 내보내기. QuestPDF + MiniExcel.

#### Google Calendar 연동
- `GoogleAuthService` — OAuth 2.0 PKCE + 루프백 리다이렉트, DPAPI 토큰 암호화.
- `GoogleCalendarApiClient` — REST v3, 401 자동 재시도, 429/5xx 지수 백오프, 410 syncToken 만료 처리.
- `GoogleSyncService` — 양방향 증분 동기화(syncToken), Ktask `extendedProperties` 통합, 학사일정 일괄 등록(연속일정 그룹핑).
- `secrets.json` 인프라 — `secrets.template.json` + 빌드 시 조건부 `<Content>` 복사 + `.gitignore`.
- `CalendarSettingsDialog` — 일정 표시/Google 연동/동기화/학사일정 일괄 등록 4 Expander.
- 인증 직후 자동 매핑(`개인` → primary 캘린더, `수업/학급/업무` → 학교명 캘린더 또는 신규 생성).
- `App.GoogleSync` 정적 인스턴스 + `RestartGoogleAutoSync()` — 앱 수명 동안 백그라운드 자동 동기화.

### Fixed / Polished
- `WorkSemesterIndex` setter — `SelectedIndex=-1` 입력 무시(콤보박스 초기화 시 잘못된 값 저장 방지).
- `BoardPageVM._suppressFilter` — Categories 재빌드 중 중간 단계마다 필터링되던 깜빡임 제거.
- `SettingsPageVM.ShowStatus` — InfoBar 메시지가 3초 후 자동 사라지도록 `DispatcherTimer` 연결.

### Build
- 빌드 경고 0개 / 오류 0개.
