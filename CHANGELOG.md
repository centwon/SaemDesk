# SaemDesk 변경 이력

모든 주목할 만한 변경 사항은 이 문서에 기록됩니다.
형식: [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/) 기준, 버전 표기는 [SemVer](https://semver.org/lang/ko/) 를 따릅니다.

---

## [Unreleased]

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
