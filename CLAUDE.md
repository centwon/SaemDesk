# SaemDesk — CLAUDE.md

## 프로젝트 개요
NewSchool (WinUI3) → SaemDesk (Avalonia 12) 마이그레이션 프로젝트.  
학교 업무 통합 데스크탑 앱 (학생·수업·학급일지·게시판·스케줄러·NEIS·Google Calendar).

- **스택**: C# / .NET 10 / Avalonia 12.0.1 / CommunityToolkit.Mvvm / SQLite (ADO.NET) / Native AOT
- **버전**: 1.0.0
- **플랫폼**: Windows 우선, 차후 크로스플랫폼 예정

> 📖 **전체 파일 명세 · 메뉴 페이지 기능**: [docs/PROJECT_REFERENCE.md](docs/PROJECT_REFERENCE.md) — 세션 시작 시 구조 파악용 참조.

## 폴더 구조
```
SaemDesk/
├── Models/           # 도메인 모델 (변경 없음)
├── Repositories/     # DB 접근 (ADO.NET 직접)
├── Services/         # 비즈니스 로직
│   └── Platform/     # ICryptoService, IFilePickerService, IKoreanImeService 추상화
├── ViewModels/       # CommunityToolkit.Mvvm (ObservableObject, RelayCommand)
├── Views/            # Avalonia XAML (Pages / Dialogs / Controls)
├── Board/            # 게시판 기능
├── Scheduler/        # Google Calendar 연동
├── Google/           # OAuth + Calendar REST API
├── Helpers/
├── Logging/
├── Caching/
├── Collections/
├── Converters/
└── Assets/           # 폰트, 아이콘, Jodit 에디터 파일
```

## 핵심 규칙

### Avalonia / MVVM
- **바인딩은 CompiledBinding만** (`AvaloniaUseCompiledBindingsByDefault=true`) — 리플렉션 바인딩 금지 (AOT 필수)
- ViewModel: `ObservableObject` 상속, `[RelayCommand]` / `[ObservableProperty]` 사용
- UI 스레드 전환: `Dispatcher.UIThread.InvokeAsync(...)` (DispatcherQueue 아님)
- 파일 피커: `StorageProvider` (IFilePickerService 경유)
- 다이얼로그: 커스텀 `DialogBase` Window 기반 (ContentDialog 없음)

### Native AOT
- `Activator.CreateInstance` 금지 → ViewLocator에 수동 딕셔너리 등록
- JSON 직렬화 신규 타입 추가 시 `JsonSerializerContext`에 반드시 등록
- AOT 문제 발생 시 `TrimmerRoots.xml`에 루트 추가

### DB
- SQLite 3개: `school.db`, `board.db`, `schedule.db`
- ORM 없음 — ADO.NET 직접 (`Microsoft.Data.Sqlite`)
- Repository 패턴 사용

### 플랫폼 추상화 인터페이스
| 인터페이스 | Windows 구현 |
|-----------|------------|
| `ICryptoService` | DPAPI (`ProtectedData`) |
| `IFilePickerService` | Avalonia `StorageProvider` |
| `IKoreanImeService` | imm32.dll P/Invoke |

### WinUI3 → Avalonia 주요 치환
| WinUI3 | Avalonia |
|--------|----------|
| `Page` | `UserControl` |
| `ListView` | `ListBox` |
| `ContentDialog` | 커스텀 DialogBase |
| `NavigationView` | `SplitView` + 커스텀 |
| `NumberBox` | `NumericUpDown` |
| `InfoBar` | 커스텀 UserControl |
| `ProgressRing` | `ProgressBar IsIndeterminate` |
| `{ThemeResource}` | `{DynamicResource}` |
| `x:Bind` | `{CompiledBinding}` |

## UI 이식 순서 원칙

의존성 방향을 따라 **아래에서 위로** 이식한다. 상위 레이어는 하위 레이어가 완성된 후 시작.

```
Model → ViewModel → 컨트롤(독립→ViewModel의존→WebView) → 페이지(단순→복잡) → 다이얼로그(단순→복잡)
```

