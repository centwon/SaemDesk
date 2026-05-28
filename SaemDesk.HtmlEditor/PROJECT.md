# SaemDesk.HtmlEditor

## 프로젝트 개요

SaemDesk 학교 업무 통합 앱에서 사용하는 **Avalonia 기반 경량 WYSIWYG HTML 편집기** 컨트롤 라이브러리.  
게시판 글 작성, 학급일지 등에서 서식이 있는 텍스트를 편집하고 HTML로 저장/로드할 수 있다.

- **스택**: C# / .NET 10 / Avalonia 12.0.3
- **버전**: 0.1.0-alpha
- **의존성**: Avalonia, Avalonia.Themes.Fluent (외부 에디터 라이브러리 없음)
- **AOT 호환**: CompiledBinding 전용, 리플렉션 미사용

## 핵심 설계

### 문서 모델 (2계층 트리)

```
HtmlDocument
  └─ Blocks: List<BlockNode>
       ├─ ParagraphBlock (Tag: p/h1/h2/h3, Alignment, Inlines)
       ├─ ImageBlock (Source: base64 data URI, Width, Height)
       └─ TableBlock
            └─ Rows → Cells → Content: List<BlockNode>

InlineNode (ParagraphBlock.Inlines)
  ├─ TextRun (Text)
  ├─ LineBreakNode
  └─ FormattedSpan (Bold, Italic, Underline, Strikethrough, Color, FontSize, Href)
       └─ Children: List<InlineNode>  (중첩 가능)
```

### 편집 방식 (블록 단위 클릭-편집)

Avalonia에는 내장 RichTextBox가 없으므로 자체 블록 편집 방식을 사용한다:

1. **뷰 모드**: 블록을 Avalonia 컨트롤(TextBlock, Image, Grid)로 렌더링
2. **편집 모드**: 블록/셀 클릭 시 해당 위치만 TextBox로 전환
3. **커밋**: 포커스 이탈 시 텍스트를 모델에 동기화하고 렌더링 뷰로 복귀

### 포커스 관리 전략

- 툴바 버튼: `Focusable="False"` — 클릭해도 TextBox 포커스 유지
- Flyout 메뉴(크기/색상): 팝업 내부 포커스 시 커밋 방지 (`IsInPopup` 체크)
- 선택 보존: TextBox 선택 변경 시 `LastSelection`에 자동 저장 → 포커스 손실 후에도 서식 적용 가능
- 서식 적용 시 3단계 fallback: ① 현재 활성 선택 → ② LastSelection → ③ Tunnel 저장값

## 폴더 구조

```
SaemDesk.HtmlEditor/
├── Models/
│   └── HtmlDocument.cs          # 문서 모델 (BlockNode, InlineNode, HtmlDocument)
├── Parsing/
│   ├── HtmlParser.cs            # HTML → 문서 모델 (커스텀 토크나이저 + 재귀 파서)
│   └── HtmlSerializer.cs        # 문서 모델 → HTML 문자열
├── Rendering/
│   └── HtmlRenderer.cs          # 문서 모델 → Avalonia 컨트롤 트리
├── Editing/
│   ├── EditArea.cs              # 블록/셀 단위 편집 영역 (클릭-편집 전환)
│   ├── FormatCommand.cs         # 서식 적용 명령 (인라인 분할, 정렬, 삽입)
│   └── DocumentCursor.cs        # 문서 위치/선택 범위 (DocPosition, DocSelection)
├── Controls/
│   ├── EditorToolbar.axaml      # 툴바 XAML (버튼 배치)
│   └── EditorToolbar.axaml.cs   # 툴바 로직 (서식 적용, Flyout 생성)
├── HtmlEditorControl.axaml      # 메인 컨트롤 (Toolbar + EditArea 조합)
├── HtmlEditorControl.axaml.cs   # Html StyledProperty (양방향 바인딩)
└── HtmlInlineParser.cs          # (레거시, 제거 예정)

SaemDesk.HtmlEditor.TestApp/     # 독립 테스트 앱
├── MainWindow.axaml(.cs)        # HtmlEditorControl + HTML 출력 확인
└── ...
```

## 구현 완료 기능

| 기능 | 설명 | 상태 |
|------|------|------|
| **굵게 (Bold)** | 선택 텍스트에 `<strong>` 적용 | ✅ 완료 |
| **기울임 (Italic)** | 선택 텍스트에 `<em>` 적용 | ✅ 완료 |
| **글꼴 크기** | MenuFlyout으로 10~48px 선택 | ✅ 완료 |
| **글자 색상** | MenuFlyout으로 9가지 색상 선택 | ✅ 완료 |
| **문단 정렬** | 좌/중/우 정렬 (`text-align`) | ✅ 완료 |
| **이미지 삽입** | 파일 선택 → base64 data URI 변환 | ✅ 완료 |
| **표 삽입** | Flyout으로 행/열 지정 후 삽입 | ✅ 완료 |
| **표 셀 편집** | 셀 클릭 → TextBox 전환, 서식 적용 | ✅ 완료 |
| **하이퍼링크** | 손 커서 + 클릭 시 브라우저 열기 | ✅ 완료 |
| **클릭 위치 커서** | 텍스트 중간 클릭 시 해당 위치에 캐럿 | ✅ 완료 |
| **HTML 파싱** | HTML 문자열 → 문서 모델 (블록/인라인 태그, style 속성, 엔티티) | ✅ 완료 |
| **HTML 직렬화** | 문서 모델 → HTML 문자열 (시맨틱 태그 + style) | ✅ 완료 |
| **Html 프로퍼티** | `StyledProperty<string>` 양방향 바인딩 | ✅ 완료 |

## 미구현 / 향후 작업

| 항목 | 설명 | 우선순위 |
|------|------|----------|
| 밑줄 / 취소선 버튼 | 모델은 지원, 툴바 버튼 미추가 | 낮음 |
| 실행 취소 (Undo/Redo) | 편집 이력 관리 | 중간 |
| 표 행/열 추가·삭제 | 기존 표 구조 편집 | 중간 |
| 이미지 리사이즈 | 드래그로 크기 조절 | 낮음 |
| 키보드 단축키 | Ctrl+B/I/U 등 | 중간 |
| 한국어 IME 최적화 | 조합 중 입력 처리 | 필요 시 |
| HtmlInlineParser.cs 제거 | 레거시 파일 정리 | 낮음 |

## 사용법

```xml
<!-- XAML -->
<local:HtmlEditorControl Html="{CompiledBinding HtmlContent, Mode=TwoWay}" />
```

```csharp
// 코드
var editor = new HtmlEditorControl();
editor.Html = "<p>Hello <strong>World</strong></p>";
string result = editor.Html; // 편집 후 HTML 가져오기
```

## 빌드 및 테스트

```bash
# 라이브러리 빌드
cd SaemDesk.HtmlEditor
dotnet build

# 테스트 앱 실행
cd SaemDesk.HtmlEditor.TestApp
dotnet run
```

## 해결한 주요 기술 이슈

1. **SelectableTextBlock 이벤트 차단**: PointerPressed를 Handled 처리하여 클릭-편집 전환 불가 → TextBlock으로 변경 + AddHandler(Tunnel, handledEventsToo: true)
2. **툴바 클릭 시 선택 소실**: Focusable="False"로도 해결 안 됨 → LastSelection 자동 저장 + 3단계 fallback
3. **Flyout 포커스 이탈**: Flyout 팝업이 비주얼 트리 밖이라 커밋 발생 → IsInPopup() 체크로 방지
4. **NumericUpDown 숫자 안 보임**: Width 부족 → Width=120으로 확대
