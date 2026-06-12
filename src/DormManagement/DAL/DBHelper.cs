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
    }
}
