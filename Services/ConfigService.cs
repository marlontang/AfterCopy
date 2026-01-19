using System;
using System.IO;
using System.Text.Json;
using AfterCopy.Models;

namespace AfterCopy.Services
{
    public class ConfigService
    {
        private const string ConfigFileName = "config.json";
        private readonly string _configPath;
        private AppConfig _currentConfig;

        public ConfigService()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            LoadConfig();
        }

        public AppConfig Current => _currentConfig;

        public void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    _currentConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                    _currentConfig = new AppConfig();
                }
            }
            else
            {
                _currentConfig = new AppConfig();
            }
        }

        public void SaveConfig(AppConfig config)
        {
            _currentConfig = config;
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { /* Handle error if needed */ }
        }
    }
}
