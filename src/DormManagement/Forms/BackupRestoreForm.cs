using System.IO;
using Microsoft.Data.SqlClient;

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
            Multiline = true, Dock = DockStyle.Fill, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9), BackColor = Color.White
        };

        public BackupRestoreForm()
        {
            Text = "备份与恢复"; Width = 740; Height = 480;
            StartPosition = FormStartPosition.CenterParent;

            var btnFull = new Button { Text = "完整备份", Width = 100 };
            var btnLog = new Button { Text = "日志备份", Width = 100 };
            var btnRestore = new Button { Text = "恢复到所选时间点", Width = 150 };

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8) };
            top.Controls.AddRange(new Control[] {
                btnFull, btnLog,
                new Label{ Text="时间点", AutoSize=true, Padding=new Padding(14,10,0,0) }, dtp, btnRestore });

            Controls.Add(txtLog);   // Fill（先加）
            Controls.Add(top);      // Top（后加）

            Load += (_, _) =>
            {
                Directory.CreateDirectory(BackupDir);
                Append($"备份目录：{BackupDir}");
                Append($"完整备份文件：{FullPath}");
                Append($"日志备份文件：{LogPath}");
                Append("流程：先『完整备份』→ 运行/产生数据 →『日志备份』→ 选时间点『恢复』。");
            };

            btnFull.Click += (_, _) => FullBackup();
            btnLog.Click += (_, _) => LogBackup();
            btnRestore.Click += (_, _) => Restore();
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

        void Restore()
        {
            if (!File.Exists(FullPath)) { MessageBox.Show("未找到完整备份文件，请先执行完整备份"); return; }
            if (!File.Exists(LogPath)) { MessageBox.Show("未找到日志备份文件，请先执行日志备份"); return; }

            var t = dtp.Value;
            if (MessageBox.Show(
                    $"将把 DormDB 恢复到 {t:yyyy-MM-dd HH:mm:ss}。\n" +
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
                Append($"[时间点恢复] 成功，已恢复到 {t:yyyy-MM-dd HH:mm:ss}");
                MessageBox.Show("恢复成功");
            }
            catch (SqlException ex)
            {
                Fail("时间点恢复", ex);
                BringOnline();   // 无论失败在哪一步，都把库拉回在线，避免卡在 RESTORING 导致谁都登不进
                MessageBox.Show("时间点恢复失败：" + ex.Message +
                    "\n\n已自动将数据库恢复到完整备份时刻并重新上线，可正常登录。\n" +
                    "提示：日志备份须在完整备份之后再做，且所选时间点要落在日志覆盖范围内。");
            }
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
