using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PMM_Windows_Helper
{
    public partial class frmMain : Form
    {
        // --- Softwares tab controls ---
        TabControl tabs;
        TabPage tabSoftwares, tabTweaks;

        TableLayoutPanel tlpSoft;          // layout chính cho tab Softwares
        Panel topBarSoft;                   // thanh công cụ phía trên
        TextBox txtSearch;
        CheckBox cbSelectAll;
        Button btnSelectNone, btnInstallSelected, btnRefreshVersions;
        ListView lvSoftwares;               // danh sách phần mềm
        StatusStrip statusSoft;
        ToolStripStatusLabel lblSoftCount;

        // --- Tweaks tab controls ---
        TableLayoutPanel tlpTweaks;
        FlowLayoutPanel flowTweaks;
        Button btnApplyTweaks;

        // Dữ liệu mẫu – bạn sẽ nối với winget sau
        class AppItem { public string Name; public string Version; public bool Selected; }
        List<AppItem> _apps;
        public frmMain()
        {
            InitializeComponent();
            Text = "WinSetup Helper";
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);               // giúp scale tốt hơn
            AutoScaleMode = AutoScaleMode.Font;

            BuildTabs();
            BuildSoftwaresTab();
            BuildTweaksTab();

            // Nạp dữ liệu mẫu
            LoadSampleApps();
            BindAppsToListView();

            // Sự kiện chung
            Resize += (s, e) => AutoResizeListViewColumns();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

        }

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
            // TableLayout: 3 hàng (TopBar - ListView - StatusStrip)
            tlpSoft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            tlpSoft.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // top bar cao theo nội dung
            tlpSoft.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // ListView chiếm hết
            tlpSoft.RowStyles.Add(new RowStyle(SizeType.AutoSize));    // status strip

            // --- Top bar ---
            topBarSoft = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };

            txtSearch = new TextBox { Width = 260, Anchor = AnchorStyles.Left | AnchorStyles.Top };
            cbSelectAll = new CheckBox { Text = "Select All", AutoSize = true, Left = 270, Top = 5 };
            btnSelectNone = new Button { Text = "Select None", AutoSize = true, Left = 360, Top = 0 };
            btnInstallSelected = new Button { Text = "Install Selected", AutoSize = true, Left = 470, Top = 0 };
            btnRefreshVersions = new Button { Text = "Refresh Versions", AutoSize = true, Left = 600, Top = 0 };

            topBarSoft.Controls.AddRange(new Control[] { txtSearch, cbSelectAll, btnSelectNone, btnInstallSelected, btnRefreshVersions });

            // --- ListView ---
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
            lvSoftwares.Columns.Add("Version", 120, HorizontalAlignment.Right);

            // --- Status strip ---
            statusSoft = new StatusStrip();
            lblSoftCount = new ToolStripStatusLabel("0 items");
            statusSoft.Items.Add(lblSoftCount);

            // Add vào layout
            tlpSoft.Controls.Add(topBarSoft, 0, 0);
            tlpSoft.Controls.Add(lvSoftwares, 0, 1);
            tlpSoft.Controls.Add(statusSoft, 0, 2);

            tabSoftwares.Controls.Add(tlpSoft);

            // --- Wire events ---
            cbSelectAll.CheckedChanged += (s, e) =>
            {
                lvSoftwares.BeginUpdate();
                foreach (ListViewItem it in lvSoftwares.Items)
                    it.Checked = cbSelectAll.Checked;
                lvSoftwares.EndUpdate();
                UpdateSoftCount();
            };

            btnSelectNone.Click += (s, e) =>
            {
                lvSoftwares.BeginUpdate();
                foreach (ListViewItem it in lvSoftwares.Items)
                    it.Checked = false;
                lvSoftwares.EndUpdate();
                cbSelectAll.CheckedChanged -= null; // no-op; tránh vòng lặp
                cbSelectAll.Checked = false;
                UpdateSoftCount();
            };

            btnInstallSelected.Click += (s, e) =>
            {
                // Chưa cài đặt tại đây – phần logic cài winget để sau.
                var selected = lvSoftwares.CheckedItems.Cast<ListViewItem>()
                                  .Select(i => i.SubItems[0].Text).ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Hãy chọn ít nhất một phần mềm.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                MessageBox.Show("Sẽ cài: " + string.Join(", ", selected), "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnRefreshVersions.Click += (s, e) =>
            {
                // Sau này nối với “winget show/upgrade --available”.
                // Tạm thời mô phỏng cập nhật.
                foreach (ListViewItem it in lvSoftwares.Items)
                {
                    // Demo: giữ nguyên Version cũ
                    // Thực tế: gọi phương thức cập nhật phiên bản ở đây.
                }
                MessageBox.Show("Versions refreshed (demo).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            txtSearch.TextChanged += (s, e) =>
            {
                var key = txtSearch.Text.Trim().ToLowerInvariant();
                lvSoftwares.BeginUpdate();
                lvSoftwares.Items.Clear();
                foreach (var a in _apps.Where(x => x.Name.ToLowerInvariant().Contains(key)))
                    lvSoftwares.Items.Add(MakeItem(a));
                lvSoftwares.EndUpdate();
                UpdateSoftCount();
                AutoResizeListViewColumns();
            };

            lvSoftwares.ItemChecked += (s, e) =>
            {
                // Giữ trạng thái Select All nếu tất cả đều được check
                cbSelectAll.CheckedChanged -= CbSelectAll_SyncGuard;
                cbSelectAll.Checked = lvSoftwares.Items.Count > 0 && lvSoftwares.CheckedItems.Count == lvSoftwares.Items.Count;
                cbSelectAll.CheckedChanged += CbSelectAll_SyncGuard;

                UpdateSoftCount();
            };
            cbSelectAll.CheckedChanged += CbSelectAll_SyncGuard;
        }

        void CbSelectAll_SyncGuard(object sender, EventArgs e)
        {
            // placeholder để detach/attach ở trên, tránh vòng lặp sự kiện
        }

        void BuildTweaksTab()
        {
            // TableLayout: 2 hàng (Flow tweaks - Button apply)
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

            // Ví dụ các tweak (chỉ UI; logic áp dụng để sau)
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

            btnApplyTweaks = new Button
            {
                Text = "Apply Tweaks",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(12)
            };
            var panelBottom = new Panel { Dock = DockStyle.Fill, Height = 48 };
            btnApplyTweaks.Left = 12; btnApplyTweaks.Top = 8;
            panelBottom.Controls.Add(btnApplyTweaks);

            tlpTweaks.Controls.Add(flowTweaks, 0, 0);
            tlpTweaks.Controls.Add(panelBottom, 0, 1);

            tabTweaks.Controls.Add(tlpTweaks);

            btnApplyTweaks.Click += (s, e) =>
            {
                // Chưa áp dụng ở đây – để phần logic sau.
                MessageBox.Show("Tweaks will be applied (demo).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        void LoadSampleApps()
        {
            _apps = new List<AppItem>
            {
                new AppItem { Name = "Google Chrome", Version = "—", Selected = true },
                new AppItem { Name = "Visual Studio Code", Version = "—", Selected = true },
                new AppItem { Name = "7-Zip", Version = "—", Selected = true },
                new AppItem { Name = "Git", Version = "—", Selected = false },
                new AppItem { Name = "VLC media player", Version = "—", Selected = false },
                new AppItem { Name = "Notepad++", Version = "—", Selected = false },
                new AppItem { Name = "Everything", Version = "—", Selected = false },
                new AppItem { Name = "PowerToys", Version = "—", Selected = false },
                new AppItem { Name = "SumatraPDF", Version = "—", Selected = false },
            };
        }

        void BindAppsToListView()
        {
            lvSoftwares.BeginUpdate();
            lvSoftwares.Items.Clear();
            foreach (var a in _apps)
            {
                var item = MakeItem(a);
                lvSoftwares.Items.Add(item);
            }
            lvSoftwares.EndUpdate();
            UpdateSoftCount();
            AutoResizeListViewColumns();
        }

        ListViewItem MakeItem(AppItem a)
        {
            var it = new ListViewItem(a.Name) { Checked = a.Selected, Tag = a };
            it.SubItems.Add(a.Version ?? "—");
            return it;
        }

        void UpdateSoftCount()
        {
            lblSoftCount.Text = $"{lvSoftwares.Items.Count} items | {lvSoftwares.CheckedItems.Count} selected";
        }

        void AutoResizeListViewColumns()
        {
            if (lvSoftwares.Columns.Count < 2) return;

            // Giữ cột Version ~120px, còn lại cho cột Name
            int versionWidth = 160;
            int total = lvSoftwares.ClientSize.Width;
            int nameWidth = Math.Max(200, total - versionWidth - SystemInformation.VerticalScrollBarWidth - 4);

            lvSoftwares.Columns[0].Width = nameWidth;
            lvSoftwares.Columns[1].Width = versionWidth;
        }
    }
}
