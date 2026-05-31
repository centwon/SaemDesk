# SaemDesk 프로젝트 참조 문서

> **세션 시작 시 참조용.** 앱의 전체 구조, 각 파일의 역할, 메뉴 페이지별 기능을 한 곳에 정리한 문서다.
> 구조가 바뀌면 이 문서도 함께 갱신한다. 빌드/규칙은 [CLAUDE.md](../CLAUDE.md), 마이그레이션 진행은 [MIGRATION_PLAN.md](../MIGRATION_PLAN.md) 참고.

---

## 1. 한눈에 보기

**SaemDesk** — 학교 업무 통합 데스크탑 앱. NewSchool(WinUI3) → Avalonia 12 마이그레이션 산물.

| 항목 | 값 |
|------|-----|
| 스택 | C# / .NET 10 / Avalonia 12.0.4 / CommunityToolkit.Mvvm 8.4 / SQLite(ADO.NET) / Native AOT |
| 버전 | 1.0.0 |
| 플랫폼 | Windows 우선, 차후 크로스플랫폼 |
| 바인딩 | CompiledBinding 전용 (리플렉션 금지, AOT 필수) |
| 주요 라이브러리 | QuestPDF(PDF), MiniExcel·ClosedXML(엑셀), Avalonia.Controls.WebView(Jodit), Semi.Avalonia·Fluent(테마), ProtectedData(DPAPI) |

### 데이터 저장
- **3개 SQLite DB**: `school.db`(학생·수업·학급), `board.db`(게시판), `schedule.db`(일정/할일)
- ORM 없음 — ADO.NET 직접(`Microsoft.Data.Sqlite`) + Repository 패턴
- 사용자 데이터 경로: `Settings.UserDataPath` (DB, `BoardFiles/`, `Prints/`, `Exports/`, 학생 사진 등)
- 비밀정보: `secrets.json`(DPAPI 암호화 OAuth 토큰, git 제외), 빌드 비밀은 `secrets.props` → `BuildSecrets.g.cs` 상수 주입

### 아키텍처 레이어 (의존성: 아래 → 위)
```
Model → Repository → Service → ViewModel → View(Control → Page → Dialog)
```
- **Model**: 순수 도메인 객체 (NEIS 표준 구조)
- **Repository**: DB CRUD (BaseRepository 상속, 비동기·트랜잭션)
- **Service**: 비즈니스 로직, 여러 Repository 조합
- **ViewModel**: `ObservableObject` + `[ObservableProperty]`/`[RelayCommand]`. 다수 페이지 VM은 **필터 상태만** 관리하고 실제 데이터 로드는 컨트롤/code-behind가 담당
- **View**: AXAML + code-behind. 다이얼로그는 커스텀 `Window`(ContentDialog 없음)

### 진입점 & 네비게이션
- [Program.cs](../Program.cs) → [App.axaml.cs](../App.axaml.cs): DI 없이 `ViewLocator.Register()`로 VM↔View 수동 등록(AOT), 플랫폼 서비스 정적 노출
- [Views/MainWindow.axaml](../Views/MainWindow.axaml): 상단 수평 `Menu`. 각 MenuItem의 `Tag` → [MainWindow.axaml.cs](../Views/MainWindow.axaml.cs) `Navigate(tag)` switch → `MainWindowViewModel.CurrentPage` 교체 → `TransitioningContentControl`이 [ViewLocator.cs](../ViewLocator.cs)로 View 생성
- 시작 페이지: `TodayPageVM`(홈)

---

## 2. 메뉴 페이지 기능 명세

> 메뉴 → `Tag` → 페이지 VM 매핑은 [MainWindow.axaml.cs](../Views/MainWindow.axaml.cs) `Navigate()` 기준.

### 🏠 홈 / 📅 달력
| 메뉴 | Tag | 페이지 VM | 기능 |
|------|-----|-----------|------|
| 🏠 홈 | `Home` | TodayPageVM | 오늘 대시보드. 좌(오늘 시간표 + 학사일정 + 급식) / 우(어젠다 + 메모보드). 날짜·인사말·담임 학급 현황 |
| 📅 달력 | `Calendar` | CalendarHomePageVM | 통합 월(月) 달력. 42칸 `DayInfo` 셀, 공휴일/휴업/학사일정 자동 표시, 일정·할일 편집(UnifiedItemDialog), Google Calendar 동기화 |