| 순서 | 레이어 | 이유 |
|------|--------|------|
| 1 | Model | 의존성 없음 |
| 2 | ViewModel | Model만 의존, 바인딩 오류를 컴파일 타임에 잡음 |
| 3 | 컨트롤 — 독립형 | ViewModel 없음 (InfoBar, MonthPicker 등) |
| 4 | 컨트롤 — ViewModel 의존 | MemoBoard, KAgendaControl |
| 5 | 컨트롤 — WebView | JoditEditor (AOT 검증 포함, 별도 테스트) |
| 6 | 페이지 — 단순 | 외부 의존 없음 (HelpPage, SettingsPage, TodayPage) |
| 7 | 페이지 — 중간 | 학생·수업·동아리·내보내기 |
| 8 | 페이지 — 복잡 | 스케줄러·게시판 (JoditEditor/Calendar 포함) |
| 9 | 다이얼로그 — 단순 | ConfirmDialog, SchoolSearchDialog 등 독립적 |
| 10 | 다이얼로그 — 중간 | ViewModel 의존 (Student/Course/Diary 관련) |
| 11 | 다이얼로그 — 복잡 | JoditEditor 포함 (PostEditDialog, UnifiedItemDialog) |

> **다이얼로그를 페이지보다 나중에** 하는 이유: 다이얼로그는 컨트롤·페이지에 의존하는 경우가 많아 먼저 작업하면 반쪽짜리가 된다.

## 빌드
```bash
dotnet build                          # 디버그 빌드
dotnet publish -r win-x64 -c Release  # Native AOT 퍼블리시
```
경고 0 유지 목표. `AVLN3001`은 의도적으로 억제 중.

## AI 코딩 행동 원칙 (Karpathy Guidelines)

> 단순 작업에는 판단 우선. 이 원칙들은 속도보다 신중함을 우선시함.

### 1. 코딩 전에 먼저 생각하기
- 가정을 명시적으로 밝힌다. 불확실하면 질문한다.
- 해석이 여러 가지면 제시하고 고른다 — 조용히 하나를 선택하지 않는다.
- 더 단순한 접근이 있으면 말한다. 필요하면 반대 의견을 낸다.
- 모호한 게 있으면 멈추고, 뭐가 헷갈리는지 명확히 말하고 질문한다.

### 2. 단순함 우선
- 문제를 해결하는 최소한의 코드만 작성한다.
- 요청하지 않은 기능, 추상화, 유연성, 설정 옵션은 추가하지 않는다.
- 불가능한 시나리오에 대한 에러 핸들링은 넣지 않는다.
- 200줄로 쓴 코드가 50줄로 가능하면 다시 쓴다.
- "시니어 개발자가 보면 과하다고 할까?" — Yes면 단순화한다.

### 3. 외과적 수정 (Surgical Changes)
- 요청된 것만 수정한다. 인접 코드·주석·포맷을 "개선"하지 않는다.
- 동작하는 코드는 리팩토링하지 않는다.
- 기존 스타일을 그대로 따른다 (내 방식이 더 좋아도).
- 내 변경으로 생긴 미사용 import/변수/함수는 정리한다.
- 기존에 있던 dead code는 언급만 하고 건드리지 않는다.
- 모든 변경 줄은 요청으로 직접 추적 가능해야 한다.

### 4. 목표 중심 실행
- 성공 기준을 먼저 정의하고, 검증될 때까지 반복한다.
- "버그 수정" → "재현 테스트 작성 후 통과시키기"
- "리팩토링" → "전후 테스트 통과 확인"
- 여러 단계 작업은 간략한 계획을 먼저 제시한다:
  ```
  1. [작업] → 검증: [확인 방법]
  2. [작업] → 검증: [확인 방법]
  ```

## 주의사항
- `secrets.props` — git 제외. 빌드 시 `GoogleClientId`/`GoogleClientSecret`/`NeisApiKey` 를 `BuildSecrets.g.cs` 상수로 주입 (SecretsService 가 읽는 실제 출처). 변경 후 재빌드 필요.
- `secrets.props.template` — 구조 참고용
- OAuth 토큰(access/refresh) 은 Settings DB 에 DPAPI 암호화 저장 (GoogleAuthService)
- QuestPDF / MiniExcel / Avalonia.WebView → AOT 호환성 확인 필요 (Phase 6)
