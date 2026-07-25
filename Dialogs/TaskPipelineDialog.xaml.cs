using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentTaskScheduler.Models;
using FluentTaskScheduler.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentTaskScheduler.Dialogs
{
    /// <summary>Row in the task pickers: shows the friendly name, carries the full path.</summary>
    public class PipelineTaskChoice
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string DisplayName => string.IsNullOrEmpty(Path) || Path == "\\" + Name
            ? Name
            : $"{Name}  ({Path})";
    }

    /// <summary>
    /// Editor for a task's completion actions ("execution pipeline"): which tasks run after this
    /// one finishes, split by exit code 0 vs non-zero.
    /// </summary>
    public sealed partial class TaskPipelineDialog : ContentDialog
    {
        private readonly string _ownTaskPath;
        private readonly ObservableCollection<string> _successPaths = new();
        private readonly ObservableCollection<string> _failurePaths = new();

        /// <summary>The edited pipeline. Only meaningful once the dialog returned Primary.</summary>
        public TaskPipeline Result { get; private set; } = new();

        private static string L(string key, string fallback) => LocalizationService.GetString(key, fallback);

        /// <param name="ownTaskPath">Path of the task being edited, excluded from the pickers to avoid self-chaining.</param>
        /// <param name="current">Existing configuration, or null for a fresh one.</param>
        /// <param name="availableTasks">Every task the user can chain to.</param>
        public TaskPipelineDialog(string ownTaskPath, TaskPipeline? current, IEnumerable<ScheduledTaskModel> availableTasks)
        {
            this.InitializeComponent();
            _ownTaskPath = ownTaskPath ?? "";
            this.RequestedTheme = SettingsService.Theme;

            var existing = current?.Clone() ?? new TaskPipeline();
            foreach (var p in existing.OnSuccessTasks) _successPaths.Add(p);
            foreach (var p in existing.OnFailureTasks) _failurePaths.Add(p);

            SuccessList.ItemsSource = _successPaths;
            FailureList.ItemsSource = _failurePaths;
            PipelineEnabledSwitch.IsOn = existing.IsEnabled;

            var choices = (availableTasks ?? Enumerable.Empty<ScheduledTaskModel>())
                .Where(t => !string.IsNullOrWhiteSpace(t.Path))
                .Where(t => !string.Equals(t.Path, _ownTaskPath, StringComparison.OrdinalIgnoreCase))
                .Where(t => !t.IsReadOnlyFallback)
                .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new PipelineTaskChoice { Path = t.Path, Name = t.Name })
                .ToList();

            SuccessTaskPicker.ItemsSource = choices;
            FailureTaskPicker.ItemsSource = new List<PipelineTaskChoice>(choices);

            ApplyLocalizedUi();
            UpdateBodyState();

            if (choices.Count == 0)
            {
                ShowWarning(L("Pipeline.Warning.NoTasks",
                    "No other tasks are available to chain to. Create another task first."));
            }
        }

        private void ApplyLocalizedUi()
        {
            Title = L("Pipeline.Dialog.Title", "Completion Actions (Task Pipeline)");
            PrimaryButtonText = L("Dialog.Common.Save", "Save");
            CloseButtonText = L("Dialog.Common.Cancel", "Cancel");

            PipelineInfoBar.Title = L("Pipeline.Info.Title", "How chaining works");
            PipelineInfoBar.Message = L("Pipeline.Info.Message",
                "When this task finishes, FluentTaskScheduler reads the action's exit code from the Windows Task Scheduler event log. " +
                "Exit code 0 starts the success tasks; any other code starts the failure tasks. " +
                "The app must be running for chained tasks to be started, and chains are skipped while Global Snooze is active.");

            PipelineEnabledSwitch.Header = L("Pipeline.EnableSwitch", "Run downstream tasks when this task completes");
            PipelineEnabledSwitch.OnContent = L("Dialog.Common.On", "On");
            PipelineEnabledSwitch.OffContent = L("Dialog.Common.Off", "Off");

            OnSuccessHeader.Text = L("Pipeline.OnSuccess", "On success (exit code 0)");
            OnFailureHeader.Text = L("Pipeline.OnFailure", "On failure (any other exit code)");

            AddSuccessButton.Content = L("Pipeline.Add", "Add");
            AddFailureButton.Content = L("Pipeline.Add", "Add");
            RemoveSuccessButton.Content = L("Pipeline.Remove", "Remove selected");
            RemoveFailureButton.Content = L("Pipeline.Remove", "Remove selected");

            SuccessTaskPicker.PlaceholderText = L("Pipeline.PickTask", "Choose a task...");
            FailureTaskPicker.PlaceholderText = L("Pipeline.PickTask", "Choose a task...");
        }

        private void ShowWarning(string message)
        {
            PipelineWarningBar.Message = message;
            PipelineWarningBar.IsOpen = true;
        }

        private void PipelineEnabled_Toggled(object sender, RoutedEventArgs e) => UpdateBodyState();

        private void UpdateBodyState()
        {
            // The lists stay visible when disabled so the user can see what is configured.
            if (PipelineBodyPanel != null)
                PipelineBodyPanel.Opacity = PipelineEnabledSwitch.IsOn ? 1.0 : 0.5;
        }

        private void AddSuccess_Click(object sender, RoutedEventArgs e) =>
            AddChoice(SuccessTaskPicker, _successPaths, _failurePaths);

        private void AddFailure_Click(object sender, RoutedEventArgs e) =>
            AddChoice(FailureTaskPicker, _failurePaths, _successPaths);

        private void AddChoice(ComboBox picker, ObservableCollection<string> target, ObservableCollection<string> other)
        {
            if (picker.SelectedItem is not PipelineTaskChoice choice) return;

            if (target.Contains(choice.Path, StringComparer.OrdinalIgnoreCase))
            {
                ShowWarning(string.Format(L("Pipeline.Warning.Duplicate", "'{0}' is already in this list."), choice.Name));
                return;
            }

            target.Add(choice.Path);
            PipelineWarningBar.IsOpen = false;
            picker.SelectedItem = null;

            if (other.Contains(choice.Path, StringComparer.OrdinalIgnoreCase))
            {
                ShowWarning(string.Format(
                    L("Pipeline.Warning.BothLists", "'{0}' now runs on both success and failure — it will start after every completion."),
                    choice.Name));
            }
        }

        private void RemoveSuccess_Click(object sender, RoutedEventArgs e)
        {
            if (SuccessList.SelectedItem is string path) _successPaths.Remove(path);
        }

        private void RemoveFailure_Click(object sender, RoutedEventArgs e)
        {
            if (FailureList.SelectedItem is string path) _failurePaths.Remove(path);
        }

        private void Dialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Result = new TaskPipeline
            {
                IsEnabled = PipelineEnabledSwitch.IsOn,
                OnSuccessTasks = _successPaths.ToList(),
                OnFailureTasks = _failurePaths.ToList()
            };

            if (Result.IsEnabled && !Result.HasAnyTargets)
            {
                args.Cancel = true;
                ShowWarning(L("Pipeline.Warning.NoTargets",
                    "Add at least one downstream task, or turn the switch off."));
                return;
            }

            LogService.Info(
                $"Pipeline saved for '{_ownTaskPath}': enabled={Result.IsEnabled}, " +
                $"{Result.OnSuccessTasks.Count} on success, {Result.OnFailureTasks.Count} on failure.");
        }
    }
}
