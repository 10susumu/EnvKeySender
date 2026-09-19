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
            _keyCombo = new ComboBox() { Location = new Point(110, 12), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

            var keyOptions = new[]
            {
                new KeyItem { Name = "Ctrl+Shift+Alt+G", Vk = (int)Keys.G, Ctrl = true, Shift = true, Alt = true },
                new KeyItem { Name = "F13", Vk = (int)Keys.F13, Ctrl = false, Shift = false, Alt = false },
                new KeyItem { Name = "F14", Vk = (int)Keys.F14, Ctrl = false, Shift = false, Alt = false },
                new KeyItem { Name = "F15", Vk = (int)Keys.F15, Ctrl = false, Shift = false, Alt = false }
            };

            foreach (var option in keyOptions)
            {
                _keyCombo.Items.Add(option);
            }

            var selectedIndex = 0;
            for (int i = 0; i < _keyCombo.Items.Count; i++)
            {
                if (_keyCombo.Items[i] is KeyItem item &&
                    item.Vk == _settings.MonitoredKey &&
                    item.Ctrl == _settings.CtrlModifier &&
                    item.Shift == _settings.ShiftModifier &&
                    item.Alt == _settings.AltModifier)
                {
                    selectedIndex = i;
                    break;
                }
            }
            _keyCombo.SelectedIndex = selectedIndex;

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
                _settings.CtrlModifier = ki.Ctrl;
                _settings.ShiftModifier = ki.Shift;
                _settings.AltModifier = ki.Alt;
            }
            _settings.EnvVarName = _envNameBox.Text?.Trim() ?? string.Empty;
            _settings.Save();
            ApplySettingsToHook();
        }

        private void ApplySettingsToHook()
        {
            _hook?.Dispose();
            _hook = new KeyboardHook(_settings.MonitoredKey, _settings.CtrlModifier, _settings.ShiftModifier, _settings.AltModifier);
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
            public bool Ctrl { get; set; }
            public bool Shift { get; set; }
            public bool Alt { get; set; }
            public override string ToString() => Name;
        }
    }
}
