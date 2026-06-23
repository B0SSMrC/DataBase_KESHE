-- 07_trigger.sql  住宿分配增/改/删时，自动重算寝室已住人数与状态
USE DormDB;
GO
CREATE OR ALTER TRIGGER trg_Allocation_Occupancy
ON Allocation
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- 收集受影响的寝室（新增侧 + 删除侧的床位所属寝室）
    DECLARE @rooms TABLE (room_id INT PRIMARY KEY);
    INSERT INTO @rooms (room_id)
    SELECT DISTINCT bd.room_id
    FROM Bed bd
    WHERE bd.bed_id IN (SELECT bed_id FROM inserted)
       OR bd.bed_id IN (SELECT bed_id FROM deleted);

    -- 对每个受影响寝室，按实际分配数重算人数与状态
    UPDATE r
    SET occupied_count = x.cnt,
        status = CASE WHEN x.cnt = 0 THEN '空闲'
                      WHEN x.cnt >= r.capacity THEN '已满'
                      ELSE '有人' END
    FROM Room r
    JOIN @rooms ar ON ar.room_id = r.room_id
    CROSS APPLY (
        SELECT COUNT(*) AS cnt
        FROM Allocation a
        JOIN Bed b ON a.bed_id = b.bed_id
        WHERE b.room_id = r.room_id
    ) x;
END
GO