### 👥 학급
| 메뉴 | Tag | 페이지 VM | 기능 |
|------|-----|-----------|------|
| 📓 학급 일지 | `ClassDiary` | DiaryPageVM | 좌 ListStudent + 우상 ClassDiaryBox(출결·메모·알림장·시간표) + 우하 LogListViewer. 일자별 학급일지 작성 |
| 👤 학생 정보 | `StudentInfo` | StudentInfoPageVM | 좌 ListStudent + 우상 StudentCard(인적·상세정보) + 우하 LogListViewer(활동기록) |
| ✏ 학생 기록 | `StudentLog` | StudentLogPageVM | 누가기록 작성. 좌 ListStudent + 우 LogListViewer. 학년도/학기/학년/반/카테고리 필터 |
| 📋 학생부 관리 | `StudentSpec` | StudentSpecPageVM | 학교생활기록부 특기사항(교과세특·자율·동아리·진로·종합의견). SpecListViewer + NEIS 바이트 카운터 |
| 🪑 자리 배정 | `Seats` | SeatsPageVM | 좌석 격자(Rows×Jul)에 학생 배치. PhotoCard 드래그, 짝 배제/고정 옵션, 자동 배치, PDF/HTML 출력 |
| 📌 학급 게시판 | `ClassBoard` | BoardPageVM | 카테고리 `Homeroom` 고정 게시판(목록/상세/작성) |
| 🖨 학생정보 출력 | `StudentInfoExport` | StudentInfoExportPageVM | 학급 필터 + 출력항목 선택 + JoditEditor 미리보기 + CSV/Excel/프린터 출력 |
| 📤 통합 내보내기 | `UnifiedExport` | UnifiedExportPageVM | 데이터×형식(Excel/PDF/HTML) 조합 학급 단위 일괄 내보내기. 미리보기 후 저장·자동 열기 |
| 🗓 학급 시간표 관리 | `Timetable_ClassManagement` | ClassTimetablePageVM | 학급 관점 시간표 편집(셀 추가/수정, TimetableEditDialog) |

### 📚 수업
| 메뉴 | Tag | 페이지 VM | 기능 |
|------|-----|-----------|------|
| 🏠 수업홈 | `LessonHome` | LessonHomePageVM | 교사 수업 대시보드. TimetableControl(셀 클릭→수업기록) + LessonLogList + KAgendaControl + MemoBoard |
| ✏ 누가 기록 | `LessonActivity` | LessonActivityPageVM | 수업(Course)+강의실 선택 후 수강생 ListStudent + LogListViewer |
| 📋 교과 세특 | `LessonSpec` | LessonSpecPageVM | 수업용 학생부. 카테고리 교과활동 고정, 과목/강의실 필터 |
| 🗓 수업 시간표 | `Timetable_Teacher` | TeacherTimetablePageVM | 교사 본인 시간표 조회 |
| 📌 수업 게시판 | `LessonBoard` | BoardPageVM | 카테고리 `Lesson` 고정 게시판 |
| 📚 수업 관리 | `CourseManagement` | CourseManagementPageVM | 학년도/학기 필터로 Course 조회/추가/편집/삭제, 시간표 배치(CourseScheduleDialog), 수강생 관리 |
| 🎭 동아리 활동 | `ClubActivity` | ClubActivityPageVM | 동아리 선택 후 부원 ListStudent + 활동기록 LogListViewer |

### 💼 업무
| 메뉴 | Tag | 페이지 VM | 기능 |
|------|-----|-----------|------|
| 💼 업무 관리 | `SchoolWork` | SchoolWorkPageVM | 행정업무 대시보드. 어젠다 + 업무메모 + 업무게시판 + 다가오는 학사일정 집계 |
| 📌 업무 게시판 | `WorkBoard` | BoardPageVM | 카테고리 `Work` 고정 게시판 |

### 🗃 아카이브 / ⚙ 설정
| 메뉴 | Tag | 페이지 VM | 기능 |
|------|-----|-----------|------|
| 🗃 아카이브 | `Archive` | BoardPageVM | 전체 카테고리 게시판(카테고리 변경 허용) |
| 🏫 학교 설정 | `Settings_School` | SettingsPageVM | 학교·사용자·학년도 설정 |
| 🗓 학사일정 관리 | `Settings_SchoolSchedule` | SchoolScheduleManagementPageVM | 학사일정 편집(추가/수정/삭제) |
| 👤 학생 관리 | `Settings_Student` | StudentsPageVM | 필터→조회→인라인 편집 표→저장/삭제, 명렬 일괄입력 |
| ⚙ 앱 설정 | `Settings_App` | SettingsPageVM | 앱 환경설정(학교 설정과 동일 VM) |
| ❓ 도움말 | `Help` | HelpPageVM | 정적 도움말 |
| 🔄 업데이트 확인 | `CheckUpdate` | HelpPageVM | (현재 HelpPage로 연결) |

> 게시판 4종(학급/수업/업무/아카이브)은 모두 `BoardPageVM` + `BoardPageParameter`로 카테고리만 다르게 재사용한다.

---

## 3. 파일 명세

