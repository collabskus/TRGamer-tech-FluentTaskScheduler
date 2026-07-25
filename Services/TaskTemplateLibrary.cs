using System;
using System.Collections.Generic;
using System.Linq;
using FluentTaskScheduler.Models;

namespace FluentTaskScheduler.Services
{
    /// <summary>
    /// Built-in, production-ready task presets grouped by purpose. Everything here is data only —
    /// nothing is registered with Windows until the user deploys a template and saves the editor.
    /// </summary>
    public static class TaskTemplateLibrary
    {
        public const string GroupMaintenance = "System Maintenance";
        public const string GroupDeveloper = "Developer Tools";
        public const string GroupPower = "Power & Utility";

        private static string L(string key, string fallback) => LocalizationService.GetString(key, fallback);

        /// <summary>Localized display name for a group id.</summary>
        public static string GetGroupDisplayName(string group) => group switch
        {
            GroupMaintenance => L("Templates.Group.Maintenance", "System Maintenance"),
            GroupDeveloper => L("Templates.Group.Developer", "Developer Tools"),
            GroupPower => L("Templates.Group.Power", "Power & Utility"),
            _ => group
        };

        public static IReadOnlyList<string> Groups => new[] { GroupMaintenance, GroupDeveloper, GroupPower };

        /// <summary>All built-in templates, rebuilt on each call so language changes are picked up.</summary>
        public static List<TaskTemplate> GetAll() => new()
        {
            // ── System Maintenance ──────────────────────────────────────────────
            new TaskTemplate
            {
                Id = "maint.temp-cleanup",
                Group = GroupMaintenance,
                Glyph = "\uE74D",
                Name = L("Templates.TempCleanup.Name", "Nightly Temp File Cleanup"),
                Description = L("Templates.TempCleanup.Desc",
                    "Deletes files older than 7 days from the user and Windows temp folders. Files still in use are skipped."),
                Command = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " +
                            "\"$cutoff=(Get-Date).AddDays(-7); " +
                            "@($env:TEMP,\"$env:SystemRoot\\Temp\") | ForEach-Object { " +
                            "Get-ChildItem -Path $_ -Recurse -Force -ErrorAction SilentlyContinue | " +
                            "Where-Object { -not $_.PSIsContainer -and $_.LastWriteTime -lt $cutoff } | " +
                            "Remove-Item -Force -ErrorAction SilentlyContinue } \"",
                TaskCategory = "Maintenance",
                Tags = new List<string> { "cleanup", "disk" },
                TriggerType = "Daily",
                TriggerHour = 3,
                RunIfMissed = true,
                Note = L("Templates.TempCleanup.Note", "Deletes files permanently — review the age threshold before deploying.")
            },
            new TaskTemplate
            {
                Id = "maint.update-health",
                Group = GroupMaintenance,
                Glyph = "\uE895",
                Name = L("Templates.UpdateHealth.Name", "Windows Update Health Check"),
                Description = L("Templates.UpdateHealth.Desc",
                    "Reports pending Windows updates and the last successful install date to a log file in your Documents folder."),
                Command = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " +
                            "\"$out=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'WindowsUpdateHealth.log'; " +
                            "$s=New-Object -ComObject Microsoft.Update.Session; " +
                            "$r=$s.CreateUpdateSearcher().Search('IsInstalled=0 and IsHidden=0'); " +
                            "\\\"$(Get-Date -Format s) Pending updates: $($r.Updates.Count)\\\" | Out-File -Append $out\"",
                RunAsAdmin = true,
                TaskCategory = "Maintenance",
                Tags = new List<string> { "updates", "health" },
                TriggerType = "Weekly",
                TriggerHour = 9,
                WeeklyDays = new List<string> { "Monday" },
                Note = L("Templates.UpdateHealth.Note", "Requires administrator privileges to query the update agent.")
            },
            new TaskTemplate
            {
                Id = "maint.defrag-trim",
                Group = GroupMaintenance,
                Glyph = "\uEDA2",
                Name = L("Templates.Defrag.Name", "Disk Optimize (Defrag / TRIM)"),
                Description = L("Templates.Defrag.Desc",
                    "Runs the built-in Windows optimizer on the system drive. SSDs receive a TRIM/retrim, HDDs are defragmented."),
                Command = "defrag.exe",
                Arguments = "C: /O /H",
                RunAsAdmin = true,
                OnlyIfIdle = true,
                OnlyIfAC = true,
                TaskCategory = "Maintenance",
                Tags = new List<string> { "disk", "performance" },
                TriggerType = "Weekly",
                TriggerHour = 2,
                WeeklyDays = new List<string> { "Sunday" },
                Note = L("Templates.Defrag.Note", "Requires administrator privileges. Runs only when the PC is idle and on AC power.")
            },

            // ── Developer Tools ─────────────────────────────────────────────────
            new TaskTemplate
            {
                Id = "dev.git-fetch",
                Group = GroupDeveloper,
                Glyph = "\uE895",
                Name = L("Templates.GitFetch.Name", "Git Repository Fetch / Backup"),
                Description = L("Templates.GitFetch.Desc",
                    "Fetches all remotes with pruning for every Git repository found under a folder you choose. Edit the path before saving."),
                Command = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " +
                            "\"$root='C:\\Repos'; " +
                            "Get-ChildItem -Path $root -Directory -Filter .git -Recurse -Depth 2 -Force | ForEach-Object { " +
                            "git -C $_.Parent.FullName fetch --all --prune }\"",
                TaskCategory = "Work",
                Tags = new List<string> { "git", "sync" },
                TriggerType = "Daily",
                TriggerHour = 8,
                RunIfMissed = true,
                Note = L("Templates.GitFetch.Note", "Change 'C:\\Repos' to your own repository root, and make sure git is on PATH.")
            },
            new TaskTemplate
            {
                Id = "dev.docker-prune",
                Group = GroupDeveloper,
                Glyph = "\uE7B8",
                Name = L("Templates.DockerPrune.Name", "Docker System Prune"),
                Description = L("Templates.DockerPrune.Desc",
                    "Removes stopped containers, unused networks, dangling images and build cache to reclaim disk space."),
                Command = "docker.exe",
                Arguments = "system prune -f",
                TaskCategory = "Work",
                Tags = new List<string> { "docker", "cleanup" },
                TriggerType = "Weekly",
                TriggerHour = 19,
                WeeklyDays = new List<string> { "Friday" },
                Note = L("Templates.DockerPrune.Note", "Requires Docker Desktop to be running. Deletes unused Docker data permanently.")
            },
            new TaskTemplate
            {
                Id = "dev.log-rotation",
                Group = GroupDeveloper,
                Glyph = "\uE7C3",
                Name = L("Templates.LogRotation.Name", "Daily Log Rotation"),
                Description = L("Templates.LogRotation.Desc",
                    "Compresses yesterday's *.log files into a dated ZIP archive and removes archives older than 30 days."),
                Command = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " +
                            "\"$src='C:\\Logs'; $dst=Join-Path $src 'archive'; " +
                            "New-Item -ItemType Directory -Force -Path $dst | Out-Null; " +
                            "$logs=Get-ChildItem -Path $src -Filter *.log -File | " +
                            "Where-Object { $_.LastWriteTime -lt (Get-Date).Date }; " +
                            "if ($logs) { Compress-Archive -Path $logs.FullName " +
                            "-DestinationPath (Join-Path $dst (\\\"logs-$(Get-Date -Format yyyyMMdd).zip\\\")) -Force; " +
                            "$logs | Remove-Item -Force }; " +
                            "Get-ChildItem $dst -Filter *.zip | " +
                            "Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item -Force\"",
                TaskCategory = "Work",
                Tags = new List<string> { "logs", "cleanup" },
                TriggerType = "Daily",
                TriggerHour = 1,
                RunIfMissed = true,
                Note = L("Templates.LogRotation.Note", "Change 'C:\\Logs' to your own log directory before deploying.")
            },

            // ── Power & Utility ─────────────────────────────────────────────────
            new TaskTemplate
            {
                Id = "power.idle-shutdown",
                Group = GroupPower,
                Glyph = "\uE7E8",
                Name = L("Templates.IdleShutdown.Name", "Auto-Shutdown at Night when Idle"),
                Description = L("Templates.IdleShutdown.Desc",
                    "Shuts the PC down at night, but only after it has been idle — with a 60 second warning you can cancel."),
                Command = "shutdown.exe",
                Arguments = "/s /t 60 /c \"Scheduled idle shutdown. Run 'shutdown /a' to cancel.\"",
                OnlyIfIdle = true,
                RunIfMissed = false,
                TaskCategory = "System",
                Tags = new List<string> { "power" },
                TriggerType = "Daily",
                TriggerHour = 23,
                TriggerMinute = 30,
                Note = L("Templates.IdleShutdown.Note", "Shuts the machine down. Unsaved work in open apps may be lost.")
            },
            new TaskTemplate
            {
                Id = "power.battery-saver",
                Group = GroupPower,
                Glyph = "\uE83E",
                Name = L("Templates.BatterySaver.Name", "Battery Saver Task Suspend"),
                Description = L("Templates.BatterySaver.Desc",
                    "Switches Windows to the Power Saver plan at logon so background work is throttled while unplugged."),
                Command = "powercfg.exe",
                Arguments = "/setactive a1841308-3541-4fab-bc81-f71556f20b4a",
                TaskCategory = "System",
                Tags = new List<string> { "power", "battery" },
                TriggerType = "AtLogon",
                RunIfMissed = false,
                Note = L("Templates.BatterySaver.Note", "Pair this with the Global Snooze toggle to pause your own tasks while on battery.")
            },
            new TaskTemplate
            {
                Id = "power.startup-cleanup",
                Group = GroupPower,
                Glyph = "\uE72C",
                Name = L("Templates.RecycleBin.Name", "Empty Recycle Bin Weekly"),
                Description = L("Templates.RecycleBin.Desc",
                    "Empties the Recycle Bin for the current user once a week without a confirmation prompt."),
                Command = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"",
                TaskCategory = "Maintenance",
                Tags = new List<string> { "cleanup" },
                TriggerType = "Weekly",
                TriggerHour = 20,
                WeeklyDays = new List<string> { "Saturday" },
                Note = L("Templates.RecycleBin.Note", "Deleted items become unrecoverable once the bin is emptied.")
            }
        };

