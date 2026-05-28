using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SaemDesk.Models;
using SaemDesk.Repositories;

namespace SaemDesk.Services;

/// <summary>
/// 수업 일정 Undo/Redo 서비스
/// </summary>
public class ScheduleShiftService
{
    private readonly ScheduleRepository _scheduleRepo;
    private readonly ScheduleUnitMapRepository _mapRepo;
    private readonly UndoHistoryRepository _undoRepo;
    private readonly LessonRepository _lessonRepo;
    private readonly SchoolScheduleRepository _schoolScheduleRepo;

    public ScheduleShiftService(
        ScheduleRepository scheduleRepo,
        ScheduleUnitMapRepository mapRepo,
        UndoHistoryRepository undoRepo,
        LessonRepository lessonRepo,
        SchoolScheduleRepository schoolScheduleRepo)
    {
        _scheduleRepo = scheduleRepo;
        _mapRepo = mapRepo;
        _undoRepo = undoRepo;
        _lessonRepo = lessonRepo;
        _schoolScheduleRepo = schoolScheduleRepo;
    }

    public async Task<UndoRedoResult> UndoLastActionAsync(int courseId, string room)
    {
        var result = new UndoRedoResult();
        try
        {
            var action = await _undoRepo.GetLastUndoableActionAsync(courseId, room);
            if (action == null)
            {
                result.Success = false;
                result.Message = "취소할 작업이 없습니다.";
                return result;
            }

            switch (action.ActionType)
            {
                case UndoActionType.ScheduleShift:
                    await UndoShiftAsync(action);
                    break;
                case UndoActionType.ScheduleCreate:
                    await UndoCreateAsync(action);
                    break;
                case UndoActionType.ScheduleDelete:
                    await UndoDeleteAsync(action);
                    break;
                case UndoActionType.BulkGenerate:
                    await UndoBulkGenerateAsync(action);
                    break;
                default:
                    result.Success = false;
                    result.Message = $"지원하지 않는 작업 유형입니다.";
                    return result;
            }

            await _undoRepo.MarkAsUndoneAsync(action.No);
            result.Success = true;
            result.Message = $"'{action.Description}' 취소됨";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"취소 실패: {ex.Message}";
        }
        return result;
    }

    public async Task<UndoRedoResult> RedoLastActionAsync(int courseId, string room)
    {
        var result = new UndoRedoResult();
        try
        {
            var action = await _undoRepo.GetLastRedoableActionAsync(courseId, room);
            if (action == null)
            {
                result.Success = false;
                result.Message = "다시 실행할 작업이 없습니다.";
                return result;
            }

            switch (action.ActionType)
            {
                case UndoActionType.ScheduleShift:
                    await RedoShiftAsync(action);
                    break;
                case UndoActionType.BulkGenerate:
                    throw new NotSupportedException("자동 배치의 다시 실행은 지원되지 않습니다.");
                default:
                    result.Success = false;
                    result.Message = "지원하지 않는 작업 유형입니다.";
                    return result;
            }

            await _undoRepo.MarkAsRedoneAsync(action.No);
            result.Success = true;
            result.Message = $"'{action.Description}' 다시 실행됨";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"다시 실행 실패: {ex.Message}";
        }
        return result;
    }

    private async Task UndoShiftAsync(UndoAction action)
    {
        var data = action.GetData<ShiftActionData>();
        if (data == null) return;
        foreach (var shift in data.ShiftedSchedules.OrderBy(s => s.OriginalDate).ThenBy(s => s.OriginalPeriod))
        {
            var schedule = await _scheduleRepo.GetByIdAsync(shift.ScheduleId);
            if (schedule != null)
            {
                schedule.Date = shift.OriginalDate;
                schedule.Period = shift.OriginalPeriod;
                await _scheduleRepo.UpdateAsync(schedule);
            }
        }
    }

    private async Task UndoCreateAsync(UndoAction action)
    {
        var data = action.GetData<ScheduleActionData>();
        if (data == null) return;
        await _mapRepo.DeleteByScheduleAsync(data.ScheduleId);
        await _scheduleRepo.DeleteAsync(data.ScheduleId);
    }

    private async Task UndoDeleteAsync(UndoAction action)
    {
        var data = action.GetData<ScheduleActionData>();
        if (data == null) return;
        var schedule = new Schedule
        {
            CourseId = data.CourseId,
            Room = data.Room,
            Date = data.Date,
            Period = data.Period,
            IsPinned = data.IsPinned
        };
        await _scheduleRepo.CreateAsync(schedule);
        foreach (var sectionId in data.SectionIds)
            await _mapRepo.AddUnitToScheduleAsync(schedule.No, sectionId);
    }

    private async Task UndoBulkGenerateAsync(UndoAction action)
    {
        var data = action.GetData<BulkGenerateActionData>();
        if (data == null) return;
        foreach (var scheduleId in data.CreatedScheduleIds)
        {
            await _mapRepo.DeleteByScheduleAsync(scheduleId);
            await _scheduleRepo.DeleteAsync(scheduleId);
        }
    }

    private async Task RedoShiftAsync(UndoAction action)
    {
        var data = action.GetData<ShiftActionData>();
        if (data == null) return;
        foreach (var shift in data.ShiftedSchedules)
        {
            var schedule = await _scheduleRepo.GetByIdAsync(shift.ScheduleId);
            if (schedule != null)
            {
                schedule.Date = shift.NewDate;
                schedule.Period = shift.NewPeriod;
                await _scheduleRepo.UpdateAsync(schedule);
            }
        }
    }
}

public class UndoRedoResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
}