### 3.1 루트 / 앱 인프라
| 파일 | 역할 |
|------|------|
| [Program.cs](../Program.cs) | 진입점. Avalonia 앱 빌드/실행 |
| [App.axaml.cs](../App.axaml.cs) | 앱 부트스트랩. ViewLocator 등록, 플랫폼 서비스(FilePicker 등) 정적 노출, DB 초기화 |
| [ViewLocator.cs](../ViewLocator.cs) | AOT-safe VM↔View 매핑. `Register<TVM>(factory)` 수동 딕셔너리 |
| [AppInfo.cs](../AppInfo.cs) | 앱 메타데이터(Version/Product/Company), csproj와 동기화 |
| [Settings.cs](../Settings.cs) | 설정 속성(Fluent API). 캐시 읽기 + 변경 시 저장. 윈도우 상태·학교·사용자·담임학급 |
| [Functions.cs](../Functions.cs) | AOT 호환 유틸. 급식 정보 조회, 현재 교시 계산 |
| [Tools.cs](../Tools.cs) | 유틸. 파일 크기 포맷, NEIS 바이트 카운트(영문1·한글3) |
| [DateTimeHelper.cs](../DateTimeHelper.cs) | ISO 8601/RFC 3339 DateTime 표준 포맷(DB·API 통신용) |
| [DatabaseInitializer.cs](../DatabaseInitializer.cs) | school.db 스키마 초기화(NEIS 표준 구조, FK ON DELETE SET NULL) |
| [SchoolDatabase.cs](../SchoolDatabase.cs) | school.db 관리(초기화·백업·복원) |
| [TrimmerRoots.xml](../TrimmerRoots.xml) | AOT 트리밍 루트 보존 목록 |

### 3.2 Models/ — 도메인 모델 (NEIS 표준)
| 파일 | 모델 |
|------|------|
| [Student.cs](../Models/Student.cs) | 학생 기본 인적정보 |
| [StudentDetail.cs](../Models/StudentDetail.cs) | 학생 상세(보호자·가족·진로·특기), Student와 1:1 |
| [StudentLog.cs](../Models/StudentLog.cs) | 학생 기록부(행동특성·종합의견), 구조화 활동기록 |
| [StudentSpecial.cs](../Models/StudentSpecial.cs) | 생기부 특기사항(NEIS 최종기록, 바이트 제한) |
| [StudentCardData.cs](../Models/StudentCardData.cs) | 좌석 PDF 렌더용 학생 데이터(UI 독립) |
| [Enrollment.cs](../Models/Enrollment.cs) | 학적(학교·학년·반·번호) — 핵심 테이블 |
| [Attendance.cs](../Models/Attendance.cs) | 출결(일별 상태) |
| [School.cs](../Models/School.cs) | 학교 정보 |
| [Teacher.cs](../Models/Teacher.cs) | 교사 정보 |
| [TeacherSchoolHistory.cs](../Models/TeacherSchoolHistory.cs) | 교사 근무이력(학교별) |
| [Course.cs](../Models/Course.cs) | 수업 개설(학년도/학기), 시간표는 분리 |
| [CourseEnrollment.cs](../Models/CourseEnrollment.cs) | 수강 신청 |
| [Lesson.cs](../Models/Lesson.cs) | 수업 시간표(정기/비정기), Course 연결 개별 일정 |
| [LessonLog.cs](../Models/LessonLog.cs) | 수업 일지(교시별 진행기록), IDailyRecord |
| [LessonLogFile.cs](../Models/LessonLogFile.cs) | 수업기록 첨부파일 메타 |
| [ClassDiary.cs](../Models/ClassDiary.cs) | 학급 일지(출결·메모·알림장·생활기록) |
| [ClassTimetable.cs](../Models/ClassTimetable.cs) | 학급 시간표(학생/학급 관점) |
| [Club.cs](../Models/Club.cs) | 동아리 |
| [ClubEnrollment.cs](../Models/ClubEnrollment.cs) | 동아리 부원 배정 |
| [SchoolSchedule.cs](../Models/SchoolSchedule.cs) | 학사일정(NEIS API + DB) |
| [SchoolScheduleGroup.cs](../Models/SchoolScheduleGroup.cs) | 학사일정 그룹(연속날짜 묶음) |
| [SeatArrangement.cs](../Models/SeatArrangement.cs) | 학급별 좌석 배치 메타(SchoolCode·Year·Grade·Class 유일키) |
| [SeatOptions.cs](../Models/SeatOptions.cs) | 좌석 배치 옵션(JSON 저장) |
| [SubjectYearPlan.cs](../Models/SubjectYearPlan.cs) | 연간 수업계획 |
| [WeeklyLessonHours.cs](../Models/WeeklyLessonHours.cs) | 주차별 수업 시수 |
| [GoogleCalendarCheckItem.cs](../Models/GoogleCalendarCheckItem.cs) | Google Calendar 연동 체크 항목 |
| [Base.cs](../Models/Base.cs) | 급식정보 표시 등 기반 모델 |
| [Constants.cs](../Models/Constants.cs) · [LogEnums.cs](../Models/LogEnums.cs) | 카테고리·학적상태·표시모드 등 상수/enum |
| [Interfaces.cs](../Models/Interfaces.cs) · [NotifyPropertyChangedBase.cs](../Models/NotifyPropertyChangedBase.cs) | 엔티티 인터페이스, INPC 기반 클래스 |

### 3.3 Repositories/ — DB 접근
`BaseRepository`(비동기·트랜잭션·에러처리) 상속. 각 모델당 1 Repository로 CRUD 제공.

