using System;
using System.Drawing;
using System.Windows.Forms;

namespace EnvKeySender
{
    public class MainForm : Form
    {
        private readonly AppSettings _settings;
        private KeyboardHook? _hook;
        private NotifyIcon _trayIcon;
        private ComboBox _keyCombo;
        private TextBox _envNameBox;
        private Button _saveBtn;

        public MainForm()
        {
            Text = "EnvKeySender";
            Size = new Size(360, 160);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            _settings = AppSettings.Load();

            InitializeComponents();
            ApplySettingsToHook();
        }

        private void InitializeComponents()
        {
            var lblKey = new Label() { Text = "監視するキー:", Location = new Point(12, 15), AutoSize = true };
            _keyCombo = new ComboBox() { Location = new Point(110, 12), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int i = 1; i <= 24; i++) _keyCombo.Items.Add(new KeyItem { Name = $"F{i}", Vk = 0x6F + i /* 0x70=F1 */ });
            _keyCombo.SelectedIndex = Math.Max(0, Math.Min(_keyCombo.Items.Count - 1, _settings.MonitoredKey - 0x6F - 1));

            var lblEnv = new Label() { Text = "環境変数名:", Location = new Point(12, 50), AutoSize = true };
            _envNameBox = new TextBox() { Location = new Point(110, 47), Width = 200, Text = _settings.EnvVarName };

            _saveBtn = new Button() { Text = "保存して適用", Location = new Point(110, 85), Width = 120 };
            _saveBtn.Click += SaveBtn_Click;

            Controls.Add(lblKey);
            Controls.Add(_keyCombo);
            Controls.Add(lblEnv);
            Controls.Add(_envNameBox);
            Controls.Add(_saveBtn);

            // Tray
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "EnvKeySender";
            _trayIcon.Icon = SystemIcons.Application;
            var menu = new ContextMenuStrip();
            var settingsItem = new ToolStripMenuItem("設定を開く");
            settingsItem.Click += (s, e) => { ShowWindow(); };
            var exitItem = new ToolStripMenuItem("終了");
            exitItem.Click += (s, e) => { ExitApplication(); };
            menu.Items.Add(settingsItem);
            menu.Items.Add(exitItem);
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += (s, e) => ShowWindow();

            Load += (s, e) => { HideWindowOnStart(); };
            FormClosing += MainForm_FormClosing;
        }

        private void SaveBtn_Click(object? sender, EventArgs e)
        {
            if (_keyCombo.SelectedItem is KeyItem ki)
            {
                _settings.MonitoredKey = ki.Vk;
            }
            _settings.EnvVarName = _envNameBox.Text?.Trim() ?? string.Empty;
            _settings.Save();
            ApplySettingsToHook();
        }

        private void ApplySettingsToHook()
        {
            _hook?.Dispose();
            _hook = new KeyboardHook(_settings.MonitoredKey);
            _hook.Triggered += Hook_Triggered;
        }

        private void Hook_Triggered(object? sender, EventArgs e)
        {
            try
            {
                var name = _settings.EnvVarName;
                if (string.IsNullOrWhiteSpace(name)) return;

                // Try Process -> User -> Machine
                string? val = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
                if (string.IsNullOrEmpty(val)) val = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
                if (string.IsNullOrEmpty(val)) val = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
                if (string.IsNullOrEmpty(val)) return;

                TextSender.SendText(val);
            }
            catch
            {
                // Do not log or expose secret values
            }
        }

        private void HideWindowOnStart()
        {
            // Start hidden in tray
            Hide();
            ShowInTaskbar = false;
        }

        private void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Hide instead of exit when user closes window
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void ExitApplication()
        {
            _trayIcon.Visible = false;
            _hook?.Dispose();
            _trayIcon.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hook?.Dispose();
                _trayIcon?.Dispose();
            }
            base.Dispose(disposing);
        }

        private class KeyItem
        {
            public string Name { get; set; } = string.Empty;
            public int Vk { get; set; }
            public override string ToString() => Name;
        }
    }
}
