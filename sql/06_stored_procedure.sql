-- 06_stored_procedure.sql  输入楼号+房号，返回该寝室住宿学生名单
USE DormDB;
GO
CREATE OR ALTER PROCEDURE usp_GetRoomStudents
    @building_no NVARCHAR(20),
    @room_no     NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.student_no    AS 学号,
           s.name          AS 姓名,
           s.gender        AS 性别,
           bd.bed_no       AS 床号,
           a.check_in_date AS 入住日期
    FROM Building b
    JOIN Room r       ON r.building_id = b.building_id
    JOIN Bed  bd      ON bd.room_id    = r.room_id
    JOIN Allocation a ON a.bed_id      = bd.bed_id
    JOIN Student s    ON s.student_id  = a.student_id
    WHERE b.building_no = @building_no
      AND r.room_no     = @room_no
    ORDER BY bd.bed_no;
END
GO
