using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentTaskScheduler.Models;
using FluentTaskScheduler.Services;
using Microsoft.UI.Xaml;

namespace FluentTaskScheduler.ViewModels
{
    /// <summary>A template card as shown in the library grid.</summary>
    public class TaskTemplateCard
    {
        public TaskTemplate Template { get; set; } = new();

        public string Name => Template.Name;
        public string Description => Template.Description;
        public string Glyph => string.IsNullOrEmpty(Template.Glyph) ? "" : Template.Glyph;
        public string Note => Template.Note;
        public Visibility NoteVisibility => Template.HasNote ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AdminVisibility => Template.RunAsAdmin ? Visibility.Visible : Visibility.Collapsed;
        public string DeployLabel => LocalizationService.GetString("Templates.Deploy", "Deploy Template");

        /// <summary>"powershell.exe · Daily 03:00" style summary line.</summary>
        public string Summary
        {
            get
            {
                string command = System.IO.Path.GetFileName(Template.Command);
                string trigger = Template.TriggerType switch
                {
                    "AtLogon" => LocalizationService.GetString("Trigger.AtLogon", "At Logon"),
                    "AtStartup" => LocalizationService.GetString("Trigger.AtStartup", "At Startup"),
                    "OnIdle" => LocalizationService.GetString("Trigger.OnIdle", "On Idle"),
                    "Weekly" => $"{LocalizationService.GetString("Dialog.Trigger.Weekly", "Weekly")} " +
                                $"{string.Join(", ", Template.WeeklyDays)} {Template.TriggerHour:00}:{Template.TriggerMinute:00}",
                    _ => $"{LocalizationService.GetString("Dialog.Trigger.Daily", "Daily")} " +
                         $"{Template.TriggerHour:00}:{Template.TriggerMinute:00}"
                };
                return $"{command}  ·  {trigger}";
            }
        }
    }

    /// <summary>A named group of template cards, rendered as one section of the library.</summary>
    public class TaskTemplateGroup
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public ObservableCollection<TaskTemplateCard> Templates { get; } = new();
    }

    public class TaskTemplatesViewModel
    {
        public ObservableCollection<TaskTemplateGroup> Groups { get; } = new();

        /// <summary>Rebuilds the library. Called on load and whenever the language changes.</summary>
        public void Load()
        {
            Groups.Clear();

            var all = TaskTemplateLibrary.GetAll();
            foreach (var key in TaskTemplateLibrary.Groups)
            {
                var group = new TaskTemplateGroup
                {
                    Key = key,
                    DisplayName = TaskTemplateLibrary.GetGroupDisplayName(key)
                };

                foreach (var template in all.Where(t => string.Equals(t.Group, key, StringComparison.Ordinal)))
                    group.Templates.Add(new TaskTemplateCard { Template = template });

                if (group.Templates.Count > 0) Groups.Add(group);
            }
        }
    }
}