        public static List<TaskTemplate> GetByGroup(string group) =>
            GetAll().Where(t => string.Equals(t.Group, group, StringComparison.Ordinal)).ToList();

        /// <summary>
        /// Converts a template into a fully-formed task model with the recommended trigger applied,
        /// ready to hand to the editor dialog.
        /// </summary>
        public static ScheduledTaskModel ToTaskModel(TaskTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            var start = DateTime.Today.AddHours(template.TriggerHour).AddMinutes(template.TriggerMinute);
            if (start < DateTime.Now) start = start.AddDays(1);

            var trigger = new TaskTriggerModel
            {
                TriggerType = template.TriggerType,
                ScheduleInfo = start.ToString("yyyy-MM-dd HH:mm:ss"),
                DailyInterval = template.DailyInterval,
                WeeklyInterval = template.WeeklyInterval,
                WeeklyDays = new List<string>(template.WeeklyDays)
            };

            var model = new ScheduledTaskModel
            {
                Name = template.Name,
                Description = template.Description,
                Author = Environment.UserName,
                Category = template.TaskCategory,
                IsEnabled = true,
                RunWithHighestPrivileges = template.RunAsAdmin,
                WakeToRun = template.WakeToRun,
                RunIfMissed = template.RunIfMissed,
                OnlyIfIdle = template.OnlyIfIdle,
                OnlyIfAC = template.OnlyIfAC,
                DisallowStartOnBatteries = template.OnlyIfAC
            };

            model.Tags = new System.Collections.ObjectModel.ObservableCollection<string>(template.Tags);
            model.Actions.Add(new TaskActionModel
            {
                Command = template.Command,
                Arguments = template.Arguments,
                WorkingDirectory = template.WorkingDirectory
            });
            model.TriggersList.Add(trigger);

            return model;
        }
    }
}
