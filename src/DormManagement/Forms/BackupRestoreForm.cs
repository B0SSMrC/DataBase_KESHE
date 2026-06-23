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
            var btnUndo = new Button { Text = "撤销到选中操作之前", Width = 150 };

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(8) };
            top.Controls.AddRange(new Control[] {
                new Label{ Text="【数据库备份/恢复·要求8】", AutoSize=true, Padding=new Padding(0,10,0,0) },
                btnFull, btnLog,
                new Label{ Text="时间点", AutoSize=true, Padding=new Padding(10,10,0,0) }, dtp, btnRestore,
                new Label{ Text="▏【操作撤销】", AutoSize=true, Padding=new Padding(12,10,0,0) },
                btnRefreshLog, btnUndo });

            Controls.Add(gridLog);  // Fill（先加）
            Controls.Add(txtLog);   // Bottom
            Controls.Add(top);      // Top（后加）

            Load += (_, _) =>
            {
                try { Directory.CreateDirectory(BackupDir); Append($"备份目录：{BackupDir}"); }
                catch (Exception ex) { Append($"⚠ 备份目录不可用（{BackupDir}）：{ex.Message}。如本机无该路径，请把 BackupRestoreForm.BackupDir 改成本机可写目录。"); }
                Append("【备份/恢复·要求8】完整/日志备份 + 恢复到所选时间点（SQL Server PITR）。");
                Append("【操作撤销】在下方时间线选中一条操作，点『撤销到选中操作之前』即可回退到该操作之前（不依赖备份文件、不锁库）。");
                LoadOpLog();
            };

            btnFull.Click += (_, _) => FullBackup();
            btnLog.Click += (_, _) => LogBackup();
            btnRestore.Click += (_, _) => RunRestore(dtp.Value);
            btnRefreshLog.Click += (_, _) => LoadOpLog();
            btnUndo.Click += (_, _) => UndoToBeforeSelected();
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
            catch (Exception ex) { Fail("完整备份", ex); MessageBox.Show("完整备份失败：" + ex.Message); }
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
            catch (Exception ex) { Fail("日志备份", ex); MessageBox.Show("日志备份失败：" + ex.Message); }
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
                // 还原完整备份 + 前滚日志到 STOPAT；不在同批 SET MULTI_USER，
                // 否则当 STOPAT 越过日志末尾、库停在 Restoring 时，ALTER 会失败
                ExecMaster(
                    @"ALTER DATABASE DormDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                      RESTORE DATABASE DormDB FROM DISK=@full WITH NORECOVERY, REPLACE;
                      RESTORE LOG DormDB FROM DISK=@log WITH STOPAT=@t, RECOVERY;",
                    new SqlParameter("@full", FullPath),
                    new SqlParameter("@log", LogPath),
                    new SqlParameter("@t", t));

                bool stillRestoring = Convert.ToInt32(
                    ScalarMaster("SELECT state FROM sys.databases WHERE name=N'DormDB'")) == 1;
                if (stillRestoring)
                {
                    // STOPAT 晚于日志备份最后一条记录：当前日志没覆盖到该时间点
                    BringOnline();   // 恢复到日志最新时刻并上线，避免卡在 Restoring
                    Append($"[时间点恢复] 所选时间点 {t:yyyy-MM-dd HH:mm:ss} 超出当前日志备份范围，已恢复到日志最新时刻。");
                    MessageBox.Show(
                        "所选时间点超出当前『日志备份』的覆盖范围，已恢复到日志中最新的时刻。\n\n" +
                        "要回滚到某操作之前，请确保顺序：完整备份 → 执行(并完成)要回滚的操作 → 点【日志备份】" +
                        "（让日志覆盖到该时间点）→ 再选该操作恢复。");
                }
                else
                {
                    ExecMaster("ALTER DATABASE DormDB SET MULTI_USER;");
                    Append($"[时间点恢复] 成功，已恢复到 {t:yyyy-MM-dd HH:mm:ss.fff}");
                    MessageBox.Show("恢复成功");
                }
            }
            catch (SqlException ex)
            {
                Fail("时间点恢复", ex);
                BringOnline();
                MessageBox.Show("时间点恢复失败：" + ex.Message + "\n\n已自动将数据库重新上线，可正常登录。");
            }
            LoadOpLog();   // 刷新时间线
        }

        // 撤销到选中操作之前：重载该操作执行前的整库快照（纯 DML，不依赖备份文件、不锁库）
        void UndoToBeforeSelected()
        {
            if (gridLog.CurrentRow?.Cells["编号"].Value is not int opId)
            { MessageBox.Show("请先在操作时间线中选择一条记录"); return; }
            if (!DBHelper.SnapshotExists(opId))
            { MessageBox.Show("该操作没有可用快照（多为本功能上线前的旧操作），无法撤销。"); return; }

            var desc = gridLog.CurrentRow.Cells["描述"].Value?.ToString() ?? $"操作{opId}";
            if (MessageBox.Show(
                    $"将把数据库回退到【{desc}】执行之前的状态。\n该操作及其之后的所有操作都会被撤销，确认继续？",
                    "确认撤销", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                DBHelper.RestoreSnapshot(opId);
                Append($"[撤销] 已回退到操作 {opId}（{desc}）之前的状态。");
                MessageBox.Show("撤销成功：数据库已回到该操作之前。");
            }
            catch (SqlException ex) { Fail("撤销", ex); MessageBox.Show("撤销失败：" + ex.Message); }
            LoadOpLog();
        }

        void LoadOpLog()
        {
            try
            {
                DBHelper.EnsureOperationLog();   // 表若被 PITR 回退掉则补建，恢复后无需重启即自愈
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

        static object? ScalarMaster(string sql)
        {
            using var conn = new SqlConnection(MasterCs);
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            conn.Open();
            return cmd.ExecuteScalar();
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

        void Fail(string op, Exception ex) => Append($"[{op}] 失败：{ex.Message}");
        void Append(string line) => txtLog.AppendText(line + Environment.NewLine);
    }
}
