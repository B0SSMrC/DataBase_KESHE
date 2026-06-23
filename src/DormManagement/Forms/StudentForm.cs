using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    public class StudentForm : Form
    {
        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly TextBox txtNo = new() { Width = 100, MaxLength = 20 };
        readonly TextBox txtName = new() { Width = 90, MaxLength = 50 };
        readonly ComboBox cmbGender = new() { Width = 60, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly ComboBox cmbClass = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox txtPhone = new() { Width = 120, MaxLength = 20 };

        public StudentForm()
        {
            Text = "学生管理"; Width = 820; Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            cmbGender.Items.AddRange(new object[] { "男", "女" });

            var btnAdd = new Button { Text = "新增", Width = 70 };
            var btnUpd = new Button { Text = "修改", Width = 70 };
            var btnDel = new Button { Text = "删除", Width = 70 };
            var btnClear = new Button { Text = "清空", Width = 70 };
            var btnRefresh = new Button { Text = "刷新", Width = 70 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 110, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="学号", AutoSize=true }, txtNo,
                new Label{ Text="姓名", AutoSize=true }, txtName,
                new Label{ Text="性别", AutoSize=true }, cmbGender,
                new Label{ Text="班级", AutoSize=true }, cmbClass,
                new Label{ Text="电话", AutoSize=true }, txtPhone,
                btnAdd, btnUpd, btnDel, btnClear, btnRefresh });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => { LoadClasses(); LoadData(); };
            btnRefresh.Click += (_, _) => LoadData();
            grid.SelectionChanged += (_, _) => FillInputs();
            btnClear.Click += (_, _) => ClearInputs();
            btnAdd.Click += (_, _) => Add();
            btnUpd.Click += (_, _) => Upd();
            btnDel.Click += (_, _) => Del();
        }

        void LoadClasses()
        {
            cmbClass.DisplayMember = "class_name";
            cmbClass.ValueMember = "class_id";
            cmbClass.DataSource = DBHelper.QueryTable("SELECT class_id, class_name FROM ClassInfo ORDER BY class_id");
            cmbClass.SelectedIndex = -1;
        }

        void LoadData()
        {
            grid.DataSource = DBHelper.QueryTable(
                @"SELECT s.student_id AS 编号, s.student_no AS 学号, s.name AS 姓名, s.gender AS 性别,
                         c.class_name AS 班级, s.phone AS 电话
                  FROM Student s LEFT JOIN ClassInfo c ON s.class_id=c.class_id
                  ORDER BY s.student_id");
            if (grid.Columns.Contains("编号")) grid.Columns["编号"].Visible = false;
        }

        int? CurrentId() => grid.CurrentRow?.Cells["编号"].Value is int v ? v : (int?)null;

        object ClassIdParam() => cmbClass.SelectedValue is int cid ? cid : DBNull.Value;

        void FillInputs()
        {
            var row = grid.CurrentRow;
            if (row == null) return;
            txtNo.Text = row.Cells["学号"].Value?.ToString() ?? "";
            txtName.Text = row.Cells["姓名"].Value?.ToString() ?? "";
            cmbGender.Text = row.Cells["性别"].Value?.ToString() ?? "";
            txtPhone.Text = row.Cells["电话"].Value?.ToString() ?? "";
            var cls = row.Cells["班级"].Value?.ToString() ?? "";
            cmbClass.SelectedIndex = string.IsNullOrEmpty(cls) ? -1 : cmbClass.FindStringExact(cls);
        }

        void ClearInputs()
        {
            txtNo.Clear(); txtName.Clear(); txtPhone.Clear();
            cmbGender.SelectedIndex = -1; cmbClass.SelectedIndex = -1;
        }

        void Add()
        {
            if (txtNo.Text.Trim() == "" || txtName.Text.Trim() == "") { MessageBox.Show("学号和姓名必填"); return; }
            try
            {
                DBHelper.ExecuteLogged(
                    "INSERT INTO Student(student_no,name,gender,class_id,phone) VALUES(@no,@nm,@g,@c,@p)",
                    new[] {
                        new SqlParameter("@no", txtNo.Text.Trim()),
                        new SqlParameter("@nm", txtName.Text.Trim()),
                        new SqlParameter("@g", NullIfEmpty(cmbGender.Text)),
                        new SqlParameter("@c", ClassIdParam()),
                        new SqlParameter("@p", NullIfEmpty(txtPhone.Text)) },
                    "学生", "新增", $"新增学生 {txtName.Text.Trim()}({txtNo.Text.Trim()})");
                LoadData();
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601) { MessageBox.Show("学号已存在"); }
        }

        void Upd()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            try
            {
                DBHelper.ExecuteLogged(
                    "UPDATE Student SET student_no=@no,name=@nm,gender=@g,class_id=@c,phone=@p WHERE student_id=@id",
                    new[] {
                        new SqlParameter("@no", txtNo.Text.Trim()),
                        new SqlParameter("@nm", txtName.Text.Trim()),
                        new SqlParameter("@g", NullIfEmpty(cmbGender.Text)),
                        new SqlParameter("@c", ClassIdParam()),
                        new SqlParameter("@p", NullIfEmpty(txtPhone.Text)),
                        new SqlParameter("@id", id) },
                    "学生", "修改", $"修改学生 {txtName.Text.Trim()}({txtNo.Text.Trim()})");
                LoadData();
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601) { MessageBox.Show("学号已存在"); }
        }

        void Del()
        {
            if (CurrentId() is not int id) { MessageBox.Show("请先选择一行"); return; }
            if (MessageBox.Show("确认删除该学生？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            var sno = grid.CurrentRow?.Cells["学号"].Value?.ToString() ?? "";
            var sname = grid.CurrentRow?.Cells["姓名"].Value?.ToString() ?? "";
            try
            {
                DBHelper.ExecuteLogged("DELETE FROM Student WHERE student_id=@id",
                    new[] { new SqlParameter("@id", id) },
                    "学生", "删除", $"删除学生 {sname}({sno})");
                LoadData();
            }
            catch (SqlException ex) when (ex.Number == 547) { MessageBox.Show("该学生有住宿/记录，需先退宿后再删除"); }
        }

        static object NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
    }
}
