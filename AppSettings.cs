using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace EnvKeySender
{
    public class AppSettings
    {
        public bool CtrlModifier { get; set; } = true;
        public bool ShiftModifier { get; set; } = true;
        public bool AltModifier { get; set; } = true;
        public int MonitoredKey { get; set; } = (int)Keys.G;
        public string EnvVarName { get; set; } = "MY_SHORT_PASSWORD";

        private static string GetSettingsPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnvKeySender");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        public void Save()
        {
            var path = GetSettingsPath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            using var fs = File.Create(path);
            JsonSerializer.Serialize(fs, this, options);
        }

        public static AppSettings Load()
        {
            var path = GetSettingsPath();
            if (!File.Exists(path)) return new AppSettings();
            try
            {
                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s == null) return new AppSettings();

                // Backward compatibility for older settings files that only stored MonitoredKey.
                if (s.MonitoredKey == 124 && !s.CtrlModifier && !s.ShiftModifier && !s.AltModifier)
                {
                    s.CtrlModifier = true;
                    s.ShiftModifier = true;
                    s.AltModifier = true;
                    s.MonitoredKey = (int)Keys.G;
                }

                return s;
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
