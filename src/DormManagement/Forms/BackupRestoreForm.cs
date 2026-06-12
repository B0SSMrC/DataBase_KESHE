using System.IO;
using Microsoft.Data.SqlClient;
using DormManagement.DAL;

namespace DormManagement.Forms
{
    /// <summary>备份与恢复（要求 8）：完整备份 / 日志备份 / 基于时间点(STOPAT)的恢复。
    /// 备份恢复命令须连到 master 执行，故本窗体单独使用 Database=master 的连接串。</summary>
    public class BackupRestoreForm : Form
    {
        // 在主连接串基础上把目标库换成 master，得到执行备份/恢复用的连接串
        static readonly string MasterCs =
            new SqlConnectionStringBuilder(AppConfig.ConnectionString) { InitialCatalog = "master" }.ConnectionString;

        const string BackupDir = @"D:\DataBase\KESHE\backup";
        static string FullPath => Path.Combine(BackupDir, "DormDB_full.bak");
        static string LogPath  => Path.Combine(BackupDir, "DormDB_log.trn");

        readonly DateTimePicker dtp = new()
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm:ss",
            ShowUpDown = true,
            Width = 170
        };
        readonly TextBox txtLog = new()
        {
            Multiline = true, Dock = DockStyle.Bottom, Height = 120, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9), BackColor = Color.White
        };
        readonly DataGridView gridLog = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        public BackupRestoreForm()
        {
            Text = "备份与恢复"; Width = 740; Height = 480;
            StartPosition = FormStartPosition.CenterParent;

            var btnFull = new Button { Text = "完整备份", Width = 90 };
            var btnLog = new Button { Text = "日志备份", Width = 90 };
            var btnRestore = new Button { Text = "恢复到所选时间点", Width = 140 };
            var btnRefreshLog = new Button { Text = "刷新日志", Width = 80 };
            var btnBefore = new Button { Text = "恢复到选中之前", Width = 120 };
            var btnAfter = new Button { Text = "恢复到选中之后", Width = 120 };

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(8) };
            top.Controls.AddRange(new Control[] {
                btnFull, btnLog,
                new Label{ Text="时间点", AutoSize=true, Padding=new Padding(14,10,0,0) }, dtp, btnRestore,
                btnRefreshLog, btnBefore, btnAfter });

            Controls.Add(gridLog);  // Fill（先加）
            Controls.Add(txtLog);   // Bottom
            Controls.Add(top);      // Top（后加）

            Load += (_, _) =>
            {
                Directory.CreateDirectory(BackupDir);
                Append($"备份目录：{BackupDir}");
                Append("流程：完整备份 → 改数据/误操作 → 日志备份 →（刷新日志）选中一条操作 → 恢复到之前/之后。");
                LoadOpLog();
            };

            btnFull.Click += (_, _) => FullBackup();
            btnLog.Click += (_, _) => LogBackup();
            btnRestore.Click += (_, _) => RunRestore(dtp.Value);
            btnRefreshLog.Click += (_, _) => LoadOpLog();
            btnBefore.Click += (_, _) => RestoreBySelectedRow(before: true);
            btnAfter.Click += (_, _) => RestoreBySelectedRow(before: false);
            gridLog.SelectionChanged += (_, _) =>
            {
                if (gridLog.CurrentRow?.Cells["时间"].Value is DateTime t
                    && t >= dtp.MinDate && t <= dtp.MaxDate) dtp.Value = t;
            };
        }

        void FullBackup()
        {
            try
            {
                Directory.CreateDirectory(BackupDir);
                ExecMaster("BACKUP DATABASE DormDB TO DISK=@p WITH INIT, NAME=N'DormDB-Full'",
                    new SqlParameter("@p", FullPath));
                Append($"[完整备份] 成功 → {FullPath}");
                // 新的完整备份会让旧日志备份脱离日志链；删除它，避免被用于时间点恢复时报 LSN 链不匹配
                if (File.Exists(LogPath))
                {
                    try { File.Delete(LogPath); Append("已清除旧日志备份；时间点恢复前请重新做一次『日志备份』。"); }
                    catch (IOException) { Append("提示：旧日志备份未能删除，时间点恢复前请重新做『日志备份』覆盖它。"); }
                }
                MessageBox.Show("完整备份成功");
            }
            catch (SqlException ex) { Fail("完整备份", ex); MessageBox.Show("完整备份失败：" + ex.Message); }
        }

        void LogBackup()
        {
            try
            {
                Directory.CreateDirectory(BackupDir);
                ExecMaster("BACKUP LOG DormDB TO DISK=@p WITH INIT, NAME=N'DormDB-Log'",
                    new SqlParameter("@p", LogPath));
                Append($"[日志备份] 成功 → {LogPath}");
                MessageBox.Show("日志备份成功");
            }
            catch (SqlException ex) { Fail("日志备份", ex); }
        }

        void RunRestore(DateTime t)
        {
            if (!File.Exists(FullPath)) { MessageBox.Show("未找到完整备份文件，请先执行完整备份"); return; }
            if (!File.Exists(LogPath)) { MessageBox.Show("未找到日志备份文件，请先执行日志备份"); return; }

            if (MessageBox.Show(
                    $"将把 DormDB 恢复到 {t:yyyy-MM-dd HH:mm:ss.fff}。\n" +
                    "期间会断开该库所有连接并覆盖当前数据，确认继续？",
                    "确认时间点恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                ExecMaster(
                    @"ALTER DATABASE DormDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                      RESTORE DATABASE DormDB FROM DISK=@full WITH NORECOVERY, REPLACE;
                      RESTORE LOG DormDB FROM DISK=@log WITH STOPAT=@t, RECOVERY;
                      ALTER DATABASE DormDB SET MULTI_USER;",
                    new SqlParameter("@full", FullPath),
                    new SqlParameter("@log", LogPath),
                    new SqlParameter("@t", t));
                Append($"[时间点恢复] 成功，已恢复到 {t:yyyy-MM-dd HH:mm:ss.fff}");
                MessageBox.Show("恢复成功");
            }
            catch (SqlException ex)
            {
                Fail("时间点恢复", ex);
                BringOnline();
                MessageBox.Show("时间点恢复失败：" + ex.Message +
                    "\n\n已自动将数据库恢复到完整备份时刻并重新上线，可正常登录。");
            }
            LoadOpLog();   // 回滚后日志也回到该刻，刷新时间线
        }

        // 时间线选中一行 → 计算 STOPAT（+500ms 缓冲确保跨过该笔事务提交点）→ 恢复
        void RestoreBySelectedRow(bool before)
        {
            if (gridLog.CurrentRow?.Cells["编号"].Value is not int opId
                || gridLog.CurrentRow.Cells["时间"].Value is not DateTime opTime)
            { MessageBox.Show("请先在操作时间线中选择一条记录"); return; }

            DateTime stopAt;
            if (before)
            {
                var prev = DBHelper.Scalar("SELECT MAX(op_time) FROM OperationLog WHERE op_id < @id",
                    new SqlParameter("@id", opId));
                if (prev is null or DBNull)
                { MessageBox.Show("这已是最早的操作记录；如需更早请直接还原完整备份基线。"); return; }
                stopAt = Convert.ToDateTime(prev).AddMilliseconds(500);   // 含前一条，排除选中这条
            }
            else
            {
                stopAt = opTime.AddMilliseconds(500);   // 含选中这条
            }
            RunRestore(stopAt);
        }

        void LoadOpLog()
        {
            try
            {
                gridLog.DataSource = DBHelper.QueryTable(
                    @"SELECT op_id AS 编号, op_time AS 时间, category AS 类别, action AS 操作,
                             description AS 描述, operator AS 操作人
                      FROM OperationLog ORDER BY op_id DESC");
                if (gridLog.Columns.Contains("编号")) gridLog.Columns["编号"].Visible = false;
            }
            catch (SqlException ex) { Append("[操作时间线] 加载失败（OperationLog 是否已建？）：" + ex.Message); }
        }

        static void ExecMaster(string sql, params SqlParameter[] ps)
        {
            using var conn = new SqlConnection(MasterCs);
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            cmd.Parameters.AddRange(ps);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        // 失败兜底：若数据库停在 RESTORING(state=1) 则完成恢复让其上线，并恢复多用户访问
        void BringOnline()
        {
            try
            {
                ExecMaster("IF (SELECT state FROM sys.databases WHERE name=N'DormDB') = 1 RESTORE DATABASE DormDB WITH RECOVERY;");
                ExecMaster("IF (SELECT state FROM sys.databases WHERE name=N'DormDB') = 0 ALTER DATABASE DormDB SET MULTI_USER;");
                Append("[自动恢复] 数据库已重新上线 (ONLINE / MULTI_USER)。");
            }
            catch (SqlException ex) { Append("[自动恢复] 失败：" + ex.Message); }
        }

        void Fail(string op, SqlException ex) => Append($"[{op}] 失败：{ex.Message}");
        void Append(string line) => txtLog.AppendText(line + Environment.NewLine);
    }
}
