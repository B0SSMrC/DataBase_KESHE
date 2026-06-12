using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    /// <summary>寝室名单：输入楼号 + 房号，调用存储过程 usp_GetRoomStudents 返回名单（要求 6）</summary>
    public class RoomRosterForm : Form
    {
        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly ComboBox cmbBuilding = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox txtRoom = new() { Width = 90 };

        public RoomRosterForm()
        {
            Text = "寝室名单（存储过程）"; Width = 720; Height = 500;
            StartPosition = FormStartPosition.CenterParent;

            var btnQuery = new Button { Text = "查询名单", Width = 90 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="楼号", AutoSize=true }, cmbBuilding,
                new Label{ Text="房号", AutoSize=true }, txtRoom,
                btnQuery });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => LoadBuildings();
            btnQuery.Click += (_, _) => Query();
            txtRoom.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Query(); };
        }

        void LoadBuildings()
        {
            // 楼号既作显示也作传给存储过程的值（@building_no 为字符串）
            cmbBuilding.DisplayMember = "building_no";
            cmbBuilding.ValueMember = "building_no";
            cmbBuilding.DataSource = DBHelper.QueryTable("SELECT building_no FROM Building ORDER BY building_id");
            cmbBuilding.SelectedIndex = -1;
        }

        void Query()
        {
            var buildingNo = cmbBuilding.SelectedValue as string ?? cmbBuilding.Text;
            if (string.IsNullOrWhiteSpace(buildingNo)) { MessageBox.Show("请选择楼号"); return; }
            if (txtRoom.Text.Trim() == "") { MessageBox.Show("请填写房号"); return; }
            grid.DataSource = DBHelper.ProcTable("usp_GetRoomStudents",
                new SqlParameter("@building_no", buildingNo),
                new SqlParameter("@room_no", txtRoom.Text.Trim()));
            if (grid.Rows.Count == 0) MessageBox.Show("该寝室暂无住宿学生");
        }
    }
}
