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

        // 在单个事务内执行数据写入 + 记一条操作日志，保证两者一致、时间戳同步
        public static int ExecuteLogged(string dataSql, SqlParameter[] dataParams,
            string category, string action, string description)
        {
            using var conn = new SqlConnection(AppConfig.ConnectionString);
            conn.Open();
            using var tran = conn.BeginTransaction();
            try
            {
                int n;
                using (var cmd = new SqlCommand(dataSql, conn, tran))
                {
                    if (dataParams != null) cmd.Parameters.AddRange(dataParams);
                    n = cmd.ExecuteNonQuery();
                }
                using (var log = new SqlCommand(
                    "INSERT INTO OperationLog(category,action,description,operator) VALUES(@c,@a,@d,@o)",
                    conn, tran))
                {
                    log.Parameters.AddRange(new[] {
                        new SqlParameter("@c", category),
                        new SqlParameter("@a", action),
                        new SqlParameter("@d", (object?)description ?? DBNull.Value),
                        new SqlParameter("@o", string.IsNullOrWhiteSpace(Session.CurrentOperator)
                            ? (object)DBNull.Value : Session.CurrentOperator) });
                    log.ExecuteNonQuery();
                }
                tran.Commit();
                return n;
            }
            catch { try { tran.Rollback(); } catch { /* 回滚自身异常忽略，保留并抛出原始异常 */ } throw; }
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
    }
}
