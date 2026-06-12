using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    public class LoginForm : Form
    {
        readonly TextBox txtUser = new() { Width = 170 };
        readonly TextBox txtPwd = new() { Width = 170, UseSystemPasswordChar = true };
        readonly Button btnOk = new() { Text = "登录", Width = 80 };
        readonly Button btnCancel = new() { Text = "取消", Width = 80 };

        public string LoggedInName { get; private set; } = "";

        public LoginForm()
        {
            Text = "宿舍管理系统 - 登录";
            Width = 380; Height = 240;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;

            var title = new Label
            {
                Text = "学生宿舍管理系统",
                Dock = DockStyle.Top, Height = 56,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 15, FontStyle.Bold)
            };

            var lblU = new Label { Text = "用户名", Left = 45, Top = 78, Width = 60 };
            txtUser.Left = 115; txtUser.Top = 75;
            var lblP = new Label { Text = "密  码", Left = 45, Top = 115, Width = 60 };
            txtPwd.Left = 115; txtPwd.Top = 112;
            btnOk.Left = 115; btnOk.Top = 152;
            btnCancel.Left = 205; btnCancel.Top = 152;

            Controls.AddRange(new Control[] { title, lblU, txtUser, lblP, txtPwd, btnOk, btnCancel });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnOk.Click += (_, _) => DoLogin();
        }

        void DoLogin()
        {
            if (txtUser.Text.Trim() == "")
            {
                MessageBox.Show("请输入用户名");
                return;
            }
            try
            {
                var dt = DBHelper.QueryTable(
                    "SELECT real_name FROM Admin WHERE username=@u AND password=@p",
                    new SqlParameter("@u", txtUser.Text.Trim()),
                    new SqlParameter("@p", txtPwd.Text));
                if (dt.Rows.Count == 1)
                {
                    LoggedInName = dt.Rows[0]["real_name"]?.ToString() ?? txtUser.Text.Trim();
                    DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show("用户名或密码错误");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("数据库连接失败：" + ex.Message);
            }
        }
    }
}
