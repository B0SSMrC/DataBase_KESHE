namespace DormManagement
{
    /// <summary>运行期会话状态：保存当前登录操作员，供操作日志记录使用</summary>
    public static class Session
    {
        public static string CurrentOperator { get; set; } = "";
    }
}
