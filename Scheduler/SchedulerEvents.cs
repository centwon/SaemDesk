using System;

namespace SaemDesk.Scheduler;

/// <summary>
/// 일정/할일 변경 알림 정적 이벤트 버스.
/// CalendarHomePage 와 KAgendaControl 등 여러 화면이 같은 데이터를 표시할 때
/// 어느 쪽에서 편집하면 다른 쪽도 새로고침하기 위해 사용한다.
/// </summary>
public static class SchedulerEvents
{
    /// <summary>일정/할일이 추가·수정·삭제되었을 때 발생.</summary>
    public static event EventHandler? ItemChanged;

    /// <summary>변경 알림 발사 — 편집/삭제/토글 직후 호출.</summary>
    public static void RaiseItemChanged() => ItemChanged?.Invoke(null, EventArgs.Empty);
}
