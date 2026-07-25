using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.UI.Xaml;

namespace FluentTaskScheduler.Services
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Default";
        public bool IsOledMode { get; set; } = false;
        public bool IsMicaEnabled { get; set; } = true;
        public string Language { get; set; } = "en-US";
        public bool ConfirmDelete { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public bool EnableTrayIcon { get; set; } = true;
        public bool MinimizeToTray { get; set; } = false;
        public bool EnableLogging { get; set; } = true;
        public bool SeparateLogFiles { get; set; } = true;
        public bool RunOnStartup { get; set; } = false;
        public bool SmoothScrolling { get; set; } = true;
        public int WindowWidth { get; set; } = 1200;
        public int WindowHeight { get; set; } = 800;
        public string LastFolderPath { get; set; } = "\\";
        public string LastSeenVersion { get; set; } = "";
        public bool HasCompletedOnboarding { get; set; } = false;
        public bool EnableUpcomingReminders { get; set; } = true;
        public int ReminderLeadMinutes { get; set; } = 5;
        public bool ShowHiddenTasks { get; set; } = true;
        public List<string> SavedCategories { get; set; } = new() { "Work", "Personal", "Maintenance", "System" };
        public List<string> SavedTags { get; set; } = new() { "urgent", "sync", "database", "cleanup" };

        // ── Global snooze ────────────────────────────────────────────────────────
        public bool IsSnoozed { get; set; } = false;
        /// <summary>Absolute UTC end of the snooze window. Null when snoozing until reboot.</summary>
        public DateTime? SnoozeUntilUtc { get; set; } = null;
        /// <summary>When true the snooze lasts until the machine is restarted instead of a fixed time.</summary>
        public bool SnoozeUntilReboot { get; set; } = false;
        /// <summary>Boot timestamp captured when an "until reboot" snooze started, used to detect a restart.</summary>
        public string SnoozeBootStamp { get; set; } = "";
        /// <summary>When true, starting a snooze also disables scheduled triggers via the Task Scheduler API.</summary>
        public bool SnoozeSuspendsScheduledTasks { get; set; } = false;
        /// <summary>Tasks this app disabled when the current snooze started; re-enabled when it ends.</summary>
        public List<string> SnoozeDisabledTaskPaths { get; set; } = new();

        // ── Task chaining ────────────────────────────────────────────────────────
        /// <summary>Master switch for the pipeline watcher that starts downstream tasks on completion.</summary>
        public bool EnableTaskPipelines { get; set; } = true;
    }

    public static class SettingsService
    {
        private static AppSettings _settings = new();
        private static string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FluentTaskScheduler");
        private static string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

        static SettingsService()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // Fallback to defaults on error
                _settings = new AppSettings();
                LogService.Error($"Failed to load settings from '{SettingsPath}', reverting to defaults.", ex);
            }
        }

        private static void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }
                string json = JsonSerializer.Serialize(_settings);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to save settings to '{SettingsPath}'.", ex);
            }
        }

        public static ElementTheme Theme
        {
            get => Enum.TryParse<ElementTheme>(_settings.Theme, out var t) ? t : ElementTheme.Default;
            set
            {
                _settings.Theme = value.ToString();
                Save();
            }
        }

        public static bool IsOledMode
        {
            get => _settings.IsOledMode;
            set
            {
                _settings.IsOledMode = value;
                Save();
            }
        }

        public static bool IsMicaEnabled
        {
            get => _settings.IsMicaEnabled;
            set
            {
                _settings.IsMicaEnabled = value;
                Save();
            }
        }

        public static string Language
        {
            get => _settings.Language;
            set
            {
                _settings.Language = value;
                Save();
            }
        }

        public static bool ConfirmDelete
        {
            get => _settings.ConfirmDelete;
            set
            {
                _settings.ConfirmDelete = value;
                Save();
            }
        }

        public static bool ShowNotifications
        {
            get => _settings.ShowNotifications;
            set
            {
                _settings.ShowNotifications = value;
                Save();
            }
        }

        public static bool EnableTrayIcon
        {
            get => _settings.EnableTrayIcon;
            set
            {
                _settings.EnableTrayIcon = value;
                Save();
            }
        }

        public static bool MinimizeToTray
        {
            get => _settings.MinimizeToTray;
            set
            {
                _settings.MinimizeToTray = value;
                Save();
            }
        }

        public static bool EnableLogging
        {
            get => _settings.EnableLogging;
            set
            {
                _settings.EnableLogging = value;
                Save();
            }
        }
        
        public static bool SeparateLogFiles
        {
            get => _settings.SeparateLogFiles;
            set
            {
                _settings.SeparateLogFiles = value;
                Save();
            }
        }

        public static bool RunOnStartup
        {
            get => _settings.RunOnStartup;
            set
            {
                _settings.RunOnStartup = value;
                Save();
            }
        }

        public static bool SmoothScrolling
        {
            get => _settings.SmoothScrolling;
            set
            {
                _settings.SmoothScrolling = value;
                Save();
            }
        }

        public static int WindowWidth
        {
            get => _settings.WindowWidth;
            set { _settings.WindowWidth = value; Save(); }
        }

        public static int WindowHeight
        {
            get => _settings.WindowHeight;
            set { _settings.WindowHeight = value; Save(); }
        }

        public static string LastFolderPath
        {
            get => _settings.LastFolderPath;
            set { _settings.LastFolderPath = value; Save(); }
        }

        public static string LastSeenVersion
        {
            get => _settings.LastSeenVersion;
            set { _settings.LastSeenVersion = value; Save(); }
        }

        public static bool HasCompletedOnboarding
        {
            get => _settings.HasCompletedOnboarding;
            set { _settings.HasCompletedOnboarding = value; Save(); }
        }

        public static bool EnableUpcomingReminders
        {
            get => _settings.EnableUpcomingReminders;
            set { _settings.EnableUpcomingReminders = value; Save(); }
        }

        public static int ReminderLeadMinutes
        {
            get => _settings.ReminderLeadMinutes;
            set { _settings.ReminderLeadMinutes = value; Save(); }
        }

        public static List<string> SavedCategories
        {
            get => _settings.SavedCategories;
            set { _settings.SavedCategories = value; Save(); }
        }

        public static List<string> SavedTags
        {
            get => _settings.SavedTags;
            set { _settings.SavedTags = value; Save(); }
        }

        public static bool ShowHiddenTasks
        {
            get => _settings.ShowHiddenTasks;
            set { _settings.ShowHiddenTasks = value; Save(); }
        }

        // ── Global snooze ────────────────────────────────────────────────────────

        public static bool IsSnoozed
        {
            get => _settings.IsSnoozed;
            set { _settings.IsSnoozed = value; Save(); }
        }

        public static DateTime? SnoozeUntilUtc
        {
            get => _settings.SnoozeUntilUtc;
            set { _settings.SnoozeUntilUtc = value; Save(); }
        }

        public static bool SnoozeUntilReboot
        {
            get => _settings.SnoozeUntilReboot;
            set { _settings.SnoozeUntilReboot = value; Save(); }
        }

        public static string SnoozeBootStamp
        {
            get => _settings.SnoozeBootStamp;
            set { _settings.SnoozeBootStamp = value; Save(); }
        }

        public static bool SnoozeSuspendsScheduledTasks
        {
            get => _settings.SnoozeSuspendsScheduledTasks;
            set { _settings.SnoozeSuspendsScheduledTasks = value; Save(); }
        }

        public static List<string> SnoozeDisabledTaskPaths
        {
            get => _settings.SnoozeDisabledTaskPaths;
            set { _settings.SnoozeDisabledTaskPaths = value ?? new List<string>(); Save(); }
        }

        public static bool EnableTaskPipelines
        {
            get => _settings.EnableTaskPipelines;
            set { _settings.EnableTaskPipelines = value; Save(); }
        }

        /// <summary>
        /// Writes several snooze fields in one go so a single Save() hits disk instead of one per property.
        /// </summary>
        public static void SaveSnoozeState(bool isSnoozed, DateTime? untilUtc, bool untilReboot, string bootStamp, List<string> disabledPaths)
        {
            _settings.IsSnoozed = isSnoozed;
            _settings.SnoozeUntilUtc = untilUtc;
            _settings.SnoozeUntilReboot = untilReboot;
            _settings.SnoozeBootStamp = bootStamp ?? "";
            _settings.SnoozeDisabledTaskPaths = disabledPaths ?? new List<string>();
            Save();
        }

        public static void ExportSettings(string targetPath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(targetPath, json);
                LogService.Info($"Settings exported to {targetPath}");
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to export settings", ex);
                throw;
            }
        }

        public static void ImportSettings(string sourcePath)
        {
            try
            {
                string json = File.ReadAllText(sourcePath);
                var imported = JsonSerializer.Deserialize<AppSettings>(json);
                if (imported != null)
                {
                    _settings = imported;
                    Save();
                    LogService.Info($"Settings imported from {sourcePath}");
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to import settings", ex);
                throw;
            }
        }
    }
}
