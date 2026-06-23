using DormManagement.DAL;

namespace DormManagement.Forms
{
    public class MainForm : Form
    {
        readonly string _operator;

        public MainForm(string loginName)
        {
            _operator = loginName;
            Session.CurrentOperator = loginName;   // 供操作日志记录使用
            try { DBHelper.EnsureOperationLog(); } catch { /* 启动期自愈失败不阻塞主界面 */ }
            try { DBHelper.EnsureUndoInfra(); } catch { /* 同上：撤销快照表自愈 */ }
            Text = "学生宿舍管理系统";
            Width = 920; Height = 620;
            StartPosition = FormStartPosition.CenterScreen;

            var welcome = new Label
            {
                Text = "欢迎使用学生宿舍管理系统",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 22, FontStyle.Bold)
            };

            var status = new StatusStrip();
            status.Items.Add(new ToolStripStatusLabel($"当前用户：{_operator}"));

            var menu = new MenuStrip();

            var mBase = new ToolStripMenuItem("基础信息");
            mBase.DropDownItems.Add("宿舍楼管理", null, (_, _) => Open(new BuildingForm()));
            mBase.DropDownItems.Add("班级管理", null, (_, _) => Open(new ClassForm()));
            mBase.DropDownItems.Add("学生管理", null, (_, _) => Open(new StudentForm()));
            mBase.DropDownItems.Add("寝室管理", null, (_, _) => Open(new RoomForm()));

            var mBiz = new ToolStripMenuItem("住宿业务");
            mBiz.DropDownItems.Add("入住登记", null, (_, _) => Open(new CheckInForm(_operator)));
            mBiz.DropDownItems.Add("调宿", null, (_, _) => Open(new TransferForm(_operator)));
            mBiz.DropDownItems.Add("退宿", null, (_, _) => Open(new CheckOutForm(_operator)));

            var mQuery = new ToolStripMenuItem("查询");
            mQuery.DropDownItems.Add("空床位查询", null, (_, _) => Open(new EmptyBedQueryForm()));
            mQuery.DropDownItems.Add("学生住宿查询", null, (_, _) => Open(new StudentHousingQueryForm()));
            mQuery.DropDownItems.Add("寝室名单", null, (_, _) => Open(new RoomRosterForm()));

            var mSys = new ToolStripMenuItem("系统");
            mSys.DropDownItems.Add("备份与恢复", null, (_, _) => Open(new BackupRestoreForm()));
            mSys.DropDownItems.Add("退出", null, (_, _) => Close());

            menu.Items.AddRange(new ToolStripItem[] { mBase, mBiz, mQuery, mSys });

            // 添加顺序：先 Fill，再 Bottom，最后 Top —— 保证布局正确
            Controls.Add(welcome);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;
        }

        // 以模态方式打开并在关闭后释放窗体（ShowDialog 不会自动 Dispose）
        static void Open(Form f) { using (f) f.ShowDialog(); }
    }
}
