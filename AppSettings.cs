using System;
using System.IO;
using System.Text.Json;

namespace EnvKeySender
{
    public class AppSettings
    {
        public int MonitoredKey { get; set; } = 124; // F13 default (VK_F13 = 0x7C = 124)
        public string EnvVarName { get; set; } = "MYKEY";

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
                return s ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
