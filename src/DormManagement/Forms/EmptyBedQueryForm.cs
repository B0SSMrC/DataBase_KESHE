using System.Data;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    /// <summary>空床位查询：可按楼/房号筛选，列出当前未被占用的床位（要求 3、5）</summary>
    public class EmptyBedQueryForm : Form
    {
        readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        readonly ComboBox cmbBuilding = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        readonly TextBox txtRoom = new() { Width = 90 };

        public EmptyBedQueryForm()
        {
            Text = "空床位查询"; Width = 720; Height = 520;
            StartPosition = FormStartPosition.CenterParent;

            var btnQuery = new Button { Text = "查询", Width = 70 };
            var btnAll = new Button { Text = "显示全部", Width = 80 };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[] {
                new Label{ Text="楼(可空)", AutoSize=true }, cmbBuilding,
                new Label{ Text="房号(可空)", AutoSize=true }, txtRoom,
                btnQuery, btnAll });

            Controls.Add(grid); Controls.Add(panel);

            Load += (_, _) => { LoadBuildings(); Query(); };
            btnQuery.Click += (_, _) => Query();
            btnAll.Click += (_, _) => { cmbBuilding.SelectedIndex = -1; txtRoom.Clear(); Query(); };
        }

        void LoadBuildings()
        {
            cmbBuilding.DisplayMember = "building_no";
            cmbBuilding.ValueMember = "building_id";
            cmbBuilding.DataSource = DBHelper.QueryTable("SELECT building_id, building_no FROM Building ORDER BY building_id");
            cmbBuilding.SelectedIndex = -1;
        }

        void Query()
        {
            object bidParam = cmbBuilding.SelectedValue is int bid ? bid : DBNull.Value;
            grid.DataSource = DBHelper.QueryTable(
                @"SELECT b.building_no AS 楼号, r.room_no AS 房号, bd.bed_no AS 床号
                  FROM Bed bd
                  JOIN Room r     ON bd.room_id   = r.room_id
                  JOIN Building b ON r.building_id = b.building_id
                  WHERE bd.bed_id NOT IN (SELECT bed_id FROM Allocation)
                    AND (@bid IS NULL OR r.building_id = @bid)
                    AND (@rno = N'' OR r.room_no = @rno)
                  ORDER BY b.building_no, r.room_no, bd.bed_no",
                new SqlParameter("@bid", bidParam),
                new SqlParameter("@rno", txtRoom.Text.Trim()));
        }
    }
}
