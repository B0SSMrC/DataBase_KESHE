using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    /// <summary>入住登记：选未入住学生 + 楼→房→空床级联选床 → 写入 Allocation 并记日志</summary>
    public class CheckInForm : Form
    {
        readonly string _operator;

        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly ComboBox cmbBuilding = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox cmbRoom = new() { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox cmbBed = new() { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        bool _suppress;   // 重新绑定下拉时抑制级联事件

        public CheckInForm(string op)
        {
            _operator = op;
            Text = "入住登记"; Width = 840; Height = 540;
            StartPosition = FormStartPosition.CenterParent;

            var btnIn = new Button { Text = "入住", Width = 80 };
            var btnRefresh = new Button { Text = "刷新", Width = 70 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="未入住学生见上表 ▏ 选床位：楼", AutoSize=true }, cmbBuilding,
                new Label{ Text="房", AutoSize=true }, cmbRoom,
                new Label{ Text="空床", AutoSize=true }, cmbBed,
                btnIn, btnRefresh });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => { LoadBuildings(); LoadStudents(); };
            btnRefresh.Click += (_, _) => { LoadStudents(); LoadBeds(); };
            cmbBuilding.SelectedIndexChanged += (_, _) => { if (!_suppress) LoadRooms(); };
            cmbRoom.SelectedIndexChanged += (_, _) => { if (!_suppress) LoadBeds(); };
            btnIn.Click += (_, _) => CheckIn();
        }

        void LoadStudents()
        {
            grid.DataSource = DBHelper.QueryTable(
                @"SELECT s.student_id AS 编号, s.student_no AS 学号, s.name AS 姓名,
                         s.gender AS 性别, c.class_name AS 班级
                  FROM Student s LEFT JOIN ClassInfo c ON s.class_id=c.class_id
                  WHERE s.student_id NOT IN (SELECT student_id FROM Allocation)
                  ORDER BY s.student_id");
            if (grid.Columns.Contains("编号")) grid.Columns["编号"].Visible = false;
        }

        void LoadBuildings()
        {
            _suppress = true;
            cmbBuilding.DisplayMember = "building_no";
            cmbBuilding.ValueMember = "building_id";
            cmbBuilding.DataSource = DBHelper.QueryTable("SELECT building_id, building_no FROM Building ORDER BY building_id");
            cmbBuilding.SelectedIndex = -1;
            _suppress = false;
        }

        void LoadRooms()
        {
            _suppress = true;
            if (cmbBuilding.SelectedValue is int bid)
            {
                cmbRoom.DisplayMember = "room_no";
                cmbRoom.ValueMember = "room_id";
                cmbRoom.DataSource = DBHelper.QueryTable(
                    "SELECT room_id, room_no FROM Room WHERE building_id=@b ORDER BY room_id",
                    new SqlParameter("@b", bid));
                cmbRoom.SelectedIndex = -1;
            }
            else cmbRoom.DataSource = null;
            _suppress = false;
            LoadBeds();
        }

        void LoadBeds()
        {
            if (cmbRoom.SelectedValue is int rid)
            {
                cmbBed.DisplayMember = "bed_no";
                cmbBed.ValueMember = "bed_id";
                cmbBed.DataSource = DBHelper.QueryTable(
                    @"SELECT bed_id, bed_no FROM Bed
                      WHERE room_id=@r AND bed_id NOT IN (SELECT bed_id FROM Allocation)
                      ORDER BY bed_no",
                    new SqlParameter("@r", rid));
                cmbBed.SelectedIndex = -1;
            }
            else cmbBed.DataSource = null;
        }

        int? CurrentStudentId() => grid.CurrentRow?.Cells["编号"].Value is int v ? v : (int?)null;

        void CheckIn()
        {
            if (CurrentStudentId() is not int sid) { MessageBox.Show("请先在上表选择一名学生"); return; }
            if (cmbBed.SelectedValue is not int bid) { MessageBox.Show("请选择楼、房、空床"); return; }
            if (GenderMismatch(sid, bid, out var gmsg)) { MessageBox.Show(gmsg); return; }
            try
            {
                var sno = grid.CurrentRow?.Cells["学号"].Value?.ToString() ?? "";
                var sname = grid.CurrentRow?.Cells["姓名"].Value?.ToString() ?? "";
                // 分配 + 住宿流水在同一事务内（ExecuteLogged 还会一并记 OperationLog）
                DBHelper.ExecuteLogged(
                    @"INSERT INTO Allocation(student_id,bed_id) VALUES(@s,@b);
                      INSERT INTO HousingLog(student_id,bed_id,op_type,operator) VALUES(@s,@b,N'入住',@op);",
                    new[] {
                        new SqlParameter("@s", sid), new SqlParameter("@b", bid),
                        new SqlParameter("@op", NullIfEmpty(_operator)) },
                    "入住", "新增", $"入住 {sname}({sno})");
                MessageBox.Show("入住成功");
                LoadStudents(); LoadBeds();
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            { MessageBox.Show("该学生已入住，或该床位已被占用"); }
        }

        static object NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

        // 性别一致性：学生性别与床位所属楼的 gender_type 不符则拦截（任一为空则放行）
        static bool GenderMismatch(int sid, int bid, out string msg)
        {
            msg = "";
            var gender = DBHelper.Scalar("SELECT gender FROM Student WHERE student_id=@s",
                new SqlParameter("@s", sid))?.ToString();
            var btype = DBHelper.Scalar(
                @"SELECT b.gender_type FROM Bed bd JOIN Room r ON bd.room_id=r.room_id
                  JOIN Building b ON r.building_id=b.building_id WHERE bd.bed_id=@b",
                new SqlParameter("@b", bid))?.ToString();
            if (!string.IsNullOrEmpty(gender) && !string.IsNullOrEmpty(btype) && gender != btype)
            { msg = $"性别不符：{gender}生不能入住「{btype}」楼"; return true; }
            return false;
        }
    }
}
