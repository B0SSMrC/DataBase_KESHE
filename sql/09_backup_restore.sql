-- 09_backup_restore.sql  备份 / 恢复 / 基于时间点恢复(PITR)
-- 按步骤执行；Step 2 打印的时间点 T 要复制，填到 Step 5 的 STOPAT。

-- ===== Step 1：完整备份 =====
USE master;
GO
BACKUP DATABASE DormDB
TO DISK = 'D:\DataBase\KESHE\backup\DormDB_full.bak'
WITH INIT, NAME = 'DormDB-Full';
GO

-- ===== Step 2：制造"好数据" + 记录时间点 T =====
USE DormDB;
GO
INSERT INTO Student(student_no,name,gender,class_id,phone)
VALUES('2021009','重要同学','男',
       (SELECT class_id FROM ClassInfo WHERE class_name='计科2101'),'13900000009');
SELECT CONVERT(VARCHAR(23), SYSDATETIME(), 121) AS 时间点T;   -- ← 复制这个值
GO

-- ===== Step 3：制造"误操作"（发生在 T 之后）=====
USE DormDB;
GO
DELETE FROM Student WHERE student_no='2021009';
GO

-- ===== Step 4：事务日志备份 =====
USE master;
GO
BACKUP LOG DormDB
TO DISK = 'D:\DataBase\KESHE\backup\DormDB_log.trn'
WITH INIT, NAME = 'DormDB-Log';
GO

-- ===== Step 5：恢复到时间点 T（误删之前）=====
USE master;
GO
ALTER DATABASE DormDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;  -- 踢掉其他连接，独占
GO
RESTORE DATABASE DormDB
FROM DISK = 'D:\DataBase\KESHE\backup\DormDB_full.bak'
WITH NORECOVERY, REPLACE;
GO
RESTORE LOG DormDB
FROM DISK = 'D:\DataBase\KESHE\backup\DormDB_log.trn'
WITH STOPAT = '2026-06-12 10:30:39.590',   -- ← Step 2 记录的 T
     RECOVERY;
GO
ALTER DATABASE DormDB SET MULTI_USER;
GO

-- ===== Step 6：验证 PITR 成功 =====
USE DormDB;
SELECT * FROM Student WHERE student_no='2021009';   -- 预期：有 1 行（找回了）
GO
