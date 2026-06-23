-- 10_operation_log.sql  通用操作日志（供时间点恢复的可点选时间线使用）
USE DormDB;
GO
IF OBJECT_ID('OperationLog','U') IS NOT NULL DROP TABLE OperationLog;
GO
CREATE TABLE OperationLog (
    op_id       INT IDENTITY(1,1) PRIMARY KEY,
    op_time     DATETIME2(3) NOT NULL DEFAULT SYSDATETIME(),  -- 毫秒精度，供 STOPAT 使用
    category    NVARCHAR(20) NOT NULL,   -- 宿舍楼/班级/学生/寝室/入住/调宿/退宿
    action      NVARCHAR(10) NOT NULL,   -- 新增/修改/删除
    description NVARCHAR(200) NULL,
    operator    NVARCHAR(50)  NULL
);
GO
