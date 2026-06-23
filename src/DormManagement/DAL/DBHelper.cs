using System.Data;
using Microsoft.Data.SqlClient;

namespace DormManagement.DAL
{
    /// <summary>数据访问层：统一用参数化命令访问 SQL Server（防注入）</summary>
    public static class DBHelper
    {
        // 查询 → DataTable（用于 DataGridView 绑定）
        public static DataTable QueryTable(string sql, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(ps);
            using var da = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        // 增删改 → 受影响行数
        public static int Execute(string sql, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(ps);
            conn.Open();
            return cmd.ExecuteNonQuery();
        }

        // 标量查询
        public static object? Scalar(string sql, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(ps);
            conn.Open();
            return cmd.ExecuteScalar();
        }

        // 调用存储过程 → DataTable
        public static DataTable ProcTable(string proc, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(proc, conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddRange(ps);
            using var da = new SqlDataAdapter(cmd);
            var dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        // 单事务内：先记 OperationLog 取回 op_id，再拍快照(=操作前状态)，再执行数据写入
        public static int ExecuteLogged(string dataSql, SqlParameter[] dataParams,
            string category, string action, string description)
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();
            try
            {
                int opId;
                using (var log = new SqlCommand(
                    "INSERT INTO OperationLog(category,action,description,operator) OUTPUT INSERTED.op_id VALUES(@c,@a,@d,@o)",
                    conn, tran))
                {
                    log.Parameters.AddRange(new[] {
                        new SqlParameter("@c", category),
                        new SqlParameter("@a", action),
                        new SqlParameter("@d", (object?)description ?? DBNull.Value),
                        new SqlParameter("@o", string.IsNullOrWhiteSpace(Session.CurrentOperator)
                            ? (object)DBNull.Value : Session.CurrentOperator) });
                    opId = Convert.ToInt32(log.ExecuteScalar());
                }
                using (var snap = new SqlCommand(SnapSql, conn, tran))
                {
                    snap.Parameters.Add(new SqlParameter("@op_id", opId));
                    snap.ExecuteNonQuery();
                }
                int n;
                using (var cmd = new SqlCommand(dataSql, conn, tran))
                {
                    if (dataParams != null) cmd.Parameters.AddRange(dataParams);
                    n = cmd.ExecuteNonQuery();
                }
                tran.Commit();
                return n;
            }
            catch { try { tran.Rollback(); } catch { /* 保留并抛出原始异常 */ } throw; }
        }

        // 仅记一条操作日志（用于多语句操作如"新增寝室含建床位"，成功后调用）
        public static void Log(string category, string action, string description)
            => Execute("INSERT INTO OperationLog(category,action,description,operator) VALUES(@c,@a,@d,@o)",
                new SqlParameter("@c", category),
                new SqlParameter("@a", action),
                new SqlParameter("@d", (object?)description ?? DBNull.Value),
                new SqlParameter("@o", string.IsNullOrWhiteSpace(Session.CurrentOperator)
                    ? (object)DBNull.Value : Session.CurrentOperator));

        // 确保操作日志表存在：即使 PITR 回滚到建表之前、或未手动跑 sql/10，启动时也能自愈
        public static void EnsureOperationLog()
            => Execute(@"IF OBJECT_ID('OperationLog','U') IS NULL
                         CREATE TABLE OperationLog(
                             op_id       INT IDENTITY(1,1) PRIMARY KEY,
                             op_time     DATETIME2(3) NOT NULL DEFAULT SYSDATETIME(),
                             category    NVARCHAR(20) NOT NULL,
                             action      NVARCHAR(10) NOT NULL,
                             description NVARCHAR(200) NULL,
                             operator    NVARCHAR(50)  NULL);");

        // ===== 操作撤销：整库快照 =====

        // 业务表 → 影子快照（参数 @op_id 标记本次快照 = 该操作执行前的状态）
        const string SnapSql = @"
INSERT INTO Snap_Meta(snap_op_id) VALUES(@op_id);
INSERT INTO Snap_Building(snap_op_id,building_id,building_no,building_name,gender_type)
  SELECT @op_id,building_id,building_no,building_name,gender_type FROM Building;
INSERT INTO Snap_ClassInfo(snap_op_id,class_id,class_name,major)
  SELECT @op_id,class_id,class_name,major FROM ClassInfo;
INSERT INTO Snap_Room(snap_op_id,room_id,building_id,room_no,capacity,occupied_count,status)
  SELECT @op_id,room_id,building_id,room_no,capacity,occupied_count,status FROM Room;
INSERT INTO Snap_Bed(snap_op_id,bed_id,room_id,bed_no)
  SELECT @op_id,bed_id,room_id,bed_no FROM Bed;
INSERT INTO Snap_Student(snap_op_id,student_id,student_no,name,gender,class_id,phone)
  SELECT @op_id,student_id,student_no,name,gender,class_id,phone FROM Student;
INSERT INTO Snap_Allocation(snap_op_id,allocation_id,student_id,bed_id,check_in_date)
  SELECT @op_id,allocation_id,student_id,bed_id,check_in_date FROM Allocation;
INSERT INTO Snap_HousingLog(snap_op_id,log_id,student_id,bed_id,op_type,op_time,operator)
  SELECT @op_id,log_id,student_id,bed_id,op_type,op_time,operator FROM HousingLog;";

        // 自愈：保证 8 张快照表存在（与 sql/13 一致）
        const string EnsureUndoSql = @"
IF OBJECT_ID('Snap_Meta','U') IS NULL CREATE TABLE Snap_Meta(snap_op_id INT PRIMARY KEY);
IF OBJECT_ID('Snap_Building','U') IS NULL CREATE TABLE Snap_Building(snap_op_id INT, building_id INT, building_no NVARCHAR(20), building_name NVARCHAR(50), gender_type NVARCHAR(10));
IF OBJECT_ID('Snap_ClassInfo','U') IS NULL CREATE TABLE Snap_ClassInfo(snap_op_id INT, class_id INT, class_name NVARCHAR(50), major NVARCHAR(50));
IF OBJECT_ID('Snap_Room','U') IS NULL CREATE TABLE Snap_Room(snap_op_id INT, room_id INT, building_id INT, room_no NVARCHAR(20), capacity INT, occupied_count INT, status NVARCHAR(10));
IF OBJECT_ID('Snap_Bed','U') IS NULL CREATE TABLE Snap_Bed(snap_op_id INT, bed_id INT, room_id INT, bed_no NVARCHAR(10));
IF OBJECT_ID('Snap_Student','U') IS NULL CREATE TABLE Snap_Student(snap_op_id INT, student_id INT, student_no NVARCHAR(20), name NVARCHAR(50), gender NVARCHAR(10), class_id INT, phone NVARCHAR(20));
IF OBJECT_ID('Snap_Allocation','U') IS NULL CREATE TABLE Snap_Allocation(snap_op_id INT, allocation_id INT, student_id INT, bed_id INT, check_in_date DATE);
IF OBJECT_ID('Snap_HousingLog','U') IS NULL CREATE TABLE Snap_HousingLog(snap_op_id INT, log_id INT, student_id INT, bed_id INT, op_type NVARCHAR(10), op_time DATETIME, operator NVARCHAR(50));";

        // 重载快照 @op_id：禁用占用触发器→按外键序清空→按主键序重载→删除≥@op_id的日志与快照
        const string RestoreSql = @"
SET XACT_ABORT ON;
BEGIN TRAN;
DISABLE TRIGGER trg_Allocation_Occupancy ON Allocation;
DELETE FROM Allocation; DELETE FROM HousingLog; DELETE FROM Bed; DELETE FROM Student; DELETE FROM Room; DELETE FROM ClassInfo; DELETE FROM Building;
SET IDENTITY_INSERT Building ON;
INSERT INTO Building(building_id,building_no,building_name,gender_type) SELECT building_id,building_no,building_name,gender_type FROM Snap_Building WHERE snap_op_id=@op_id;
SET IDENTITY_INSERT Building OFF;
SET IDENTITY_INSERT ClassInfo ON;
INSERT INTO ClassInfo(class_id,class_name,major) SELECT class_id,class_name,major FROM Snap_ClassInfo WHERE snap_op_id=@op_id;
SET IDENTITY_INSERT ClassInfo OFF;
SET IDENTITY_INSERT Room ON;
INSERT INTO Room(room_id,building_id,room_no,capacity,occupied_count,status) SELECT room_id,building_id,room_no,capacity,occupied_count,status FROM Snap_Room WHERE snap_op_id=@op_id;
SET IDENTITY_INSERT Room OFF;
SET IDENTITY_INSERT Student ON;
INSERT INTO Student(student_id,student_no,name,gender,class_id,phone) SELECT student_id,student_no,name,gender,class_id,phone FROM Snap_Student WHERE snap_op_id=@op_id;
SET IDENTITY_INSERT Student OFF;
SET IDENTITY_INSERT Bed ON;
INSERT INTO Bed(bed_id,room_id,bed_no) SELECT bed_id,room_id,bed_no FROM Snap_Bed WHERE snap_op_id=@op_id;
SET IDENTITY_INSERT Bed OFF;
SET IDENTITY_INSERT Allocation ON;
INSERT INTO Allocation(allocation_id,student_id,bed_id,check_in_date) SELECT allocation_id,student_id,bed_id,check_in_date FROM Snap_Allocation WHERE snap_op_id=@op_id;
SET IDENTITY_INSERT Allocation OFF;
SET IDENTITY_INSERT HousingLog ON;
INSERT INTO HousingLog(log_id,student_id,bed_id,op_type,op_time,operator) SELECT log_id,student_id,bed_id,op_type,op_time,operator FROM Snap_HousingLog WHERE snap_op_id=@op_id;
SET IDENTITY_INSERT HousingLog OFF;
ENABLE TRIGGER trg_Allocation_Occupancy ON Allocation;
DELETE FROM OperationLog WHERE op_id >= @op_id;
DELETE FROM Snap_Meta WHERE snap_op_id >= @op_id;
DELETE FROM Snap_Building WHERE snap_op_id >= @op_id;
DELETE FROM Snap_ClassInfo WHERE snap_op_id >= @op_id;
DELETE FROM Snap_Room WHERE snap_op_id >= @op_id;
DELETE FROM Snap_Bed WHERE snap_op_id >= @op_id;
DELETE FROM Snap_Student WHERE snap_op_id >= @op_id;
DELETE FROM Snap_Allocation WHERE snap_op_id >= @op_id;
DELETE FROM Snap_HousingLog WHERE snap_op_id >= @op_id;
COMMIT;";

        // 自愈创建快照表（同 EnsureOperationLog，防被 PITR 回退掉）
        public static void EnsureUndoInfra() => Execute(EnsureUndoSql);

        // 该操作是否有可用快照（本功能上线前的旧操作没有）
        public static bool SnapshotExists(int opId)
            => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM Snap_Meta WHERE snap_op_id=@id",
                   new SqlParameter("@id", opId))) > 0;

        // 把数据库回退到操作 @opId 执行之前的整库状态（纯 DML，不依赖备份文件、不锁库）
        public static void RestoreSnapshot(int opId)
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            using var cmd = new SqlCommand(RestoreSql, conn) { CommandTimeout = 120 };
            cmd.Parameters.Add(new SqlParameter("@op_id", opId));
            conn.Open();
            cmd.ExecuteNonQuery();
        }
    }
}
