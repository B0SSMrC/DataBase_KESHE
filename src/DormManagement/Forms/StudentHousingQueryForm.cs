using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    /// <summary>学生住宿查询：按学号/姓名关键字查 v_StudentHousing 视图（要求 3）</summary>
    public class StudentHousingQueryForm : Form
    {
        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly TextBox txtKeyword = new() { Width = 160 };

        public StudentHousingQueryForm()
        {
            Text = "学生住宿查询"; Width = 820; Height = 520;
            StartPosition = FormStartPosition.CenterParent;

            var btnQuery = new Button { Text = "查询", Width = 70 };
            var btnAll = new Button { Text = "显示全部", Width = 80 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="学号/姓名关键字", AutoSize=true }, txtKeyword, btnQuery, btnAll });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => Query();
            btnQuery.Click += (_, _) => Query();
            btnAll.Click += (_, _) => { txtKeyword.Clear(); Query(); };
            txtKeyword.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) Query(); };
        }

        void Query()
        {
            grid.DataSource = DBHelper.QueryTable(
                @"SELECT * FROM v_StudentHousing
                  WHERE [学号] LIKE @k OR [姓名] LIKE @k
                  ORDER BY [楼号], [房号], [床号]",
                new SqlParameter("@k", "%" + txtKeyword.Text.Trim() + "%"));
        }
    }
}