[BaseRepository](../Repositories/BaseRepository.cs) · [Student](../Repositories/StudentRepository.cs) · [StudentDetail](../Repositories/StudentDetailRepository.cs) · [StudentLog](../Repositories/StudentLogRepository.cs) · [StudentSpecial](../Repositories/StudentSpecialRepository.cs) · [Enrollment](../Repositories/EnrollmentRepository.cs) · [School](../Repositories/SchoolRepository.cs) · [Teacher](../Repositories/TeacherRepository.cs) · [TeacherSchoolHistory](../Repositories/TeacherSchoolHistoryRepository.cs) · [Course](../Repositories/CourseRepository.cs) · [CourseEnrollment](../Repositories/CourseEnrollmentRepository.cs) · [Lesson](../Repositories/LessonRepository.cs) · [LessonLog](../Repositories/LessonLogRepository.cs) · [LessonLogFile](../Repositories/LessonLogFileRepository.cs) · [ClassDiary](../Repositories/ClassDiaryRepository.cs) · [ClassTimetable](../Repositories/ClassTimetableRepository.cs) · [Club](../Repositories/ClubRepository.cs) · [ClubEnrollment](../Repositories/ClubEnrollmentRepository.cs) · [SchoolSchedule](../Repositories/SchoolScheduleRepository.cs) · [SubjectYearPlan](../Repositories/SubjectYearPlanRepository.cs) · [WeeklyLessonHours](../Repositories/WeeklyLessonHoursRepository.cs)

### 3.4 Services/ — 비즈니스 로직
| 파일 | 역할 |
|------|------|
| [StudentService.cs](../Services/StudentService.cs) | 학생 통합 관리(Student+Enrollment+Detail 일괄) |
| [StudentDetailService.cs](../Services/StudentDetailService.cs) | 학생 상세정보 로직 |
| [StudentLogService.cs](../Services/StudentLogService.cs) | 누가기록(Repository 패턴, 연결 재사용) |
| [StudentSpecialService.cs](../Services/StudentSpecialService.cs) | 생기부 특기사항(마감 시 수정/삭제 거부) |
| [EnrollmentService.cs](../Services/EnrollmentService.cs) | 학적 관리(학급 배정) |
| [SchoolService.cs](../Services/SchoolService.cs) | 학교 정보·통계(Upsert) |
| [TeacherService.cs](../Services/TeacherService.cs) | 교사 + 근무이력 |
| [CourseService.cs](../Services/CourseService.cs) | 교사 과목 목록 등 |
| [EnrollmentService.cs](../Services/EnrollmentService.cs) | 학적 비즈니스 로직 |
| [LessonService.cs](../Services/LessonService.cs) | 시간표·수업 진행 관리 |
| [LessonLogService.cs](../Services/LessonLogService.cs) | 수업기록 CRUD |
| [ClassDiaryService.cs](../Services/ClassDiaryService.cs) | 학급일지(StudentLog 연동 Life 자동생성) |
| [ClubService.cs](../Services/ClubService.cs) | 동아리 관리 |
| [SchoolScheduleService.cs](../Services/SchoolScheduleService.cs) | 학사일정 + NEIS API 연동 |
| [TimetableService.cs](../Services/TimetableService.cs) | 교사/학급 시간표 조회·VM 변환 |
| [SeatService.cs](../Services/SeatService.cs) | 좌석 배치 저장/로드/이력(짝 배제용) |
| [StudentDetailService.cs](../Services/StudentDetailService.cs) | 상세정보 |
| [PhotoService.cs](../Services/PhotoService.cs) | 학생 사진 경로 관리(상대→절대) |
| [DialogService.cs](../Services/DialogService.cs) | 모달 다이얼로그 헬퍼(ContentDialog 대체) |
| [SecretsService.cs](../Services/SecretsService.cs) | API키·OAuth 자격증명 제공(BuildSecrets.g.cs) |
| [WeeklyHoursHelper.cs](../Services/WeeklyHoursHelper.cs) | 주차별 시수 수정 유틸 |
| **내보내기/출력** | |
| [UnifiedExportService.cs](../Services/UnifiedExportService.cs) | 데이터×형식 조합 단일파일 생성(기존 서비스 위임) |
| [CsvExportService.cs](../Services/CsvExportService.cs) | CSV(UTF-8 BOM, RFC 4180) |
| [HtmlExportService.cs](../Services/HtmlExportService.cs) | 누가기록·학생부 HTML(브라우저 PDF 가능) |
| [StudentLogExportService.cs](../Services/StudentLogExportService.cs) · [StudentLogPrintService.cs](../Services/StudentLogPrintService.cs) | 누가기록 엑셀/PDF |
| [StudentSpecExportService.cs](../Services/StudentSpecExportService.cs) · [StudentSpecPrintService.cs](../Services/StudentSpecPrintService.cs) | 학생부 엑셀/PDF |
| [LessonLogExportService.cs](../Services/LessonLogExportService.cs) · [LessonLogPrintService.cs](../Services/LessonLogPrintService.cs) | 수업기록 진도표/기간일지 엑셀·PDF |
| [SeatsPrintService.cs](../Services/SeatsPrintService.cs) | 좌석배정표 PDF/HTML |
| [StudentCardPrintService.cs](../Services/StudentCardPrintService.cs) | 학생카드 PDF |
| [SeatOptionsJsonContext.cs](../Services/SeatOptionsJsonContext.cs) | SeatOptions AOT JSON 컨텍스트 |

