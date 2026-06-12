using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    /// <summary>退宿：选在住学生 → 删除 Allocation（触发器自动更新人数/标空闲）并记日志</summary>
    public class CheckOutForm : Form
    {
        readonly string _operator;

        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        public CheckOutForm(string op)
        {
            _operator = op;
            Text = "退宿"; Width = 820; Height = 520;
            StartPosition = FormStartPosition.CenterParent;

            var btnOut = new Button { Text = "退宿", Width = 80 };
            var btnRefresh = new Button { Text = "刷新", Width = 70 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="在上表选择要退宿的学生", AutoSize=true }, btnOut, btnRefresh });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => LoadStudents();
            btnRefresh.Click += (_, _) => LoadStudents();
            btnOut.Click += (_, _) => CheckOut();
        }

        void LoadStudents()
        {
            grid.DataSource = DBHelper.QueryTable(
                @"SELECT s.student_id AS 编号, a.bed_id AS 床位ID, s.student_no AS 学号, s.name AS 姓名,
                         b.building_no AS 楼号, r.room_no AS 房号, bd.bed_no AS 床号
                  FROM Allocation a
                  JOIN Student s  ON a.student_id = s.student_id
                  JOIN Bed bd     ON a.bed_id     = bd.bed_id
                  JOIN Room r     ON bd.room_id   = r.room_id
                  JOIN Building b ON r.building_id = b.building_id
                  ORDER BY s.student_id");
            if (grid.Columns.Contains("编号")) grid.Columns["编号"].Visible = false;
            if (grid.Columns.Contains("床位ID")) grid.Columns["床位ID"].Visible = false;
        }

        int? CurrentStudentId() => grid.CurrentRow?.Cells["编号"].Value is int v ? v : (int?)null;
        int? CurrentBedId() => grid.CurrentRow?.Cells["床位ID"].Value is int v ? v : (int?)null;

        void CheckOut()
        {
            if (CurrentStudentId() is not int sid) { MessageBox.Show("请先在上表选择一名在住学生"); return; }
            var name = grid.CurrentRow?.Cells["姓名"].Value?.ToString() ?? "";
            if (MessageBox.Show($"确认为【{name}】办理退宿？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            object bedParam = CurrentBedId() is int bid ? bid : DBNull.Value;
            DBHelper.ExecuteLogged("DELETE FROM Allocation WHERE student_id=@s",
                new[] { new SqlParameter("@s", sid) },
                "退宿", "删除", $"退宿 {name}");
            DBHelper.Execute("INSERT INTO HousingLog(student_id,bed_id,op_type,operator) VALUES(@s,@b,'退宿',@op)",
                new SqlParameter("@s", sid), new SqlParameter("@b", bedParam),
                new SqlParameter("@op", NullIfEmpty(_operator)));
            MessageBox.Show("退宿成功");
            LoadStudents();
        }

        static object NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
    }
}
