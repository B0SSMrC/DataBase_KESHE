-- 11_review_fixes.sql  代码审查修复（幂等，可在已有库上直接执行）
-- 补齐 02/03 的两处一致性缺口：HousingLog.bed_id 索引 + Room.status 值域 CHECK
USE DormDB;
GO

-- 1) HousingLog.bed_id 外键列补非聚集索引（与其它外键列保持一致，加速按床位查历史/删床位外键校验）
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Log_Bed' AND object_id = OBJECT_ID('HousingLog'))
    CREATE INDEX IX_Log_Bed ON HousingLog(bed_id);
GO

-- 2) Room.status 值域 CHECK（防御性：把触发器维护的合法取值固化到约束层）
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Room_Status')
    ALTER TABLE Room ADD CONSTRAINT CK_Room_Status CHECK (status IN (N'空闲', N'有人', N'已满'));
GO
