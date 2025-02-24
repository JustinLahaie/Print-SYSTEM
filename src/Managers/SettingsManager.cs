using System;
using System.IO;
using System.Text.Json;
using PrintSystem.Models;
using System.Windows.Forms;

namespace PrintSystem.Managers
{
    public static class SettingsManager
    {
        private static readonly string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private static Settings currentSettings;
        private static readonly object lockObject = new object();

        static SettingsManager()
        {
            LoadSettings();
        }

        private static void LoadSettings()
        {
            lock (lockObject)
            {
                if (File.Exists(settingsFilePath))
                {
                    try
                    {
                        string jsonData = File.ReadAllText(settingsFilePath);
                        currentSettings = JsonSerializer.Deserialize<Settings>(jsonData) ?? new Settings();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading settings: {ex.Message}", "Error");
                        currentSettings = new Settings();
                    }
                }
                else
                {
                    currentSettings = new Settings();
                    SaveSettings(currentSettings);
                }
            }
        }

        public static Settings GetSettings()
        {
            if (currentSettings == null)
            {
                LoadSettings();
            }
            return currentSettings;
        }

        public static void SaveSettings(Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            try
            {
                string jsonData = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFilePath, jsonData);
                currentSettings = settings;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error");
            }
        }
    }
} 