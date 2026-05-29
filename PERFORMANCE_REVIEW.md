# SaemDesk 성능·효율 점검 (2026-05-29)

코드베이스 전반(DB 접근 · 시작 경로 · 비동기 · UI/바인딩 · 로깅 · 에셋)을 점검하여 도출한
개선 항목과 처리 현황. 영향도 순으로 정리.

## 🔴 Tier 1 — 실질적 영향

### 1. N+1 쿼리 패턴 — 루프 안에서 단건 조회 반복
- `Services/TimetableService.cs` — `courses` 순회 중 course마다 `lessonRepo.GetByCourseAsync(course.No)` 호출.
- `Services/TeacherService.cs` `GetSchoolTeachersAsync` — `histories` 순회 중 history마다 `teacherRepo.GetByTeacherIdAsync(...)` 호출.
- **개선**: `WHERE ... IN (...)` 일괄 조회 후 메모리 그룹핑.
- **상태**: ✅ 완료 — `LessonRepository.GetByCoursesAsync` 신규, `TeacherRepository.GetByIdsAsync`(기존) 재사용.

### 2. Repository 생성마다 PRAGMA 4종 중복 실행
- `Repositories/BaseRepository.cs` 생성자가 매번 `PRAGMA journal_mode=WAL; busy_timeout; cache_size; mmap_size` 실행.
- `new XxxRepository(...)` 호출 지점이 전 영역 155곳 → PRAGMA 실행이 순수 중복 비용.
- `journal_mode=WAL`은 DB 파일에 영속되므로 재실행 불필요.
- **개선**: 프로세스 내 DB 파일별 최초 1회만 WAL 적용(정적 가드). 추가로 4개 repo가
  생성자마다 재실행하던 `EnsureTableExists()`(CREATE TABLE/INDEX)를 `EnsureSchemaOnce`로 1회화.
  연결별 튜닝 PRAGMA(busy_timeout/cache/mmap)는 풀의 각 물리 연결 정확성을 위해 매번 유지.
- **상태**: ✅ 완료

## 🟡 Tier 2 — 중간 영향

### 3. 로거가 매 배치 쓰기마다 디렉터리 전체 스캔
- `Logging/FileLogger.cs` `WriteLogsAsync` → `RotateLogIfNeededAsync` → `CleanupOldLogs()`가
  매 배치마다 `Directory.GetFiles` + 파일별 `FileInfo` 스탯 수행.
- **개선**: 오래된 로그 정리는 세션/하루 1회만.
- **상태**: ✅ 완료 — `_lastCleanup` 게이트로 24시간당 1회만 `CleanupOldLogs` 실행.

### 4. 시작 시 스키마 DDL 무조건 재실행 (버전 가드 없음)
- `DatabaseInitializer.cs` — 매 시작마다 `CREATE TABLE`(개별 실행 ~22회) + 인덱스 배치 재실행.
- **개선**: `PRAGMA user_version` 스키마 버전 체크로 일치 시 DDL 건너뛰기.
- **상태**: ✅ 완료 — `SchemaVersion` 상수 + `user_version` 비교. 구조 변경 시 상수 증가로 재실행.

## 🟢 Tier 3 — 소규모 정리

### 5. 일부 ViewModel이 아직 plain ObservableCollection 사용
- Clear() + 루프 Add 패턴은 항목마다 UI 알림 발생. `OptimizedObservableCollection.ReplaceAll`로 통일.
- **상태**: ⏸ 변경 불필요 — 미적용 3개 점검 결과 `StudentLogPageVM`·`StudentSpecPageVM`의
  `Categories`는 고정 목록(일괄 갱신 없음), `AddStudentsPageVM.Pending`은 루프 중 중복검사가
  컬렉션 상태에 의존하는 소량 일회성 import라 변환 이득이 없고 의미만 바뀜. 과한 변경 회피.

### 6. 미사용 템플릿 에셋
- `Assets/avalonia-logo.ico`(171KB) Avalonia 템플릿 잔재. AOT 바이너리 용량 절감.
- **상태**: ✅ 완료 — 미참조 확인 후 삭제(앱 아이콘은 `icon.ico` 별도).

## 검토했으나 문제 없던 항목
- 블로킹 호출(`.Result`/`.Wait()`) 없음 (전부 `dialog.Result` 프로퍼티 오탐). `async void` 없음.
- 시작 경로: 3개 DB 병렬 초기화 양호.
- 바인딩: `ReflectionBinding` 0건, CompiledBinding 전용(AOT 적합).
- 인덱스: 86개 정의로 핵심 조회 컬럼 커버 양호.
