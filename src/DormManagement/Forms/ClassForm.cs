using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    public class ClassForm : Form
    {
        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly TextBox txtName = new() { Width = 140 };
        readonly TextBox txtMajor = new() { Width = 180 };

        public ClassForm()
        {
            Text = "班级管理"; Width = 700; Height = 520;
            StartPosition = FormStartPosition.CenterParent;

            var btnAdd = new Button { Text = "新增", Width = 70 };
            var btnUpd = new Button { Text = "修改", Width = 70 };
            var btnDel = new Button { Text = "删除", Width = 70 };
            var btnClear = new Button { Text = "清空", Width = 70 };
            var btnRefresh = new Button { Text = "刷新", Width = 70 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="班级名", AutoSize=true }, txtName,
                new Label{ Text="专业", AutoSize=true }, txtMajor,
                btnAdd, btnUpd, btnDel, btnClear, btnRefresh });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => LoadData();
            btnRefresh.Click += (_, _) => LoadData();
            grid.SelectionChanged += (_, _) => FillInputs();
            btnClear.Click += (_, _) => { txtName.Clear(); txtMajor.Clear(); };
            btnAdd.Click += (_, _) => Add();
            btnUpd.Click += (_, _) => Upd();
            btnDel.Click += (_, _) => Del();
        }

        void LoadData()
        {
            grid.DataSource = DBHelper.QueryTable(
                "SELECT class_id AS 编号, class_name AS 班级名, major AS 专业 FROM ClassInfo ORDER BY class_id");
            if (grid.Columns.Contains("编号")) grid.Columns["编号"].Visible = false;
        }

        int? CurrentId() => grid.CurrentRow?.Cells["编号"].Value is int v ? v : (int?)null;

        void FillInputs()
        {
            var row = grid.CurrentRow;
            if (row == null) return;
            txtName.Text = row.Cells["班级名"].Value?.ToString() ?? "";
            txtMajor.Text = row.Cells["专业"].Value?.ToString() ?? "";
        }

        void Add()
        {
            if (txtName.Text.Trim() == "" || txtMajor.Text.Trim() == "") { MessageBox.Show("班级名和专业都要填"); return; }
            try
            {
                DBHelper.Execute("INSERT INTO ClassInfo(class_name,major) VALUES(@n,@m)",
                    new SqlParameter("@n", txtName.Text.Trim()),
                    new SqlParameter("@m", txtMajor.Text.Trim()));
                LoadData();
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601) { MessageBox.Show("班级名已存在"); }
        }

        void Upd()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            DBHelper.Execute("UPDATE ClassInfo SET class_name=@n,major=@m WHERE class_id=@id",
                new SqlParameter("@n", txtName.Text.Trim()),
                new SqlParameter("@m", txtMajor.Text.Trim()),
                new SqlParameter("@id", id));
            LoadData();
        }

        void Del()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            if (MessageBox.Show("确认删除该班级？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                DBHelper.Execute("DELETE FROM ClassInfo WHERE class_id=@id", new SqlParameter("@id", id));
                LoadData();
            }
            catch (SqlException ex) when (ex.Number == 547) { MessageBox.Show("该班级下还有学生，无法删除"); }
        }
    }
}
