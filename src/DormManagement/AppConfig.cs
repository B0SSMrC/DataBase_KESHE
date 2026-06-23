namespace DormManagement
{
    /// <summary>全局配置：数据库连接字符串（可用环境变量 DORMDB_CONN 覆盖，便于换实例/远程而不改源码重编译）</summary>
    public static class AppConfig
    {
        public static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("DORMDB_CONN")
            ?? "Server=localhost;Database=DormDB;Trusted_Connection=True;TrustServerCertificate=True;";
    }
}
