using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FluentTaskScheduler.Models;
using FluentTaskScheduler.Services;
using FluentTaskScheduler.ViewModels;

namespace FluentTaskScheduler
{
    /// <summary>
    /// Built-in task template library. "Deploy Template" opens the normal task editor pre-filled,
    /// so nothing is registered with Windows until the user reviews and saves it.
    /// </summary>
    public sealed partial class TaskTemplatesPage : Page
    {
        public TaskTemplatesViewModel ViewModel { get; } = new();

        private MainPage? _ownerMainPage;

        private static string L(string key, string fallback) => LocalizationService.GetString(key, fallback);

        public TaskTemplatesPage()
        {
            this.InitializeComponent();
            LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
            this.Unloaded += (s, e) => LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;
            ApplyLocalizedUi();
        }

        private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
        {
            if (DispatcherQueue == null) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyLocalizedUi();
                ViewModel.Load();
            });
        }

        private void ApplyLocalizedUi()
        {
            TemplatesTitle.Text = L("Templates.Title", "Task Template Library");
            TemplatesSubtitle.Text = L("Templates.Subtitle",
                "Production-ready presets for common automation jobs. Deploying a template opens the task editor pre-filled with the command, arguments and a recommended schedule.");

            TemplatesInfoBar.Title = L("Templates.Info.Title", "Nothing is created until you save");
            TemplatesInfoBar.Message = L("Templates.Info.Message",
                "Review the command, arguments and trigger in the editor before saving — several templates delete files or change system state, and some need administrator privileges.");
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is MainPage mp) _ownerMainPage = mp;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            PageScrollViewer.IsScrollInertiaEnabled = SettingsService.SmoothScrolling;
            if (ViewModel.Groups.Count == 0) ViewModel.Load();
        }

        private void DeployTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not TaskTemplate template) return;

            var owner = _ownerMainPage ?? MainPage.Current;
            if (owner == null)
            {
                LogService.Error($"Cannot deploy template '{template.Id}': no main page is available.");
                return;
            }

            try
            {
                LogService.Info($"Deploying task template '{template.Id}'.");
                owner.OpenCreateTaskFromTaskTemplate(template);
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to deploy task template '{template.Id}'.", ex);
            }
        }
    }
}
