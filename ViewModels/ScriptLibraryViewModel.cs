using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;

namespace FluentTaskScheduler.ViewModels
{
    public class ScriptTemplateModel
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Command { get; set; } = "";
        public string Arguments { get; set; } = "";
        public bool RunAsAdmin { get; set; }

        // Runtime-only, not serialized
        [JsonIgnore] public bool IsUserTemplate { get; set; }
        [JsonIgnore] public Visibility DeleteVisibility => IsUserTemplate ? Visibility.Visible : Visibility.Collapsed;
        [JsonIgnore] public string UseLabel =>
            Services.LocalizationService.GetString("ScriptLibraryUseTemplateBtn.Content", "Create Task from Script");
    }

    public class ScriptLibraryViewModel
    {
        private static readonly string _userTemplatesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluentTaskScheduler", "user_templates.json");

        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        /// <summary>Every loaded script (built-in + user), independent of the current search filter.</summary>
        private readonly List<ScriptTemplateModel> _allScripts = new();
        private string _currentFilter = "";

        /// <summary>The subset of <see cref="_allScripts"/> matching the current search filter — this is what the UI binds to.</summary>
        public ObservableCollection<ScriptTemplateModel> Scripts { get; } = new();

        public async Task LoadScriptsAsync()
        {
            if (_allScripts.Count > 0) return;

            // Built-in templates
            try
            {
                string json = "";
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Scripts.json");

                if (File.Exists(fullPath))
                    json = await File.ReadAllTextAsync(fullPath);
                else
                {
                    try
                    {
                        var storageFile = await Package.Current.InstalledLocation.GetFileAsync("Assets\\Scripts.json");
                        json = await Windows.Storage.FileIO.ReadTextAsync(storageFile);
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var data = JsonSerializer.Deserialize<List<ScriptTemplateModel>>(json);
                    if (data != null) _allScripts.AddRange(data);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error loading scripts: {ex}"); }

            // User templates (appended after built-ins)
            LoadUserTemplates();
            ApplyFilter(_currentFilter);
        }

        public void AddUserTemplate(ScriptTemplateModel model)
        {
            model.IsUserTemplate = true;
            _allScripts.Add(model);
            SaveUserTemplates();
            ApplyFilter(_currentFilter);
        }

        public void DeleteUserTemplate(ScriptTemplateModel model)
        {
            _allScripts.Remove(model);
            SaveUserTemplates();
            ApplyFilter(_currentFilter);
        }

        /// <summary>Filters the displayed <see cref="Scripts"/> by name or description. Empty/null clears the filter.</summary>
        public void ApplyFilter(string? query)
        {
            _currentFilter = query ?? "";

            IEnumerable<ScriptTemplateModel> results = _allScripts;
            if (!string.IsNullOrWhiteSpace(_currentFilter))
            {
                results = _allScripts.Where(s =>
                    (s.Name != null && s.Name.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase)) ||
                    (s.Description != null && s.Description.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase)));
            }

            Scripts.Clear();
            foreach (var s in results) Scripts.Add(s);
        }

        private void LoadUserTemplates()
        {
            try
            {
                if (!File.Exists(_userTemplatesPath)) return;
                var json = File.ReadAllText(_userTemplatesPath);
                var data = JsonSerializer.Deserialize<List<ScriptTemplateModel>>(json);
                if (data != null)
                    foreach (var item in data) { item.IsUserTemplate = true; _allScripts.Add(item); }
            }
            catch { }
        }

        private void SaveUserTemplates()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_userTemplatesPath)!);
                var userTemplates = _allScripts.Where(s => s.IsUserTemplate).ToList();
                File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(userTemplates, _json));
            }
            catch { }
        }
    }
}
