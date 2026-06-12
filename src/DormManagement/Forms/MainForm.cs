namespace DormManagement.Forms
{
    public class MainForm : Form
    {
        readonly string _operator;

        public MainForm(string loginName)
        {
            _operator = loginName;
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
            mBase.DropDownItems.Add("宿舍楼管理", null, (_, _) => new BuildingForm().ShowDialog());
            mBase.DropDownItems.Add("班级管理", null, (_, _) => new ClassForm().ShowDialog());
            mBase.DropDownItems.Add("学生管理", null, (_, _) => new StudentForm().ShowDialog());
            mBase.DropDownItems.Add("寝室管理", null, (_, _) => new RoomForm().ShowDialog());

            var mBiz = new ToolStripMenuItem("住宿业务");
            mBiz.DropDownItems.Add("入住登记", null, (_, _) => new CheckInForm(_operator).ShowDialog());
            mBiz.DropDownItems.Add("调宿", null, (_, _) => new TransferForm(_operator).ShowDialog());
            mBiz.DropDownItems.Add("退宿", null, (_, _) => new CheckOutForm(_operator).ShowDialog());

            var mQuery = new ToolStripMenuItem("查询");
            mQuery.DropDownItems.Add("空床位查询", null, (_, _) => new EmptyBedQueryForm().ShowDialog());
            mQuery.DropDownItems.Add("学生住宿查询", null, (_, _) => new StudentHousingQueryForm().ShowDialog());
            mQuery.DropDownItems.Add("寝室名单", null, (_, _) => new RoomRosterForm().ShowDialog());

            var mSys = new ToolStripMenuItem("系统");
            mSys.DropDownItems.Add("备份与恢复", null, (_, _) => new BackupRestoreForm().ShowDialog());
            mSys.DropDownItems.Add("退出", null, (_, _) => Close());

            menu.Items.AddRange(new ToolStripItem[] { mBase, mBiz, mQuery, mSys });

            // 添加顺序：先 Fill，再 Bottom，最后 Top —— 保证布局正确
            Controls.Add(welcome);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;
        }
    }
}
