using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SaemDesk.Models;

namespace SaemDesk.Repositories;

/// <summary>
/// ScheduleUnitMap Repository — 수업-단원 매핑 관리
/// </summary>
public class ScheduleUnitMapRepository : BaseRepository
{
    public ScheduleUnitMapRepository(string dbPath) : base(dbPath)
    {
        EnsureTableExists();
    }

    private void EnsureTableExists()
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS ScheduleUnitMap (
                No INTEGER PRIMARY KEY AUTOINCREMENT,
                ScheduleId INTEGER NOT NULL,
                CourseSectionId INTEGER NOT NULL,
                AllocatedHours INTEGER DEFAULT 1,
                OrderInSlot INTEGER DEFAULT 1,
                FOREIGN KEY (ScheduleId) REFERENCES Schedule(No) ON DELETE CASCADE,
                FOREIGN KEY (CourseSectionId) REFERENCES CourseSection(No) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_sum_schedule ON ScheduleUnitMap(ScheduleId);
            CREATE INDEX IF NOT EXISTS idx_sum_section ON ScheduleUnitMap(CourseSectionId);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_sum_unique ON ScheduleUnitMap(ScheduleId, CourseSectionId);
        ";
        try
        {
            using var cmd = CreateCommand(sql);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            LogError("ScheduleUnitMap 테이블 생성 실패", ex);
        }
    }

    public async Task<int> CreateAsync(ScheduleUnitMap map)
    {
        const string q = @"
            INSERT INTO ScheduleUnitMap (ScheduleId, CourseSectionId, AllocatedHours, OrderInSlot)
            VALUES (@ScheduleId, @CourseSectionId, @AllocatedHours, @OrderInSlot);
            SELECT last_insert_rowid();";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@ScheduleId", map.ScheduleId);
        cmd.Parameters.AddWithValue("@CourseSectionId", map.CourseSectionId);
        cmd.Parameters.AddWithValue("@AllocatedHours", map.AllocatedHours);
        cmd.Parameters.AddWithValue("@OrderInSlot", map.OrderInSlot);
        var result = await cmd.ExecuteScalarAsync();
        map.No = Convert.ToInt32(result ?? 0);
        return map.No;
    }

    public async Task<int> AddUnitToScheduleAsync(int scheduleId, int courseSectionId, int order = 0)
    {
        if (order <= 0)
            order = await GetNextOrderAsync(scheduleId);
        return await CreateAsync(new ScheduleUnitMap
        {
            ScheduleId = scheduleId,
            CourseSectionId = courseSectionId,
            AllocatedHours = 1,
            OrderInSlot = order
        });
    }

    public async Task<List<ScheduleUnitMap>> GetByScheduleAsync(int scheduleId)
    {
        const string q = "SELECT * FROM ScheduleUnitMap WHERE ScheduleId=@Id ORDER BY OrderInSlot";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@Id", scheduleId);
        return await ExecuteQueryAsync(cmd);
    }

    public async Task<List<ScheduleUnitMap>> GetByScheduleWithSectionAsync(int scheduleId)
    {
        const string q = @"
            SELECT m.*, cs.SectionName, cs.UnitNo, cs.ChapterNo, cs.SectionNo, cs.SectionType
            FROM ScheduleUnitMap m
            INNER JOIN CourseSection cs ON m.CourseSectionId = cs.No
            WHERE m.ScheduleId = @Id
            ORDER BY m.OrderInSlot";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@Id", scheduleId);
        var maps = new List<ScheduleUnitMap>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var map = MapRow(reader);
            map.CourseSection = new CourseSection
            {
                No = map.CourseSectionId,
                SectionName = reader.GetString(reader.GetOrdinal("SectionName")),
                UnitNo = reader.GetInt32(reader.GetOrdinal("UnitNo")),
                ChapterNo = reader.GetInt32(reader.GetOrdinal("ChapterNo")),
                SectionNo = reader.GetInt32(reader.GetOrdinal("SectionNo")),
                SectionType = SafeString(reader, "SectionType", "Normal")
            };
            maps.Add(map);
        }
        return maps;
    }

    /// <summary>특정 매핑 존재 여부 확인 (SchedulingEngine에서 사용)</summary>
    public async Task<bool> ExistsAsync(int scheduleId, int courseSectionId)
    {
        const string q = @"
            SELECT EXISTS(SELECT 1 FROM ScheduleUnitMap
            WHERE ScheduleId=@S AND CourseSectionId=@C)";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@S", scheduleId);
        cmd.Parameters.AddWithValue("@C", courseSectionId);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }

    public async Task<bool> DeleteAsync(int no)
    {
        const string q = "DELETE FROM ScheduleUnitMap WHERE No=@No";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@No", no);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<int> DeleteByScheduleAsync(int scheduleId)
    {
        const string q = "DELETE FROM ScheduleUnitMap WHERE ScheduleId=@Id";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@Id", scheduleId);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> RemoveUnitFromScheduleAsync(int scheduleId, int courseSectionId)
    {
        const string q = "DELETE FROM ScheduleUnitMap WHERE ScheduleId=@S AND CourseSectionId=@C";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@S", scheduleId);
        cmd.Parameters.AddWithValue("@C", courseSectionId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private async Task<int> GetNextOrderAsync(int scheduleId)
    {
        const string q = "SELECT COALESCE(MAX(OrderInSlot),0)+1 FROM ScheduleUnitMap WHERE ScheduleId=@Id";
        using var cmd = CreateCommand(q);
        cmd.Parameters.AddWithValue("@Id", scheduleId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<List<ScheduleUnitMap>> ExecuteQueryAsync(SqliteCommand cmd)
    {
        var list = new List<ScheduleUnitMap>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(MapRow(reader));
        return list;
    }

    private ScheduleUnitMap MapRow(SqliteDataReader r) => new()
    {
        No = r.GetInt32(r.GetOrdinal("No")),
        ScheduleId = r.GetInt32(r.GetOrdinal("ScheduleId")),
        CourseSectionId = r.GetInt32(r.GetOrdinal("CourseSectionId")),
        AllocatedHours = SafeInt(r, "AllocatedHours", 1),
        OrderInSlot = SafeInt(r, "OrderInSlot", 1)
    };

    private int SafeInt(SqliteDataReader r, string col, int def = 0)
    {
        try { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? def : r.GetInt32(o); }
        catch { return def; }
    }

    private string SafeString(SqliteDataReader r, string col, string def = "")
    {
        try { var o = r.GetOrdinal(col); return r.IsDBNull(o) ? def : r.GetString(o); }
        catch { return def; }
    }
}
