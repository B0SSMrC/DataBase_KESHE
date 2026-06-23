-- 05_create_views.sql
USE DormDB;
GO

-- 寝室状态：已住人数、空床位数（要求 5）
CREATE OR ALTER VIEW v_RoomStatus AS
SELECT b.building_no               AS 楼号,
       r.room_no                   AS 房号,
       r.capacity                  AS 床位数,
       r.occupied_count            AS 已住人数,
       r.capacity - r.occupied_count AS 空床位数,
       r.status                    AS 状态
FROM Room r
JOIN Building b ON r.building_id = b.building_id;
GO

-- 学生住宿信息（要求 3）
CREATE OR ALTER VIEW v_StudentHousing AS
SELECT s.student_no   AS 学号,
       s.name         AS 姓名,
       c.major        AS 专业,
       b.building_no  AS 楼号,
       r.room_no      AS 房号,
       bd.bed_no      AS 床号,
       a.check_in_date AS 入住日期
FROM Student s
JOIN Allocation a ON s.student_id = a.student_id
JOIN Bed bd       ON a.bed_id     = bd.bed_id
JOIN Room r       ON bd.room_id   = r.room_id
JOIN Building b   ON r.building_id = b.building_id
LEFT JOIN ClassInfo c ON s.class_id = c.class_id;
GO
