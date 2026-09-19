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
            Icon = SystemIcons.Information;
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
                new KeyItem { Name = "G", Vk = (int)Keys.G, Ctrl = false, Shift = false, Alt = false },
                new KeyItem { Name = "Ctrl+Shift+Alt+G", Vk = (int)Keys.G, Ctrl = true, Shift = true, Alt = true },
                new KeyItem { Name = "F13", Vk = (int)Keys.F13, Ctrl = false, Shift = false, Alt = false },
                new KeyItem { Name = "F14", Vk = (int)Keys.F14, Ctrl = false, Shift = false, Alt = false }
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
            _trayIcon.Icon = SystemIcons.Information;
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

            var envStatus = ResolveEnvironmentVariable(_settings.EnvVarName, "on-save");
            ErrorLogger.Write($"Settings saved: key={_settings.MonitoredKey}, ctrl={_settings.CtrlModifier}, shift={_settings.ShiftModifier}, alt={_settings.AltModifier}, env={_settings.EnvVarName}, resolved={envStatus.Found}, source={envStatus.Source ?? "none"}, length={envStatus.Value?.Length ?? 0}");
            Hide();
            ShowInTaskbar = false;
        }

        private void ApplySettingsToHook()
        {
            _hook?.Dispose();
            _hook = new KeyboardHook(_settings.MonitoredKey, _settings.CtrlModifier, _settings.ShiftModifier, _settings.AltModifier);
            _hook.Triggered += Hook_Triggered;
        }

        private (bool Found, string? Value, string? Source) ResolveEnvironmentVariable(string name, string context)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorLogger.Write($"Environment variable lookup skipped: name is empty, context={context}");
                return (false, null, null);
            }

            string? value = null;
            string? source = null;

            foreach (var target in new[]
            {
                new { Name = "Process", Target = EnvironmentVariableTarget.Process },
                new { Name = "User", Target = EnvironmentVariableTarget.User },
                new { Name = "Machine", Target = EnvironmentVariableTarget.Machine }
            })
            {
                var candidate = Environment.GetEnvironmentVariable(name, target.Target);
                var found = !string.IsNullOrEmpty(candidate);
                ErrorLogger.Write($"Environment variable lookup: name={name}, context={context}, source={target.Name}, found={found}, length={candidate?.Length ?? 0}");

                if (found)
                {
                    value = candidate;
                    source = target.Name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(value))
            {
                ErrorLogger.Write($"Environment variable not found: name={name}, context={context}");
                return (false, null, null);
            }

            ErrorLogger.Write($"Environment variable resolved: name={name}, context={context}, source={source}, length={value.Length}");
            return (true, value, source);
        }

        private void Hook_Triggered(object? sender, EventArgs e)
        {
            try
            {
                var name = _settings.EnvVarName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    ErrorLogger.Write("Trigger ignored: environment variable name is empty.");
                    return;
                }

                var envStatus = ResolveEnvironmentVariable(name, "on-trigger");
                if (!envStatus.Found || string.IsNullOrEmpty(envStatus.Value))
                {
                    ErrorLogger.Write($"Trigger aborted: environment variable not available, name={name}");
                    return;
                }

                ErrorLogger.Write($"Trigger sending environment variable: name={name}, source={envStatus.Source ?? "unknown"}, length={envStatus.Value.Length}");
                TextSender.SendText(envStatus.Value);
            }
            catch (Exception ex)
            {
                ErrorLogger.WriteException(ex, $"Failed to send environment variable value for '{_settings.EnvVarName}'");
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
