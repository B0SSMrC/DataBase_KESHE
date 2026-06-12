using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    public class BuildingForm : Form
    {
        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly TextBox txtNo = new() { Width = 110 };
        readonly TextBox txtName = new() { Width = 140 };
        readonly ComboBox cmbGender = new() { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };

        public BuildingForm()
        {
            Text = "宿舍楼管理"; Width = 720; Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            cmbGender.Items.AddRange(new object[] { "男", "女" });

            var btnAdd = new Button { Text = "新增", Width = 70 };
            var btnUpd = new Button { Text = "修改", Width = 70 };
            var btnDel = new Button { Text = "删除", Width = 70 };
            var btnClear = new Button { Text = "清空", Width = 70 };
            var btnRefresh = new Button { Text = "刷新", Width = 70 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="楼号", AutoSize=true }, txtNo,
                new Label{ Text="楼名", AutoSize=true }, txtName,
                new Label{ Text="类别", AutoSize=true }, cmbGender,
                btnAdd, btnUpd, btnDel, btnClear, btnRefresh });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => LoadData();
            btnRefresh.Click += (_, _) => LoadData();
            grid.SelectionChanged += (_, _) => FillInputs();
            btnClear.Click += (_, _) => ClearInputs();
            btnAdd.Click += (_, _) => Add();
            btnUpd.Click += (_, _) => Upd();
            btnDel.Click += (_, _) => Del();
        }

        void LoadData()
        {
            grid.DataSource = DBHelper.QueryTable(
                "SELECT building_id AS 编号, building_no AS 楼号, building_name AS 楼名, gender_type AS 类别 FROM Building ORDER BY building_id");
            if (grid.Columns.Contains("编号")) grid.Columns["编号"].Visible = false;
        }

        int? CurrentId() => grid.CurrentRow?.Cells["编号"].Value is int v ? v : (int?)null;

        void FillInputs()
        {
            var row = grid.CurrentRow;
            if (row == null) return;
            txtNo.Text = row.Cells["楼号"].Value?.ToString() ?? "";
            txtName.Text = row.Cells["楼名"].Value?.ToString() ?? "";
            cmbGender.Text = row.Cells["类别"].Value?.ToString() ?? "";
        }

        void ClearInputs() { txtNo.Clear(); txtName.Clear(); cmbGender.SelectedIndex = -1; }

        void Add()
        {
            if (txtNo.Text.Trim() == "") { MessageBox.Show("请填写楼号"); return; }
            try
            {
                DBHelper.ExecuteLogged(
                    "INSERT INTO Building(building_no,building_name,gender_type) VALUES(@n,@m,@g)",
                    new[] {
                        new SqlParameter("@n", txtNo.Text.Trim()),
                        new SqlParameter("@m", NullIfEmpty(txtName.Text)),
                        new SqlParameter("@g", NullIfEmpty(cmbGender.Text)) },
                    "宿舍楼", "新增", $"新增宿舍楼 {txtNo.Text.Trim()}");
                LoadData();
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601) { MessageBox.Show("楼号已存在"); }
        }

        void Upd()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            DBHelper.ExecuteLogged(
                "UPDATE Building SET building_no=@n,building_name=@m,gender_type=@g WHERE building_id=@id",
                new[] {
                    new SqlParameter("@n", txtNo.Text.Trim()),
                    new SqlParameter("@m", NullIfEmpty(txtName.Text)),
                    new SqlParameter("@g", NullIfEmpty(cmbGender.Text)),
                    new SqlParameter("@id", id) },
                "宿舍楼", "修改", $"修改宿舍楼 {txtNo.Text.Trim()}（编号{id}）");
            LoadData();
        }

        void Del()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            if (MessageBox.Show("确认删除该宿舍楼？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            var no = grid.CurrentRow?.Cells["楼号"].Value?.ToString() ?? id.ToString();
            try
            {
                DBHelper.ExecuteLogged("DELETE FROM Building WHERE building_id=@id",
                    new[] { new SqlParameter("@id", id) },
                    "宿舍楼", "删除", $"删除宿舍楼 {no}");
                LoadData();
            }
            catch (SqlException ex) when (ex.Number == 547) { MessageBox.Show("该楼下还有寝室，无法删除"); }
        }

        static object NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
    }
}
