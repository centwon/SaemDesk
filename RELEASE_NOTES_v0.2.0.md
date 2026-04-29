# SaemDesk v0.2.0-alpha — 2026-04-30

> NewSchool(WinUI3) → SaemDesk(Avalonia 12 + .NET 10 + Native AOT) 마이그레이션 두 번째 알파.

## 핵심 변경
- Phase 5 본 이식 완료 — 학사일정 편집/진도 매트릭스/메모보드 풀 기능
- 동아리·업무 그룹 신규(Chunk D/F) + 교사 시간표
- 통합 내보내기 페이지 (학급 × 데이터 × 형식 일괄)
- Native AOT 단일 파일 33 MB 재검증

## 새 페이지 / 컨트롤
- **학급** — 학생 누가기록 / 학생부 특기사항 / 자리 배정 / 학생 추가 / 학급 게시판 / 학급일지
- **수업** — 수업홈 / 주간 시간표 / 수업 누가기록 / 수업 관리 / 진도 관리 / 교사 시간표 / 스케줄러
- **동아리** — 동아리홈 / 동아리 관리 / 동아리 활동
- **단독** — 학사일정 관리 / 학사일정 보기 / 통합 내보내기 / 업무 / 도움말 / 설정

## 보강된 기능
- 진도 매트릭스 — 셀 우클릭 메뉴 6항(완료/보강/병합/건너뜀/결강/비우기), 격차 분석, 일정 동기화, Excel 내보내기
- 학사일정 관리 — Excel 내보내기, Google Calendar 일괄 업로드, 일괄 학년 적용
- 메모보드 — 카테고리 4색 칩, HTML 1줄 미리보기, 더블클릭 편집, ▲/▼/🗑

## 빌드
- `dotnet publish -r win-x64 -c Release` 단일 파일 33 MB
- 빌드 경고 0, 오류 0
- TrimmerRoots: QuestPDF · MiniExcel 보호 등록
- 잔존 경고는 외부 패키지(IL2104/IL3053) — 자체 코드 영향 없음

## 알려진 한계
- 시나리오 스모크 테스트 미수행 — DB CRUD / Google OAuth / Jodit / Excel·PDF / NEIS / 좌석 등 실사용 검증 필요
- AnnualLessonPlanPage(연간 진도 계획) 이식 보류
- 앱 아이콘은 임시 Avalonia 로고 — 차후 자체 디자인
- MemoBoard 진짜 드래그 재배치는 Post 모델에 Order 컬럼 필요(현재 ▲/▼ 화살표로 대체)

## 마이그레이션 메모
NewSchool 의 비밀 정보 구조(`secrets.json` 통합)와 동일한 방식 사용. 빌드 시 `secrets.json` 이 출력 디렉터리에 복사되며 `.gitignore` 로 제외.

---

## 다음 마일스톤 (v0.3.0 또는 v1.0.0 예정)
- 시나리오 스모크 테스트 통과
- AnnualLessonPlanPage 이식
- 자체 앱 아이콘
- GitHub 리포 연결 + 정식 릴리스
