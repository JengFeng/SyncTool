using SyncTool.Core;

namespace SyncTool.App;

public sealed class MainForm : Form
{
    private const int VisibleLogLimit = 1000;
    private readonly string _configPath;
    private readonly AppConfig _config;
    private readonly NotifyIcon _tray;
    private readonly ToolTip _toolTips = new() { AutoPopDelay = 12000, InitialDelay = 300, ReshowDelay = 100 };
    private readonly ListView _jobs = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft JhengHei UI", 10) };
    private readonly ListView _logs = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft JhengHei UI", 9) };
    private readonly Label _status = new() { AutoSize = true, Font = new Font("Microsoft JhengHei UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(36, 64, 98) };
    private readonly Label _mode = new() { AutoSize = true, Font = new Font("Microsoft JhengHei UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(0, 102, 153) };
    private readonly Label _detail = new() { AutoSize = false, Height = 24, Dock = DockStyle.Top, AutoEllipsis = true, ForeColor = Color.FromArgb(75, 75, 75) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 18, Margin = new Padding(0, 5, 0, 6) };
    private readonly System.Windows.Forms.Timer _scheduler = new() { Interval = 30000 };
    private SyncJobDefinition? _selected;
    private bool _running;
    private CancellationTokenSource? _currentRunCancellation;

    public MainForm(string configPath)
    {
        _configPath = configPath; _config = AppConfig.Load(configPath);
        foreach (var job in _config.Jobs) SyncStateLayout.MigrateLegacyState(AppContext.BaseDirectory, job.Id);
        if (_config.Jobs.Count > 0) _config.Save(_configPath); // persist legacy config migration safely; jobs state migration retains a timestamped legacy copy.
        Text = "SyncTool — 多組本機資料夾同步"; ClientSize = new Size(900, 600); MinimumSize = new Size(780, 500); StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(248, 249, 250);
        _tray = new NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application, Text = "SyncTool", Visible = true, ContextMenuStrip = TrayMenu() }; _tray.DoubleClick += (_, _) => ShowMain();
        FormClosing += (_, e) => { e.Cancel = true; Hide(); };
        _scheduler.Tick += async (_, _) => await RunDueJobAsync(); _scheduler.Start();
        BuildUi(); RefreshJobs();
        if (_jobs.Items.Count > 0) _jobs.Items[0].Selected = true;
        RenderSelected();
    }

    private void BuildUi()
    {
        _jobs.Columns.Add("名稱", 135); _jobs.Columns.Add("來源 A", 185); _jobs.Columns.Add("目標 B", 185); _jobs.Columns.Add("頻率", 80); _jobs.Columns.Add("模式", 105); _jobs.Columns.Add("狀態", 90); _jobs.Columns.Add("下次同步", 135);
        _jobs.SelectedIndexChanged += (_, _) => { _selected = _jobs.SelectedItems.Count == 0 ? null : (SyncJobDefinition?)_jobs.SelectedItems[0].Tag; RenderSelected(); };
        _jobs.MouseMove += (_, e) => ShowModeToolTip(e.Location);
        _logs.Columns.Add("時間", 145); _logs.Columns.Add("工作", 125); _logs.Columns.Add("類型", 75); _logs.Columns.Add("路徑／訊息（雙擊查看完整內容）", 510); _logs.Columns.Add("結果", 80);
        _logs.DoubleClick += (_, _) => ShowSelectedLogDetail();

        var title = new Label { Text = "同步工作", Dock = DockStyle.Top, Height = 27, Font = new Font("Microsoft JhengHei UI", 11, FontStyle.Bold), Padding = new Padding(0, 5, 0, 0), ForeColor = Color.FromArgb(36, 64, 98) };
        var topButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 6, 0, 4), BackColor = Color.FromArgb(248, 249, 250) };
        topButtons.Controls.Add(Button("同步模式說明", (_, _) => ShowModeGuide()));
        topButtons.Controls.Add(Button("新增工作", (_, _) => EditJob(null)));
        topButtons.Controls.Add(Button("編輯工作", (_, _) => { if (_selected is not null) EditJob(_selected); }));
        topButtons.Controls.Add(Button("封存工作", (_, _) => ArchiveSelected()));
        topButtons.Controls.Add(Button("立即同步選取工作", async (_, _) => { if (_selected is not null) await RunJobAsync(_selected, false); }));
        topButtons.Controls.Add(Button("取消本次同步", (_, _) => _currentRunCancellation?.Cancel()));
        topButtons.Controls.Add(Button("預覽備份清理", (_, _) => { if (_selected is not null) ShowBackupCleanupPreview(_selected); }));
        topButtons.Controls.Add(Button("處理待決衝突", (_, _) => { if (_selected is not null) ShowPendingConflicts(_selected); }));
        topButtons.Controls.Add(Button("同步全部已啟用工作", async (_, _) => await RunAllAsync()));

        var jobsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 5) };
        jobsPanel.Controls.Add(_jobs); jobsPanel.Controls.Add(topButtons); jobsPanel.Controls.Add(title);
        var logTitle = new Label { Text = "同步日誌", Dock = DockStyle.Top, Height = 29, Font = new Font("Microsoft JhengHei UI", 11, FontStyle.Bold), Padding = new Padding(0, 6, 0, 0), ForeColor = Color.FromArgb(36, 64, 98) };
        var logPanel = new Panel { Dock = DockStyle.Fill }; logPanel.Controls.Add(_logs); logPanel.Controls.Add(logTitle);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 310, Panel1MinSize = 190, Panel2MinSize = 150, IsSplitterFixed = false };
        split.Panel1.Controls.Add(jobsPanel); split.Panel2.Controls.Add(logPanel);

        var statusPanel = new Panel { Dock = DockStyle.Top, Height = 104, Padding = new Padding(14, 9, 14, 8), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        _status.Dock = DockStyle.Top; _status.Height = 25; _mode.Dock = DockStyle.Top; _mode.Height = 22;
        statusPanel.Controls.Add(_detail); statusPanel.Controls.Add(_progress); statusPanel.Controls.Add(_mode); statusPanel.Controls.Add(_status);
        Controls.Add(split); Controls.Add(statusPanel);
    }

    private ContextMenuStrip TrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("開啟同步工作", null, (_, _) => ShowMain());
        menu.Items.Add("檢視同步日誌", null, (_, _) => ShowLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("同步選取工作", null, async (_, _) => { if (_selected is not null) await RunJobAsync(_selected, false); });
        menu.Items.Add("暫停 / 恢復排程", null, (_, _) => _scheduler.Enabled = !_scheduler.Enabled);
        menu.Items.Add("退出程式", null, (_, _) => { _tray.Visible = false; Environment.Exit(0); }); return menu;
    }

    private static Button Button(string text, EventHandler handler) { var button = new Button { Text = text, AutoSize = true, BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; button.Click += handler; return button; }

    private void ShowModeToolTip(Point location)
    {
        var hit = _jobs.HitTest(location);
        var description = hit.Item?.Tag is SyncJobDefinition job && hit.SubItem == hit.Item.SubItems[4]
            ? $"{SyncModeNames.Display(job.DefaultMode)}\r\n{SyncModeNames.Description(job.DefaultMode)}"
            : string.Empty;
        _toolTips.SetToolTip(_jobs, description);
    }

    private void ShowModeGuide()
    {
        var lines = Enum.GetValues<SyncMode>().Select(mode => $"【{SyncModeNames.Display(mode)}】\r\n{SyncModeNames.Description(mode)}");
        using var dialog = new Form { Text = "同步模式說明", ClientSize = new Size(760, 430), MinimumSize = new Size(580, 330), StartPosition = FormStartPosition.CenterParent, Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application };
        var header = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(14, 9, 14, 0), Font = new Font("Microsoft JhengHei UI", 10, FontStyle.Bold), Text = "選擇工作時，請先確認此處的同步模式。單向模式首次執行或切換模式時，系統一定先要求預覽與確認。" };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, BackColor = Color.White, Font = new Font("Microsoft JhengHei UI", 10), Text = string.Join("\r\n\r\n", lines) };
        var close = new Button { Text = "關閉", Dock = DockStyle.Bottom, Height = 38, DialogResult = DialogResult.OK };
        dialog.Controls.Add(text); dialog.Controls.Add(header); dialog.Controls.Add(close); dialog.ShowDialog(this);
    }

    private void RefreshJobs()
    {
        _jobs.BeginUpdate();
        try
        {
            _jobs.Items.Clear();
            foreach (var job in _config.ActiveJobs.OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase))
            {
                var status = !job.IsEnabled ? "已暫停" : _running && ReferenceEquals(job, _selected) ? "同步中" : "已啟用";
                var item = new ListViewItem(job.Name) { Tag = job };
                item.SubItems.Add(job.SourcePath); item.SubItems.Add(job.TargetPath); item.SubItems.Add(job.SyncFrequency); item.SubItems.Add(SyncModeNames.Display(job.DefaultMode)); item.SubItems.Add(status); item.SubItems.Add(job.NextSyncAt?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "—");
                _jobs.Items.Add(item);
            }
        }
        finally { _jobs.EndUpdate(); }
    }

    private void RenderSelected()
    {
        if (_selected is null) { _status.Text = "請選擇同步工作"; _mode.Text = "同步模式：—"; _detail.Text = "新增工作後，先測試路徑，再啟用或立即同步。"; return; }
        _status.Text = _selected.IsEnabled ? $"已選擇：{_selected.Name}" : $"已選擇：{_selected.Name}（暫停）";
        _mode.Text = $"同步模式：{SyncModeNames.Display(_selected.DefaultMode)}";
        _detail.Text = $"A：{_selected.SourcePath}    B：{_selected.TargetPath}";
    }

    private void EditJob(SyncJobDefinition? existing)
    {
        var isNew = existing is null;
        var job = existing ?? new SyncJobDefinition { Name = "新同步工作", IsEnabled = false };
        var previousMode = job.DefaultMode;
        using var dialog = new Form { Text = isNew ? "新增同步工作" : "編輯同步工作", ClientSize = new Size(620, 405), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 3, RowCount = 10 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        var name = new TextBox { Text = job.Name, Dock = DockStyle.Fill }; var source = new TextBox { Text = job.SourcePath, Dock = DockStyle.Fill }; var target = new TextBox { Text = job.TargetPath, Dock = DockStyle.Fill };
        var frequency = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }; frequency.Items.AddRange(["Manual", "5 分鐘", "10 分鐘", "30 分鐘", "1 小時", "6 小時", "24 小時"]); frequency.SelectedItem = job.SyncFrequency;
        var mode = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FormattingEnabled = true }; mode.Items.AddRange([SyncMode.TwoWay, SyncMode.AToB, SyncMode.BToA, SyncMode.Preview]); mode.SelectedItem = job.DefaultMode; mode.Format += (_, e) => e.Value = SyncModeNames.Display((SyncMode)e.ListItem!);
        var enabled = new CheckBox { Text = "啟用此工作", Checked = job.IsEnabled }; var preserve = new CheckBox { Text = "單向模式保留目標額外檔案", Checked = job.PreserveTargetExtras }; var deletion = new CheckBox { Text = "刪除保護", Checked = job.DeleteProtectionEnabled }; var conflicts = new CheckBox { Text = "衝突保留雙副本", Checked = job.ConflictCopiesEnabled }; var retry = new CheckBox { Text = "鎖定檔重試", Checked = job.LockRetryEnabled };
        AddRow(panel, 0, "名稱：", name); AddRow(panel, 1, "來源 A：", source, async button => await PickFolderAsync(source, button)); AddRow(panel, 2, "目標 B：", target, async button => await PickFolderAsync(target, button)); AddRow(panel, 3, "頻率：", frequency); AddRow(panel, 4, "預設模式：", mode);
        panel.Controls.Add(enabled, 1, 5); panel.Controls.Add(preserve, 1, 6); panel.Controls.Add(deletion, 1, 7); panel.Controls.Add(conflicts, 1, 8); panel.Controls.Add(retry, 2, 8);
        var test = Button("測試路徑", (_, _) => TestPaths(source.Text, target.Text)); var save = Button("儲存", (_, _) => dialog.DialogResult = DialogResult.OK); var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill }; actions.Controls.Add(test); actions.Controls.Add(save); actions.Controls.Add(cancel); panel.Controls.Add(actions, 1, 9); dialog.Controls.Add(panel); dialog.AcceptButton = save; dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        job.Name = name.Text.Trim(); job.SourcePath = source.Text.Trim(); job.TargetPath = target.Text.Trim(); job.SyncFrequency = frequency.Text; job.DefaultMode = (SyncMode)mode.SelectedItem!; if (previousMode != job.DefaultMode) job.DirectionalModeApproved = false; job.IsEnabled = enabled.Checked; job.PreserveTargetExtras = preserve.Checked; job.DeleteProtectionEnabled = deletion.Checked; job.ConflictCopiesEnabled = conflicts.Checked; job.LockRetryEnabled = retry.Checked;
        try
        {
            SyncJobDefinition.ValidateSet(_config.ActiveJobs.Where(j => !ReferenceEquals(j, job)).Append(job));
            if (isNew) _config.Jobs.Add(job); SetNext(job); _config.Save(_configPath); RefreshJobs();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "設定無法儲存", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control, Func<Button, Task>? browse = null)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row); panel.Controls.Add(control, 1, row);
        if (browse is null) return;
        Button? button = null;
        button = Button("選擇", async (_, _) => await browse(button!));
        panel.Controls.Add(button, 2, row);
    }

    private async Task PickFolderAsync(TextBox box, Button button)
    {
        if (!button.Enabled) return;
        button.Enabled = false;
        var originalText = button.Text;
        button.Text = "開啟中…";
        try
        {
            var initialPath = Directory.Exists(box.Text) ? box.Text : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var selected = await StaTask.RunAsync(() =>
            {
                using var dialog = new FolderBrowserDialog { Description = "選擇同步資料夾", SelectedPath = initialPath, ShowNewFolderButton = true, AutoUpgradeEnabled = false };
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            });
            if (!string.IsNullOrWhiteSpace(selected)) box.Text = selected;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"無法開啟資料夾選取器：{ex.Message}", "SyncTool", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            button.Text = originalText;
            button.Enabled = true;
        }
    }

    private void TestPaths(string source, string target)
    {
        try
        {
            SyncJobDefinition.ValidateSet([new SyncJobDefinition { Id = Guid.NewGuid().ToString("N"), Name = "測試", SourcePath = source, TargetPath = target }]);
            if (!Directory.Exists(source) || !Directory.Exists(target)) throw new DirectoryNotFoundException("來源 A 或目標 B 不存在。");
            MessageBox.Show(this, "A／B 路徑可讀取，且未重複或巢狀。", "測試成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "測試失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private async Task RunDueJobAsync()
    {
        if (_running) return;
        var due = _config.ActiveJobs.Where(j => j.IsEnabled && j.NextSyncAt <= DateTimeOffset.Now && j.SyncFrequency != "Manual").OrderBy(j => j.NextSyncAt).ThenBy(j => j.Name).FirstOrDefault();
        if (due is not null) await RunJobAsync(due, (due.DefaultMode is SyncMode.AToB or SyncMode.BToA) && !due.DirectionalModeApproved);
    }

    private async Task RunAllAsync()
    {
        var jobs = _config.ActiveJobs.Where(j => j.IsEnabled).OrderBy(j => j.NextSyncAt).ThenBy(j => j.Name).ToList();
        if (jobs.Count == 0) { MessageBox.Show(this, "沒有已啟用的同步工作。", "SyncTool"); return; }
        if (MessageBox.Show(this, $"將依序同步 {jobs.Count} 組已啟用工作，是否繼續？", "同步全部", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        foreach (var job in jobs) await RunJobAsync(job, false);
    }

    private async Task RunJobAsync(SyncJobDefinition job, bool dryRun)
    {
        if (_running) return;
        _running = true; _selected = job; _currentRunCancellation = new CancellationTokenSource(); RefreshJobs();
        _status.Text = $"{job.Name}：掃描中…"; _progress.Style = ProgressBarStyle.Marquee; _detail.Text = "正在掃描來源 A 與目標 B…"; _tray.Text = "SyncTool — 掃描中";
        var progress = new Progress<SyncProgress>(p => RenderProgress(job, p));
        try
        {
            var result = await RunWithControlAsync(job, dryRun, progress, _currentRunCancellation.Token);
            job.LastSyncAt = DateTimeOffset.Now; SetNext(job); _config.Save(_configPath);
            RenderLogRows(job, result.Events); var logPath = Program.WriteLog(AppContext.BaseDirectory, job, result);
            _status.Text = result.Success ? $"{job.Name}：同步完成" : $"{job.Name}：同步失敗";
            _detail.Text = result.Success ? $"新增 {result.AddCount:N0}｜更新 {result.UpdateCount:N0}｜刪除 {result.DeleteCount:N0}｜衝突 {result.ConflictCount:N0}　日誌：{logPath}" : result.ErrorMessage;
            _tray.Text = result.Success ? "SyncTool — 同步完成" : "SyncTool — 同步失敗";
            _tray.ShowBalloonTip(2500, "SyncTool", result.Success ? $"{job.Name} 同步完成" : result.ErrorMessage, result.Success ? ToolTipIcon.Info : ToolTipIcon.Error);
        }
        catch (OperationCanceledException)
        {
            _status.Text = $"{job.Name}：本次同步已取消"; _detail.Text = "未更新同步基準；下次同步會以既有基準重新安全評估。"; AddLogRow(job.Name, "CANCELLED", "使用者取消本次同步", "已取消");
        }
        catch (Exception ex) { _status.Text = $"{job.Name}：同步失敗"; _detail.Text = ex.Message; AddLogRow(job.Name, "ERROR", ex.Message, "失敗"); }
        finally { _currentRunCancellation?.Dispose(); _currentRunCancellation = null; _running = false; RefreshJobs(); }
    }

    private async Task<SyncResult> RunWithControlAsync(SyncJobDefinition job, bool dryRun, IProgress<SyncProgress> progress, CancellationToken cancellationToken)
    {
        var mode = job.DefaultMode;
        if (dryRun || mode is SyncMode.TwoWay or SyncMode.Preview)
            return await Task.Run(async () => await new SyncEngine(job.ToOptions(AppContext.BaseDirectory, mode)).RunAsync(dryRun, progress, cancellationToken), cancellationToken);

        var preview = await Task.Run(async () => await new SyncEngine(job.ToOptions(AppContext.BaseDirectory, mode)).RunAsync(true, progress, cancellationToken), cancellationToken);
        if (!preview.Success || string.IsNullOrWhiteSpace(preview.PreviewId)) return preview;
        if (job.DirectionalModeApproved && !preview.RequiresApproval)
            return await Task.Run(async () => await new SyncEngine(job.ToOptions(AppContext.BaseDirectory, mode, preview.PreviewId)).RunAsync(false, progress, cancellationToken), cancellationToken);
        var riskNotice = preview.RequiresApproval ? $"\r\n⚠ 超過安全門檻：高風險操作 {preview.RiskOperationCount:N0} 項，影響 {preview.RiskBytes / 1024d / 1024d / 1024d:F2} GiB。" : "";
        var summary = $"模式：{SyncModeNames.Display(mode)}\r\n新增：{preview.AddCount:N0}\r\n更新：{preview.UpdateCount:N0}\r\n備份後移除：{preview.DeleteCount:N0}\r\n衝突：{preview.ConflictCount:N0}{riskNotice}\r\n\r\n預覽 ID：{preview.PreviewId}\r\n有效至：{preview.PreviewExpiresAt?.ToLocalTime():yyyy/MM/dd HH:mm}\r\n\r\n確認後才會寫入。是否套用？";
        if (MessageBox.Show(this, summary, "確認單向同步預覽", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return preview;
        job.DirectionalModeApproved = true;
        return await Task.Run(async () => await new SyncEngine(job.ToOptions(AppContext.BaseDirectory, mode, preview.PreviewId)).RunAsync(false, progress, cancellationToken), cancellationToken);
    }

    private void RenderProgress(SyncJobDefinition job, SyncProgress progress)
    {
        if (progress.Stage == SyncStage.Scanning) { _status.Text = $"{job.Name}：掃描中…"; _progress.Style = ProgressBarStyle.Marquee; _detail.Text = progress.Message; _tray.Text = "SyncTool — 掃描中"; return; }
        if (progress.Stage == SyncStage.Syncing)
        {
            _status.Text = $"{job.Name}：同步中…"; _progress.Style = ProgressBarStyle.Continuous; _progress.Maximum = Math.Max(1, progress.Total); _progress.Value = Math.Min(progress.Processed, _progress.Maximum);
            var percentage = progress.Total == 0 ? 0 : progress.Processed * 100d / progress.Total; _detail.Text = $"{progress.Processed:N0}/{progress.Total:N0}（{percentage:F1}%）　{progress.CurrentPath}"; _tray.Text = $"SyncTool — {progress.Processed:N0}/{progress.Total:N0}";
        }
    }

    private void RenderLogRows(SyncJobDefinition job, IReadOnlyList<string> events)
    {
        _logs.BeginUpdate();
        try
        {
            _logs.Items.Clear(); var visible = events.TakeLast(VisibleLogLimit).ToList();
            if (events.Count > visible.Count) AddLogRow(job.Name, "INFO", $"僅顯示最後 {VisibleLogLimit:N0} 筆，完整日誌位於 jobs\\{job.Id}\\logs", "已截斷");
            foreach (var line in visible)
            {
                var colon = line.IndexOf(':'); var type = colon > 0 ? line[..colon] : "INFO"; AddLogRow(job.Name, type, colon > 0 ? line[(colon + 1)..].Trim() : line, type is "WARNING" or "ERROR" ? "注意" : "完成");
            }
        }
        finally { _logs.EndUpdate(); }
    }

    private void ShowPendingConflicts(SyncJobDefinition job)
    {
        var store = new PendingConflictStore(Path.Combine(AppContext.BaseDirectory, "jobs", job.Id, "pending-conflicts.json"));
        using var dialog = new Form { Text = $"待決衝突 — {job.Name}", ClientSize = new Size(820, 460), MinimumSize = new Size(620, 320), StartPosition = FormStartPosition.CenterParent, Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application };
        var list = new ListBox { Dock = DockStyle.Fill, DisplayMember = nameof(PendingConflictItem.RelativePath) };
        void RefreshList() { list.DataSource = store.Load().ToList(); }
        void Resolve(PendingConflictResolution resolution)
        {
            if (list.SelectedItem is not PendingConflictItem item) return;
            if (resolution == PendingConflictResolution.Skip) { MessageBox.Show(dialog, "此項目會保留在待決清單，留待下次處理。", "待決衝突"); return; }
            var action = resolution switch { PendingConflictResolution.AToB => "以 A 覆蓋 B", PendingConflictResolution.BToA => "以 B 覆蓋 A", _ => "保留兩份衝突副本" };
            if (MessageBox.Show(dialog, $"{item.RelativePath}\r\n\r\n確定要{action}？", "確認衝突決策", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                PendingConflictResolver.Resolve(job.SourcePath, job.TargetPath, Path.Combine(AppContext.BaseDirectory, "jobs", job.Id, "backups"), store, item.RelativePath, resolution);
                AddLogRow(job.Name, "CONFLICT-RESOLVED", $"{action}: {item.RelativePath}", "完成"); RefreshList();
            }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "無法處理衝突", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        footer.Controls.Add(new Button { Text = "關閉", DialogResult = DialogResult.OK, AutoSize = true });
        var skip = Button("略過", (_, _) => Resolve(PendingConflictResolution.Skip)); var keep = Button("保留兩份", (_, _) => Resolve(PendingConflictResolution.KeepBoth)); var bToA = Button("採用 B → A", (_, _) => Resolve(PendingConflictResolution.BToA)); var aToB = Button("採用 A → B", (_, _) => Resolve(PendingConflictResolution.AToB));
        footer.Controls.Add(skip); footer.Controls.Add(keep); footer.Controls.Add(bToA); footer.Controls.Add(aToB); dialog.Controls.Add(list); dialog.Controls.Add(footer); RefreshList(); dialog.ShowDialog(this);
    }

    private void ShowBackupCleanupPreview(SyncJobDefinition job)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "jobs", job.Id, "backups");
        var preview = BackupCleanupPlanner.Preview(root, job.BackupRetentionDays, DateTimeOffset.UtcNow);
        var lines = preview.Items.Select(x => $"{x.LastWriteUtc.ToLocalTime():yyyy/MM/dd HH:mm:ss}  {x.Length:N0} bytes  {x.Path}");
        using var dialog = new Form { Text = $"備份清理預覽 — {job.Name}", ClientSize = new Size(900, 520), MinimumSize = new Size(650, 360), StartPosition = FormStartPosition.CenterParent, Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application };
        var header = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 8, 12, 0), Text = $"保留期限：{job.BackupRetentionDays} 天　到期檔案：{preview.Items.Count:N0}　總容量：{preview.TotalBytes / 1024d / 1024d:F2} MiB\r\n這是預覽，不會刪除任何檔案。" };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9), Text = string.Join(Environment.NewLine, lines), BackColor = Color.White };
        var close = new Button { Text = "關閉", Dock = DockStyle.Bottom, Height = 36, DialogResult = DialogResult.OK };
        var remove = new Button { Text = "確認清理這些到期備份", Dock = DockStyle.Bottom, Height = 36, Enabled = preview.Items.Count > 0 };
        remove.Click += (_, _) =>
        {
            if (MessageBox.Show(dialog, $"即將永久刪除預覽中的 {preview.Items.Count:N0} 個備份檔案（{preview.TotalBytes / 1024d / 1024d:F2} MiB）。\r\n此操作不可復原，確定要繼續？", "確認清理備份", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { var removed = BackupCleanupExecutor.Execute(preview, Path.Combine(AppContext.BaseDirectory, "jobs", job.Id, "logs", "backup-cleanup-audit.log")); AddLogRow(job.Name, "BACKUP-CLEANUP", $"已清理 {removed:N0} 個到期備份", "完成"); MessageBox.Show(dialog, $"已清理 {removed:N0} 個備份檔案。", "備份清理"); dialog.Close(); }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "無法清理備份", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        dialog.Controls.Add(text); dialog.Controls.Add(header); dialog.Controls.Add(remove); dialog.Controls.Add(close); dialog.ShowDialog(this);
    }

    private void ShowSelectedLogDetail()
    {
        if (_logs.SelectedItems.Count == 0) return;
        var item = _logs.SelectedItems[0];
        var timestamp = item.SubItems[0].Text; var job = item.SubItems[1].Text; var type = item.SubItems[2].Text; var message = item.SubItems[3].Text; var result = item.SubItems[4].Text;
        using var dialog = new Form { Text = "同步日誌完整訊息", ClientSize = new Size(860, 460), MinimumSize = new Size(620, 320), StartPosition = FormStartPosition.CenterParent, Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application };
        var header = new Label { Dock = DockStyle.Top, Height = 52, Padding = new Padding(14, 8, 14, 0), Font = new Font("Microsoft JhengHei UI", 10), Text = $"時間：{timestamp}    工作：{job}    類型：{type}    結果：{result}" };
        var messageBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = true, Font = new Font("Microsoft JhengHei UI", 10), Text = message, BackColor = Color.White };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var close = new Button { Text = "關閉", DialogResult = DialogResult.OK, AutoSize = true };
        var openFolder = new Button { Text = "開啟集中日誌資料夾", AutoSize = true };
        openFolder.Click += (_, _) =>
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "logs"); Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
        };
        var copy = new Button { Text = "複製完整訊息", AutoSize = true };
        copy.Click += (_, _) => { Clipboard.SetText(message); copy.Text = "已複製"; };
        footer.Controls.Add(close); footer.Controls.Add(openFolder); footer.Controls.Add(copy);
        dialog.Controls.Add(messageBox); dialog.Controls.Add(header); dialog.Controls.Add(footer); dialog.AcceptButton = close; dialog.ShowDialog(this);
    }

    private void AddLogRow(string job, string type, string detail, string result)
    {
        var item = new ListViewItem(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")); item.SubItems.Add(job); item.SubItems.Add(type); item.SubItems.Add(detail); item.SubItems.Add(result);
        if (type == "ERROR") item.ForeColor = Color.FromArgb(217, 48, 37); else if (type == "WARNING") item.ForeColor = Color.FromArgb(255, 140, 0); _logs.Items.Add(item);
    }

    private static TimeSpan? FrequencyInterval(string value) => value switch { "5 分鐘" => TimeSpan.FromMinutes(5), "10 分鐘" => TimeSpan.FromMinutes(10), "30 分鐘" => TimeSpan.FromMinutes(30), "1 小時" => TimeSpan.FromHours(1), "6 小時" => TimeSpan.FromHours(6), "24 小時" => TimeSpan.FromHours(24), _ => null };
    private static void SetNext(SyncJobDefinition job) { var interval = FrequencyInterval(job.SyncFrequency); job.NextSyncAt = interval is null ? null : DateTimeOffset.Now.Add(interval.Value); }

    private void ArchiveSelected()
    {
        if (_selected is null) return;
        if (MessageBox.Show(this, $"封存「{_selected.Name}」？它將停止排程，但保留狀態、日誌與備份。", "封存同步工作", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _selected.IsArchived = true; _selected.IsEnabled = false; _config.Save(_configPath); _selected = null; RefreshJobs(); RenderSelected();
    }

    private void ShowLogs()
    {
        ShowMain();
        _logs.Focus();
    }

    private void ShowMain() { Show(); WindowState = FormWindowState.Normal; Activate(); }
}
