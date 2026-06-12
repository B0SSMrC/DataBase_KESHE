using DormManagement.Forms;

namespace DormManagement
{
    static class Program
    {
        /// <summary>应用入口：先登录，成功后进入主界面</summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            using var login = new LoginForm();
            if (login.ShowDialog() == DialogResult.OK)
                Application.Run(new MainForm(login.LoggedInName));
        }
    }
}
