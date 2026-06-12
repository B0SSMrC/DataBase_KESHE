namespace DormManagement
{
    /// <summary>全局配置：数据库连接字符串</summary>
    public static class AppConfig
    {
        public const string ConnectionString =
            "Server=localhost;Database=DormDB;Trusted_Connection=True;TrustServerCertificate=True;";

        // 备份/恢复需要连到 master 执行
        public const string MasterConnectionString =
            "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
    }
}
