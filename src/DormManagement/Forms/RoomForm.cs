using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    public class RoomForm : Form
    {
        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly ComboBox cmbBuilding = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox txtRoomNo = new() { Width = 90 };
        readonly NumericUpDown numCap = new() { Width = 60, Minimum = 1, Maximum = 8, Value = 4 };

        public RoomForm()
        {
            Text = "寝室管理"; Width = 780; Height = 520;
            StartPosition = FormStartPosition.CenterParent;

            var btnAdd = new Button { Text = "新增(含建床位)", Width = 110 };
            var btnRename = new Button { Text = "改房号", Width = 80 };
            var btnDel = new Button { Text = "删除", Width = 70 };
            var btnRefresh = new Button { Text = "刷新", Width = 70 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="楼", AutoSize=true }, cmbBuilding,
                new Label{ Text="房号", AutoSize=true }, txtRoomNo,
                new Label{ Text="床位数", AutoSize=true }, numCap,
                btnAdd, btnRename, btnDel, btnRefresh });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => { LoadBuildings(); LoadData(); };
            btnRefresh.Click += (_, _) => LoadData();
            grid.SelectionChanged += (_, _) => FillInputs();
            btnAdd.Click += (_, _) => Add();
            btnRename.Click += (_, _) => Rename();
            btnDel.Click += (_, _) => Del();
        }

        void LoadBuildings()
        {
            cmbBuilding.DisplayMember = "building_no";
            cmbBuilding.ValueMember = "building_id";
            cmbBuilding.DataSource = DBHelper.QueryTable("SELECT building_id, building_no FROM Building ORDER BY building_id");
            cmbBuilding.SelectedIndex = -1;
        }

        void LoadData()
        {
            grid.DataSource = DBHelper.QueryTable(
                @"SELECT r.room_id AS 编号, b.building_no AS 楼号, r.room_no AS 房号,
                         r.capacity AS 床位数, r.occupied_count AS 已住, r.status AS 状态
                  FROM Room r JOIN Building b ON r.building_id=b.building_id
                  ORDER BY r.room_id");
            if (grid.Columns.Contains("编号")) grid.Columns["编号"].Visible = false;
        }

        int? CurrentId() => grid.CurrentRow?.Cells["编号"].Value is int v ? v : (int?)null;
        int CurrentOccupied() => grid.CurrentRow?.Cells["已住"].Value is int v ? v : 0;

        void FillInputs()
        {
            var row = grid.CurrentRow;
            if (row == null) return;
            txtRoomNo.Text = row.Cells["房号"].Value?.ToString() ?? "";
            var bno = row.Cells["楼号"].Value?.ToString() ?? "";
            cmbBuilding.SelectedIndex = string.IsNullOrEmpty(bno) ? -1 : cmbBuilding.FindStringExact(bno);
        }

        void Add()
        {
            if (cmbBuilding.SelectedValue is not int bid) { MessageBox.Show("请选择宿舍楼"); return; }
            if (txtRoomNo.Text.Trim() == "") { MessageBox.Show("请填写房号"); return; }
            int cap = (int)numCap.Value;
            try
            {
                // 插入寝室并取回新 room_id
                var idObj = DBHelper.Scalar(
                    "INSERT INTO Room(building_id,room_no,capacity) OUTPUT INSERTED.room_id VALUES(@b,@r,@c)",
                    new SqlParameter("@b", bid),
                    new SqlParameter("@r", txtRoomNo.Text.Trim()),
                    new SqlParameter("@c", cap));
                int roomId = Convert.ToInt32(idObj);
                // 按床位数自动生成床位 1..cap
                for (int i = 1; i <= cap; i++)
                    DBHelper.Execute("INSERT INTO Bed(room_id,bed_no) VALUES(@r,@n)",
                        new SqlParameter("@r", roomId),
                        new SqlParameter("@n", i.ToString()));
                LoadData();
                MessageBox.Show($"已新增寝室并自动生成 {cap} 张床位");
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601) { MessageBox.Show("该楼下房号已存在"); }
        }

        void Rename()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            if (txtRoomNo.Text.Trim() == "") { MessageBox.Show("请填写新房号"); return; }
            try
            {
                DBHelper.Execute("UPDATE Room SET room_no=@r WHERE room_id=@id",
                    new SqlParameter("@r", txtRoomNo.Text.Trim()),
                    new SqlParameter("@id", id));
                LoadData();
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601) { MessageBox.Show("该楼下房号已存在"); }
        }

        void Del()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            if (CurrentOccupied() > 0) { MessageBox.Show("寝室内仍有学生住宿，不能删除"); return; }
            if (MessageBox.Show("确认删除该寝室及其床位？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                // 事务内先删床位再删寝室
                DBHelper.Execute(
                    @"BEGIN TRY BEGIN TRAN;
                        DELETE FROM Bed WHERE room_id=@id;
                        DELETE FROM Room WHERE room_id=@id;
                      COMMIT; END TRY
                      BEGIN CATCH ROLLBACK; THROW; END CATCH",
                    new SqlParameter("@id", id));
                LoadData();
            }
            catch (SqlException ex) when (ex.Number == 547) { MessageBox.Show("寝室内有住宿记录，无法删除"); }
        }
    }
}