#### Services/Platform/ — 플랫폼 추상화
| 인터페이스 | Windows 구현 |
|-----------|-------------|
| [ICryptoService](../Services/Platform/ICryptoService.cs) | [WindowsDpApiCryptoService](../Services/Platform/Windows/WindowsDpApiCryptoService.cs) (DPAPI) |
| [IFilePickerService](../Services/Platform/IFilePickerService.cs) | [AvaloniaFilePickerService](../Services/Platform/Windows/AvaloniaFilePickerService.cs) (StorageProvider) |
| [IKoreanImeService](../Services/Platform/IKoreanImeService.cs) | [WindowsKoreanImeService](../Services/Platform/Windows/WindowsKoreanImeService.cs) + [KoreanImeHelper](../Services/Platform/Windows/KoreanImeHelper.cs) (imm32 P/Invoke) |

### 3.5 ViewModels/
**Pages/** — 페이지 VM. 대부분 필터 상태만 관리, 데이터 로드는 컨트롤/code-behind.
[Today](../ViewModels/Pages/TodayPageVM.cs) · [CalendarHome](../ViewModels/Pages/CalendarHomePageVM.cs) · [Calendar](../ViewModels/Pages/CalendarPageVM.cs) · [Diary](../ViewModels/Pages/DiaryPageVM.cs) · [StudentInfo](../ViewModels/Pages/StudentInfoPageVM.cs) · [StudentLog](../ViewModels/Pages/StudentLogPageVM.cs) · [StudentSpec](../ViewModels/Pages/StudentSpecPageVM.cs) · [Seats](../ViewModels/Pages/SeatsPageVM.cs) · [Board](../ViewModels/Pages/BoardPageVM.cs) · [StudentInfoExport](../ViewModels/Pages/StudentInfoExportPageVM.cs) · [UnifiedExport](../ViewModels/Pages/UnifiedExportPageVM.cs) · [ClassTimetable](../ViewModels/Pages/ClassTimetablePageVM.cs) · [LessonHome](../ViewModels/Pages/LessonHomePageVM.cs) · [LessonActivity](../ViewModels/Pages/LessonActivityPageVM.cs) · [LessonSpec](../ViewModels/Pages/LessonSpecPageVM.cs) · [Lessons](../ViewModels/Pages/LessonsPageVM.cs) · [TeacherTimetable](../ViewModels/Pages/TeacherTimetablePageVM.cs) · [CourseManagement](../ViewModels/Pages/CourseManagementPageVM.cs) · [ClubActivity](../ViewModels/Pages/ClubActivityPageVM.cs) · [ClubHome](../ViewModels/Pages/ClubHomePageVM.cs) · [ClubManagement](../ViewModels/Pages/ClubManagementPageVM.cs) · [SchoolWork](../ViewModels/Pages/SchoolWorkPageVM.cs) · [Scheduler](../ViewModels/Pages/SchedulerPageVM.cs) · [SchoolScheduleManagement](../ViewModels/Pages/SchoolScheduleManagementPageVM.cs) · [Settings](../ViewModels/Pages/SettingsPageVM.cs) · [Help](../ViewModels/Pages/HelpPageVM.cs) · [AddStudents](../ViewModels/Pages/AddStudentsPageVM.cs) · [Students](../ViewModels/Pages/StudentsPageVM.cs) · [StudentManagement](../ViewModels/Pages/StudentManagementViewModel.cs)

**루트 VM** — 컨트롤/항목 바인딩용 래퍼:
[MainWindow](../ViewModels/MainWindowViewModel.cs)(페이지 호스트·타이틀) · [ViewModelBase](../ViewModels/ViewModelBase.cs) · [ClassDiary](../ViewModels/ClassDiaryViewModel.cs) · [StudentCard](../ViewModels/StudentCardViewModel.cs) · [StudentListItem](../ViewModels/StudentListItemViewModel.cs) · [StudentLog](../ViewModels/StudentLogViewModel.cs) · [StudentSpecial](../ViewModels/StudentSpecialViewModel.cs) · [Timetable](../ViewModels/TimetableViewModel.cs) · [SchoolSchedule](../ViewModels/SchoolScheduleViewModel.cs) · [SemesterIndexConverter](../ViewModels/SemesterIndexConverter.cs)

### 3.6 Views/Controls/ — 재사용 컨트롤
| 컨트롤 | 역할 |
|--------|------|
| [ListStudent](../Views/Controls/ListStudent.axaml.cs) | 학생 목록(ListBox, Enrollment 직접). 여러 페이지 공용 |
| [StudentCard](../Views/Controls/StudentCard.axaml.cs) | 학생 카드(인적+상세) |
| [PhotoCard](../Views/Controls/PhotoCard.axaml.cs) | 좌석 카드. 사진 비동기 로딩, 드래그 수신 |
| [LogListViewer](../Views/Controls/LogListViewer.axaml.cs) | 학생 기록 목록. 모드별 컬럼 토글 |
| [SpecListViewer](../Views/Controls/SpecListViewer.axaml.cs) | 학생부 특기사항 목록. 다중선택 |
| [ClassDiaryBox](../Views/Controls/ClassDiaryBox.axaml.cs) | 학급일지 입력(출결+메모+알림장 JoditEditor+시간표) |
| [StudentLogBox](../Views/Controls/StudentLogBox.axaml.cs) | 학생 활동기록 입력 폼 |
| [StudentSpecBox](../Views/Controls/StudentSpecBox.axaml.cs) | 특이사항 편집. NEIS 바이트 카운터, 맞춤법 링크 |
| [LessonLogList](../Views/Controls/LessonLogList.axaml.cs) | 수업기록 목록 |
| [MemoBoard](../Views/Controls/MemoBoard.axaml.cs) | 포스트잇 메모보드(Masonry 2열, 인라인 편집) |
| [KAgendaControl](../Views/Controls/KAgendaControl.axaml.cs) | 할일+일정 통합 어젠다 |
| [TimetableControl](../Views/Controls/TimetableControl.axaml.cs) | 시간표 그리드(교사/학급 모드) |
| [SchoolMealBox](../Views/Controls/SchoolMealBox.axaml.cs) | 급식 정보 박스 |
| [SchoolScheduleListControl](../Views/Controls/SchoolScheduleListControl.axaml.cs) | 학사일정 목록 |
| [DayCell](../Views/Controls/DayCell.axaml.cs) | 달력 날짜 셀(DayInfo 바인딩) |
| [InfoBar](../Views/Controls/InfoBar.axaml.cs) | WinUI InfoBar 대체(심각도별 색+닫기) |
| [JoditEditor](../Views/Controls/JoditEditor.axaml.cs) | Jodit HTML 에디터(NativeWebView 기반) |
| [JoditEditorWin](../Views/Controls/JoditEditorWin.axaml.cs) | JoditEditor 모달 창 래퍼 |
| **필터/피커** | [ClassPicker](../Views/Controls/ClassPicker.axaml.cs)(학년·반) · [CoursePicker](../Views/Controls/CoursePicker.axaml.cs)(과목·강의실) · [YearSemesterPicker](../Views/Controls/YearSemesterPicker.axaml.cs) · [CompactTimePicker](../Views/Controls/CompactTimePicker.axaml.cs) |
| **컨버터** | [HexToBrush](../Views/Controls/HexToBrushConverter.cs) · [PathToBitmap](../Views/Controls/PathToBitmapConverter.cs) · [BoolToVacationColor](../Views/Controls/BoolToVacationColorConverter.cs) · [MemoBoardConverters](../Views/Controls/MemoBoardConverters.cs) · [SimpleHtmlRenderer](../Views/Controls/SimpleHtmlRenderer.cs)(메모 미리보기 HTML→컨트롤) |
| **AOT JSON** | [JoditEditorJsonContext](../Views/Controls/JoditEditorJsonContext.cs) |

### 3.7 Views/Dialogs/ — 다이얼로그 (커스텀 Window)
| 다이얼로그 | 역할 |
|-----------|------|
| [ConfirmDialog](../Views/Dialogs/ConfirmDialog.axaml.cs) | 범용 확인/취소 |
| [InitialSetupDialog](../Views/Dialogs/InitialSetupDialog.axaml.cs) | 초기 설정(학교검색→사용자정보→학년도, 3단계) |
| [SchoolSearchDialog](../Views/Dialogs/SchoolSearchDialog.axaml.cs) | 학교 검색(NEIS API) |
| [StudentEditDialog](../Views/Dialogs/StudentEditDialog.axaml.cs) | 학생 추가/수정 |
| [StudentDetailDialog](../Views/Dialogs/StudentDetailDialog.axaml.cs) | 학생 상세 보기(읽기전용 2탭) |
| [BulkStudentInfoPreviewDialog](../Views/Dialogs/BulkStudentInfoPreviewDialog.axaml.cs) | 학생 일괄입력 미리보기 |
| [StudentLogDialog](../Views/Dialogs/StudentLogDialog.axaml.cs) · [StudentLogEditDialog](../Views/Dialogs/StudentLogEditDialog.axaml.cs) · [StudentLogBatchDialog](../Views/Dialogs/StudentLogBatchDialog.axaml.cs) | 학생기록 입력/편집/일괄 |
| [StudentSpecBatchDialog](../Views/Dialogs/StudentSpecBatchDialog.axaml.cs) | 학생부 특기사항 일괄(에디터+누가기록 참조+초안 생성) |
| [DiaryEditDialog](../Views/Dialogs/DiaryEditDialog.axaml.cs) · [ClassDiaryListWin](../Views/Dialogs/ClassDiaryListWin.axaml.cs) | 학급일지 작성/목록 |
| [CourseEditDialog](../Views/Dialogs/CourseEditDialog.axaml.cs) · [CourseScheduleDialog](../Views/Dialogs/CourseScheduleDialog.axaml.cs) · [CourseEnrollmentDialog](../Views/Dialogs/CourseEnrollmentDialog.axaml.cs) | 수업 편집/시간표/수강생 |
| [LessonLogEditDialog](../Views/Dialogs/LessonLogEditDialog.axaml.cs) · [LessonExportDialog](../Views/Dialogs/LessonExportDialog.axaml.cs) | 수업 차시기록/내보내기 |
| [TimetableEditDialog](../Views/Dialogs/TimetableEditDialog.axaml.cs) | 시간표 칸 추가/수정 |
| [ClubEditDialog](../Views/Dialogs/ClubEditDialog.axaml.cs) · [ClubEnrollmentDialog](../Views/Dialogs/ClubEnrollmentDialog.axaml.cs) | 동아리 편집/부원 |
| [SeatOptionsDialog](../Views/Dialogs/SeatOptionsDialog.axaml.cs) · [SeatExclusionDialog](../Views/Dialogs/SeatExclusionDialog.axaml.cs) · [SeatPrintOptionsDialog](../Views/Dialogs/SeatPrintOptionsDialog.axaml.cs) | 좌석 옵션/짝배제/인쇄 |
| [CalendarSettingsDialog](../Views/Dialogs/CalendarSettingsDialog.axaml.cs) | 일정 설정(Google 연동·동기화·학사일정 일괄) |
| [UnifiedItemDialog](../Views/Dialogs/UnifiedItemDialog.axaml.cs) | 할일/일정 통합 편집(KEvent) |
| [MemoEditDialog](../Views/Dialogs/MemoEditDialog.axaml.cs) | 메모 편집 |
| [PostDetailDialog](../Views/Dialogs/PostDetailDialog.axaml.cs) | 게시글 상세/수정(한 창 교체) |
| [RosterTableDialog](../Views/Dialogs/RosterTableDialog.axaml.cs) | 명렬표 HTML 테이블 삽입 |
| [ExportDialog](../Views/Dialogs/ExportDialog.axaml.cs) · [BatchExportFilterDialog](../Views/Dialogs/BatchExportFilterDialog.axaml.cs) · [SpecExportFilterDialog](../Views/Dialogs/SpecExportFilterDialog.axaml.cs) · [StudentPrintOptionsDialog](../Views/Dialogs/StudentPrintOptionsDialog.axaml.cs) | 내보내기/출력 필터·옵션 |

### 3.8 Board/ — 게시판 (board.db)
| 파일 | 역할 |
|------|------|
| [BoardDatabase.cs](../Board/BoardDatabase.cs) | DB 경로·스키마·파일 경로 헬퍼·서비스 팩토리 |
| [BoardDefaults.cs](../Board/BoardDefaults.cs) | 기본 카테고리/주제(목록·작성 공용) |
| Models | [Post](../Board/Models/Post.cs) · [Comment](../Board/Models/Comment.cs) · [PostFile](../Board/Models/PostFile.cs) · [BoardViewMode](../Board/Models/BoardViewMode.cs) |
| Repositories | [PostRepository](../Board/Repositories/PostRepository.cs) · [CommentRepository](../Board/Repositories/CommentRepository.cs) · [PostFileRepository](../Board/Repositories/PostFileRepository.cs) |
| Services | [BoardService](../Board/Services/BoardService.cs)(CRUD+물리파일+PagedResult) · [BoardUnitOfWork](../Board/Services/BoardUnitOfWork.cs) |
| ViewModels | [PostListViewModel](../Board/ViewModels/PostListViewModel.cs) · [PostDetailViewModel](../Board/ViewModels/PostDetailViewModel.cs) |
| Views | [PostListPage](../Board/Views/Pages/PostListPage.axaml.cs)(표/카드/갤러리·필터·페이징) · [PostDetailPage](../Board/Views/Pages/PostDetailPage.axaml.cs) · [PostEditPage](../Board/Views/Pages/PostEditPage.axaml.cs) · [PostFileListBox](../Board/Views/Controls/PostFileListBox.axaml.cs) · [BoardPageParameters](../Board/Views/Pages/BoardPageParameters.cs) |
| 컨테이너 | [BoardPage](../Views/Pages/BoardPage.axaml.cs) — 목록/상세/작성 이벤트 기반 전환(Frame 대체) |

### 3.9 Scheduler/ — 일정·할일 (schedule.db)
| 파일 | 역할 |
|------|------|
| [Scheduler.cs](../Scheduler/Scheduler.cs) | 스케줄러 핵심(Ktask→KEvent 통합) |
| [SchedulerService.cs](../Scheduler/SchedulerService.cs) | 비즈니스 로직(모든 task = KEvent ItemType="task") |
| [DatabaseInitializer.cs](../Scheduler/DatabaseInitializer.cs) | schedule.db 초기화 |
| [UnitOfWork.cs](../Scheduler/UnitOfWork.cs) | 단일 Connection+Transaction 원자성 |
| [KEvent.cs](../Scheduler/KEvent.cs) · [KEventRepository.cs](../Scheduler/KEventRepository.cs) | 일정/할일 모델·DB(Google Events 대응) |
| [KCalendarList.cs](../Scheduler/KCalendarList.cs) · [KCalendarListRepository.cs](../Scheduler/KCalendarListRepository.cs) | 캘린더 목록(카테고리+색상) |
| [AgendaItem.cs](../Scheduler/AgendaItem.cs) | 어젠다 행 VM(KAgendaControl용) |
| [DayInfo.cs](../Scheduler/DayInfo.cs) | 달력 한 칸 정보(공휴일/휴업/날짜명 산출) |
| [SchedulerEvents.cs](../Scheduler/SchedulerEvents.cs) | 변경 알림 정적 이벤트 버스(화면 간 새로고침) |
| [SchedulerService.cs](../Scheduler/SchedulerService.cs) | 작업 생성·관리 |

### 3.10 Google/ — OAuth + Calendar 연동
| 파일 | 역할 |
|------|------|
| [GoogleAuthService.cs](../Google/GoogleAuthService.cs) | OAuth 2.0(Loopback+PKCE), 토큰 DPAPI 암호화 |
| [GoogleCalendarApiClient.cs](../Google/GoogleCalendarApiClient.cs) | Calendar REST v3 클라이언트(HttpClient+STJ) |
| [GoogleSyncService.cs](../Google/GoogleSyncService.cs) | 양방향 동기화(Pull/Push, syncToken 증분, 할일 통합) |
| [GoogleCalendarModels.cs](../Google/GoogleCalendarModels.cs) · [GoogleCalendarJsonContext.cs](../Google/GoogleCalendarJsonContext.cs) | 모델 + AOT JSON 컨텍스트 |
| [SyncResult.cs](../Google/SyncResult.cs) | 동기화 결과 |

### 3.11 기타 모듈
| 폴더/파일 | 역할 |
|-----------|------|
| [Helpers/ExcelHelpers.cs](../Helpers/ExcelHelpers.cs) · [ExcelReader.cs](../Helpers/ExcelReader.cs) | Excel 읽기/쓰기(MiniExcel, AOT) |
| [Helpers/NeisHelper.cs](../Helpers/NeisHelper.cs) | NEIS 바이트 계산·영역별 글자수 제한 |
| [Helpers/TextBoxDropHelper.cs](../Helpers/TextBoxDropHelper.cs) | TextBox 드래그앤드롭 텍스트 수신 |
| [Behaviors/HangulBehavior.cs](../Behaviors/HangulBehavior.cs) | `UseHangul` 첨부속성→포커스 시 한글 IME 전환 |
| [Logging/FileLogger.cs](../Logging/FileLogger.cs) | 비동기 파일 로거(AOT) |
| [Collections/OptimizedObservableCollection.cs](../Collections/OptimizedObservableCollection.cs) | 대량 업데이트 최적화 컬렉션 |
| [Converters/DoubleToGridLengthConverter.cs](../Converters/DoubleToGridLengthConverter.cs) | double→GridLength |
| [Assets/Jodit/](../Assets/Jodit/) | Jodit 에디터 HTML/JS/CSS + DOMPurify |
| [Assets/Styles/](../Assets/Styles/) | Colors.axaml · CommonStyles.axaml |

### 3.12 SaemDesk.HtmlEditor/ (별도 프로젝트)
Avalonia 네이티브 HTML 에디터 — Jodit/WebView 의존 없는 자체 구현 실험. 세부는 [PROJECT.md](../SaemDesk.HtmlEditor/PROJECT.md).
[HtmlEditorControl](../SaemDesk.HtmlEditor/HtmlEditorControl.axaml.cs) · [EditorToolbar](../SaemDesk.HtmlEditor/Controls/EditorToolbar.axaml.cs) · [EditArea](../SaemDesk.HtmlEditor/Editing/EditArea.cs) · [HtmlParser](../SaemDesk.HtmlEditor/Parsing/HtmlParser.cs) · [HtmlSerializer](../SaemDesk.HtmlEditor/Parsing/HtmlSerializer.cs) · [HtmlRenderer](../SaemDesk.HtmlEditor/Rendering/HtmlRenderer.cs) · TestApp 포함.

---

## 4. 새 기능 추가 체크리스트
1. **Model** 추가 → 필요 시 `JsonSerializerContext` 등록
2. **Repository** → `BaseRepository` 상속, DatabaseInitializer에 스키마 추가
3. **Service** → 비즈니스 로직
4. **ViewModel** → `ObservableObject`, `[ObservableProperty]`/`[RelayCommand]`
5. **View** → AXAML(CompiledBinding) + code-behind. `Activator.CreateInstance` 금지
6. **ViewLocator.Register()** (App.axaml.cs)에 VM↔View 등록 — AOT 필수
7. 새 페이지면 [MainWindow.axaml](../Views/MainWindow.axaml) 메뉴 + [MainWindow.axaml.cs](../Views/MainWindow.axaml.cs) `Navigate()` switch 추가
8. `dotnet build` 경고 0 확인 → 필요 시 `TrimmerRoots.xml` 보강

---

*이 문서는 코드 주석(`<summary>`)과 메뉴 네비게이션 기준으로 생성. 파일 추가/삭제 시 함께 갱신할 것.*
