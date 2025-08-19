using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PMM_Windows_Helper
{
    public partial class frmMain : Form
    {
        // ====== Config của bạn ======
        private const string CATALOG_URL = "https://raw.githubusercontent.com/doanphong1995/PMM-Windows-Helper/refs/heads/master/PMM-Windows-Helper/catalog.json";

        // ====== UI ======
        TabControl tabs;
        TabPage tabSoftwares, tabTweaks;

        TableLayoutPanel tlpSoft;
        Panel topBarSoft;
        TextBox txtSearch;
        CheckBox cbSelectAll;
        Button btnSelectNone, btnInstallSelected, btnRefreshVersions, btnReloadCatalog;
        ListView lvSoftwares;
        StatusStrip statusSoft;
        ToolStripStatusLabel lblSoftCount;

        TableLayoutPanel tlpTweaks;
        FlowLayoutPanel flowTweaks;
        Button btnApplyTweaks;

        // ====== Data/Services ======
        private readonly GitCatalogService _catalogService = new GitCatalogService(CATALOG_URL);
        private readonly WingetInstallerService _winget = new WingetInstallerService();
        private List<AppItem> _apps = new List<AppItem>();           // toàn bộ từ catalog
        private List<AppItem> _viewApps = new List<AppItem>();        // danh sách đang hiển thị (sau khi filter)
        private readonly Dictionary<string, ListViewItem> _rowById = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _ctsVersions;

        public frmMain()
        {
            Text = "WinSetup Helper";
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Font;

            BuildTabs();
            BuildSoftwaresTab();
            BuildTweaksTab();

            Resize += (s, e) => AutoResizeListViewColumns();
            Shown += async (s, e) => await LoadCatalogAndRenderAsync(); // tự load catalog khi mở form
        }

        // ========== UI Builders ==========
        void BuildTabs()
        {
            tabs = new TabControl { Dock = DockStyle.Fill, HotTrack = true };
            tabSoftwares = new TabPage("Softwares");
            tabTweaks = new TabPage("Tweaks");
            tabs.TabPages.Add(tabSoftwares);
            tabs.TabPages.Add(tabTweaks);
            Controls.Add(tabs);
        }

        void BuildSoftwaresTab()
        {
            tlpSoft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            tlpSoft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpSoft.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlpSoft.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Top bar
            topBarSoft = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
            txtSearch = new TextBox { Width = 260, Anchor = AnchorStyles.Left | AnchorStyles.Top };

            cbSelectAll = new CheckBox { Text = "Select All", AutoSize = true, Left = 270, Top = 5 };
            btnSelectNone = new Button { Text = "Select None", AutoSize = true, Left = 360, Top = 0 };
            btnInstallSelected = new Button { Text = "Install Selected", AutoSize = true, Left = 470, Top = 0 };
            btnRefreshVersions = new Button { Text = "Refresh Versions", AutoSize = true, Left = 600, Top = 0 };
            btnReloadCatalog = new Button { Text = "Reload Catalog", AutoSize = true, Left = 740, Top = 0 };

            topBarSoft.Controls.AddRange(new Control[] { txtSearch, cbSelectAll, btnSelectNone, btnInstallSelected, btnRefreshVersions, btnReloadCatalog });

            // ListView
            lvSoftwares = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                HideSelection = false,
                GridLines = true
            };
            lvSoftwares.Columns.Add("Software", 500, HorizontalAlignment.Left);
            lvSoftwares.Columns.Add("Version", 160, HorizontalAlignment.Right);

            // Status
            statusSoft = new StatusStrip();
            lblSoftCount = new ToolStripStatusLabel("0 items");
            statusSoft.Items.Add(lblSoftCount);

            tlpSoft.Controls.Add(topBarSoft, 0, 0);
            tlpSoft.Controls.Add(lvSoftwares, 0, 1);
            tlpSoft.Controls.Add(statusSoft, 0, 2);
            tabSoftwares.Controls.Add(tlpSoft);

            // Events
            cbSelectAll.CheckedChanged += (s, e) =>
            {
                lvSoftwares.BeginUpdate();
                foreach (ListViewItem it in lvSoftwares.Items) it.Checked = cbSelectAll.Checked;
                lvSoftwares.EndUpdate();
                UpdateSoftCount();
            };

            btnSelectNone.Click += (s, e) =>
            {
                lvSoftwares.BeginUpdate();
                foreach (ListViewItem it in lvSoftwares.Items) it.Checked = false;
                lvSoftwares.EndUpdate();
                cbSelectAll.Checked = false;
                UpdateSoftCount();
            };

            btnInstallSelected.Click += async (s, e) =>
            {
                var ids = lvSoftwares.CheckedItems.Cast<ListViewItem>()
                                 .Select(i => ((AppItem)i.Tag).wingetId)
                                 .Where(id => !string.IsNullOrWhiteSpace(id))
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToList();
                if (ids.Count == 0)
                {
                    MessageBox.Show("Hãy chọn ít nhất một phần mềm.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Ở đây bạn sẽ nối với _winget.InstallSelectedAsync(...) (đã có trong service)
                MessageBox.Show("Preview cài đặt:\n" + string.Join("\n", ids), "Install", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // TODO: gọi _winget.InstallSelectedAsync(ids, progress, token) + cập nhật UI/log
            };

            btnRefreshVersions.Click += async (s, e) => await RefreshVersionsAsync();

            btnReloadCatalog.Click += async (s, e) => await LoadCatalogAndRenderAsync();

            txtSearch.TextChanged += (s, e) =>
            {
                ApplySearch(txtSearch.Text);
            };

            lvSoftwares.ItemChecked += (s, e) =>
            {
                cbSelectAll.CheckedChanged -= CbSelectAll_SyncGuard;
                cbSelectAll.Checked = lvSoftwares.Items.Count > 0 && lvSoftwares.CheckedItems.Count == lvSoftwares.Items.Count;
                cbSelectAll.CheckedChanged += CbSelectAll_SyncGuard;
                UpdateSoftCount();
            };
            cbSelectAll.CheckedChanged += CbSelectAll_SyncGuard;
        }
        void CbSelectAll_SyncGuard(object sender, EventArgs e) { /* guard */ }

        void BuildTweaksTab()
        {
            tlpTweaks = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            tlpTweaks.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlpTweaks.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            flowTweaks = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12),
                WrapContents = true
            };
            flowTweaks.Controls.AddRange(new Control[]
            {
                new CheckBox { Text = "Show file extensions", AutoSize = true, Margin = new Padding(8)},
                new CheckBox { Text = "Show hidden files", AutoSize = true, Margin = new Padding(8)},
                new CheckBox { Text = "Pin 'This PC' on Desktop", AutoSize = true, Margin = new Padding(8)},
                new CheckBox { Text = "Disable OneDrive auto-start", AutoSize = true, Margin = new Padding(8)},
                new CheckBox { Text = "Power plan: High performance", AutoSize = true, Margin = new Padding(8)},
                new CheckBox { Text = "Disable Start/Explorer suggestions", AutoSize = true, Margin = new Padding(8)},
                new CheckBox { Text = "Enable .NET Framework 3.5", AutoSize = true, Margin = new Padding(8)},
            });

            btnApplyTweaks = new Button { Text = "Apply Tweaks", AutoSize = true, Margin = new Padding(12) };
            var panelBottom = new Panel { Dock = DockStyle.Fill, Height = 48 };
            btnApplyTweaks.Left = 12; btnApplyTweaks.Top = 8;
            panelBottom.Controls.Add(btnApplyTweaks);

            tlpTweaks.Controls.Add(flowTweaks, 0, 0);
            tlpTweaks.Controls.Add(panelBottom, 0, 1);

            tabTweaks.Controls.Add(tlpTweaks);

            btnApplyTweaks.Click += (s, e) =>
            {
                MessageBox.Show("Tweaks will be applied (demo).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        // ========== Data binding & helpers ==========
        private async Task LoadCatalogAndRenderAsync()
        {
            ToggleSoftControls(false);
            try
            {
                _rowById.Clear();
                lvSoftwares.Items.Clear();
                UpdateSoftCount();

                var catalog = await _catalogService.GetCatalogAsync();
                _apps = (catalog.apps ?? new List<AppItem>())
                        .Where(a => !string.IsNullOrWhiteSpace(a.id) && !string.IsNullOrWhiteSpace(a.name))
                        .ToList();

                // Render tất cả, sau đó refresh version
                _viewApps = _apps.ToList();
                BindAppsToListView(_viewApps);

                await RefreshVersionsAsync(); // lấy phiên bản mới nhất rồi fill cột Version
            }
            catch (Exception ex)
            {
                MessageBox.Show("Load catalog failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleSoftControls(true);
            }
        }

        private void BindAppsToListView(List<AppItem> apps)
        {
            lvSoftwares.BeginUpdate();
            lvSoftwares.Items.Clear();
            _rowById.Clear();

            foreach (var a in apps)
            {
                var it = new ListViewItem(a.name)
                {
                    Checked = a.defaultSelected,
                    Tag = a
                };
                it.SubItems.Add("…"); // chờ cập nhật phiên bản
                lvSoftwares.Items.Add(it);
                _rowById[a.id] = it;
            }

            lvSoftwares.EndUpdate();
            UpdateSoftCount();
            AutoResizeListViewColumns();
        }

        private void ApplySearch(string keyword)
        {
            var key = (keyword ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                _viewApps = _apps.ToList();
            }
            else
            {
                _viewApps = _apps.Where(x =>
                    (x.name ?? "").ToLowerInvariant().Contains(key) ||
                    (x.group ?? "").ToLowerInvariant().Contains(key) ||
                    (x.id ?? "").ToLowerInvariant().Contains(key)
                ).ToList();
            }
            BindAppsToListView(_viewApps);
        }

        private void UpdateSoftCount()
        {
            lblSoftCount.Text = $"{lvSoftwares.Items.Count} items | {lvSoftwares.CheckedItems.Count} selected";
        }

        private void AutoResizeListViewColumns()
        {
            if (lvSoftwares.Columns.Count < 2) return;
            int versionWidth = 160;
            int total = lvSoftwares.ClientSize.Width;
            int nameWidth = Math.Max(200, total - versionWidth - SystemInformation.VerticalScrollBarWidth - 4);
            lvSoftwares.Columns[0].Width = nameWidth;
            lvSoftwares.Columns[1].Width = versionWidth;
        }

        private void ToggleSoftControls(bool enabled)
        {
            topBarSoft.Enabled = enabled;
            lvSoftwares.Enabled = enabled;
        }

        // ========== Version refresh (winget) ==========
        private async Task RefreshVersionsAsync()
        {
            // Hủy lần refresh đang chạy (nếu có)
            _ctsVersions?.Cancel();
            _ctsVersions = new CancellationTokenSource();
            var ct = _ctsVersions.Token;

            ToggleSoftControls(false);
            try
            {
                // Giới hạn song song 3 luồng (đủ nhẹ để UI mượt)
                using (var sem = new System.Threading.SemaphoreSlim(3))
                {
                    var tasks = _viewApps
                        .Where(a => !string.IsNullOrWhiteSpace(a.wingetId))
                        .Select(async a =>
                        {
                            await sem.WaitAsync(ct);
                            try
                            {
                                var ver = await _winget.GetLatestVersionAsync(a.wingetId, ct);
                                // cập nhật UI
                                if (_rowById.TryGetValue(a.id, out var row))
                                {
                                    // WinForms là single-threaded; đảm bảo update trên UI thread
                                    if (InvokeRequired)
                                        BeginInvoke(new Action(() => row.SubItems[1].Text = ver));
                                    else
                                        row.SubItems[1].Text = ver;
                                }
                            }
                            finally { sem.Release(); }
                        }).ToList();

                    await Task.WhenAll(tasks);
                }
            }
            catch (OperationCanceledException)
            {
                // user/thao tác khác hủy – bỏ qua
            }
            catch (Exception ex)
            {
                MessageBox.Show("Refresh versions failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleSoftControls(true);
            }
        }
    }
}
