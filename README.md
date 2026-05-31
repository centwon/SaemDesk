# SaemDesk

학교 업무 통합 데스크탑 앱 — 학생·수업·학급일지·게시판·스케줄러·NEIS·Google Calendar.
NewSchool(WinUI3)에서 **Avalonia 12**로 마이그레이션한 프로젝트입니다.

## 기술 스택
- **언어/런타임**: C# / .NET 10
- **UI**: Avalonia 12 (CompiledBinding, Native AOT)
- **MVVM**: CommunityToolkit.Mvvm
- **DB**: SQLite (ADO.NET 직접, Repository 패턴) — `school.db` / `board.db` / `schedule.db`
- **문서/내보내기**: QuestPDF, MiniExcel, ClosedXML
- **에디터**: Jodit (Avalonia.Controls.WebView)
- **플랫폼**: Windows 우선 (DPAPI 암호화, 한글 IME), 차후 크로스플랫폼

## 주요 기능
- 🏠 **홈/달력**: 오늘 대시보드, 통합 월 달력, Google Calendar 동기화
- 👥 **학급**: 학급일지, 학생 정보·기록, 학생부(NEIS 특기사항), 자리 배정, 학급 게시판, 정보 출력/내보내기
- 📚 **수업**: 수업홈, 누가기록, 교과 세특, 시간표, 수업 관리, 동아리 활동
- 💼 **업무**: 업무 관리 대시보드, 업무 게시판
- 🗃 **아카이브 / ⚙ 설정**: 학교·학생·앱 설정, 학사일정 관리

## 문서
- [docs/PROJECT_REFERENCE.md](docs/PROJECT_REFERENCE.md) — **전체 파일 명세 · 메뉴 페이지 기능 (구조 참조용)**
- [CLAUDE.md](CLAUDE.md) — 개발 규칙 / 코딩 가이드
- [CHANGELOG.md](CHANGELOG.md) — 변경 이력

## 빌드
```bash
dotnet build                          # 디버그 빌드
dotnet publish -r win-x64 -c Release  # Native AOT 퍼블리시
```

## 라이선스
사내/개인 프로젝트. (별도 라이선스 명시 전까지 무단 재배포 금지)
