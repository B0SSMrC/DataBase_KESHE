-- 08_verify.sql  功能与约束验证（分三段执行）
USE DormDB;
GO

-- ============ Part A：入住两人，验证触发器与存储过程 ============
DECLARE @s1 INT=(SELECT student_id FROM Student WHERE student_no='2021001'); -- 张三
DECLARE @s2 INT=(SELECT student_id FROM Student WHERE student_no='2021002'); -- 李四
DECLARE @bedA INT=(SELECT bd.bed_id FROM Bed bd JOIN Room r ON bd.room_id=r.room_id
   JOIN Building b ON r.building_id=b.building_id
   WHERE b.building_no='1号楼' AND r.room_no='101' AND bd.bed_no='1');
DECLARE @bedB INT=(SELECT bd.bed_id FROM Bed bd JOIN Room r ON bd.room_id=r.room_id
   JOIN Building b ON r.building_id=b.building_id
   WHERE b.building_no='1号楼' AND r.room_no='101' AND bd.bed_no='2');
INSERT INTO Allocation(student_id,bed_id) VALUES(@s1,@bedA),(@s2,@bedB);
SELECT 房号,已住人数,空床位数,状态 FROM v_RoomStatus WHERE 房号='101';  -- 预期 2/2/有人
EXEC usp_GetRoomStudents '1号楼','101';                                    -- 预期 张三、李四
SELECT * FROM v_StudentHousing;                                            -- 预期 张三、李四
GO

-- ============ Part B-1：一床一人（预期报错）============
-- 让王五去抢张三已占的 1 号床，bed_id 重复 → 唯一约束冲突
DECLARE @s3 INT=(SELECT student_id FROM Student WHERE student_no='2021003');
DECLARE @bedA INT=(SELECT bd.bed_id FROM Bed bd JOIN Room r ON bd.room_id=r.room_id
   JOIN Building b ON r.building_id=b.building_id
   WHERE b.building_no='1号楼' AND r.room_no='101' AND bd.bed_no='1');
INSERT INTO Allocation(student_id,bed_id) VALUES(@s3,@bedA);
GO

-- ============ Part B-2：一人一寝（预期报错）============
-- 让已入住的张三再占一张空床，student_id 重复 → 唯一约束冲突
DECLARE @s1 INT=(SELECT student_id FROM Student WHERE student_no='2021001');
DECLARE @bedC INT=(SELECT bd.bed_id FROM Bed bd JOIN Room r ON bd.room_id=r.room_id
   JOIN Building b ON r.building_id=b.building_id
   WHERE b.building_no='1号楼' AND r.room_no='101' AND bd.bed_no='3');
INSERT INTO Allocation(student_id,bed_id) VALUES(@s1,@bedC);
GO

-- ============ Part C-1：退宿 ============
-- 张三退宿，人数应减、视图更新
DECLARE @s1 INT=(SELECT student_id FROM Student WHERE student_no='2021001');
DELETE FROM Allocation WHERE student_id=@s1;
SELECT 房号,已住人数,空床位数,状态 FROM v_RoomStatus WHERE 房号='101';  -- 预期 1/3/有人
GO

-- ============ Part C-2：调宿 ============
-- 把李四从 101 换到 102 的 1 号床
DECLARE @s2 INT=(SELECT student_id FROM Student WHERE student_no='2021002');
DECLARE @bed102 INT=(SELECT bd.bed_id FROM Bed bd JOIN Room r ON bd.room_id=r.room_id
   JOIN Building b ON r.building_id=b.building_id
   WHERE b.building_no='1号楼' AND r.room_no='102' AND bd.bed_no='1');
UPDATE Allocation SET bed_id=@bed102 WHERE student_id=@s2;
SELECT 房号,已住人数,空床位数,状态 FROM v_RoomStatus WHERE 房号 IN ('101','102') ORDER BY 房号;
GO
-- 预期：101 → 0/4/空闲；102 → 1/3/有人
