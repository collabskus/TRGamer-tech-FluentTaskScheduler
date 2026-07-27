using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FluentTaskScheduler.Models;

namespace FluentTaskScheduler.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Services.TaskServiceWrapper _taskService = new();
        private List<ScheduledTaskModel> _allTasks = new();
        private bool _isLoading;
        private string _searchText = "";
        private string _currentFolderPath = "\\";
        private string _filterTag = "all";
        private ScheduledTaskModel? _selectedTask;
        
        public string ActionRunPrefix => Services.LocalizationService.GetString("Trigger.Run", "Run:");

        // Sorting
        public string SortColumn { get; private set; } = "";
        public bool SortAscending { get; private set; } = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ScheduledTaskModel> FilteredTasks { get; } = new();
        
        // Expose service for direct calls from UI where Command isn't appropriate yet
        public Services.TaskServiceWrapper TaskService => _taskService;

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    ApplyFilters();
                }
            }
        }

        public ScheduledTaskModel? SelectedTask
        {
            get => _selectedTask;
            set { _selectedTask = value; OnPropertyChanged(); }
        }

        public List<string> SavedCategories => Services.SettingsService.SavedCategories;
        public List<string> SavedTags => Services.SettingsService.SavedTags;
        public void RefreshSavedCategories() 
        { 
            OnPropertyChanged(nameof(SavedCategories)); 
            OnPropertyChanged(nameof(SavedTags)); 
        }

        public MainViewModel()
        {
            Services.LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
        }

        private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(ActionRunPrefix));
        }

        /// <summary>
        /// Unsubscribes from the static LocalizationService event. Without this, every MainPage
        /// (a new one is created per window) keeps its MainViewModel — and everything it
        /// transitively references — alive forever, even after the window closes (see 3.3).
        /// </summary>
        public void Cleanup()
        {
            Services.LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        }

        public bool IsTrayIconVisible => Services.SettingsService.EnableTrayIcon;
        public void RefreshTrayIconVisibility() => OnPropertyChanged(nameof(IsTrayIconVisible));

        private string? _loadErrorMessage;
        /// <summary>Set when the last <see cref="LoadTasksAsync"/> failed, so the page can surface
        /// an InfoBar instead of the failure only going to Debug output (see 3.11).</summary>
        public string? LoadErrorMessage
        {
            get => _loadErrorMessage;
            private set { _loadErrorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLoadError)); }
        }
        public bool HasLoadError => !string.IsNullOrEmpty(_loadErrorMessage);

        public async Task LoadTasksAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var tasks = await Task.Run(() => _taskService.GetAllTasks());
                _allTasks = tasks ?? new List<ScheduledTaskModel>();
                ApplyFilters();
                Services.TrayIconService.UpdateBadge(_allTasks.Count(t => t.State == "Running"));
                LoadErrorMessage = null;
            }
            catch (Exception ex)
            {
                Services.LogService.Error("Failed to load scheduled tasks", ex);
                LoadErrorMessage = ex.Message;
                _allTasks = new List<ScheduledTaskModel>();
                ApplyFilters();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void SetFilter(string filterTag)
        {
            _filterTag = filterTag;
            // If it's a global filter (footer items), reset folder path
            if (IsGlobalFilter(filterTag))
            {
                _currentFolderPath = "\\";
            }
            else if (!string.IsNullOrEmpty(filterTag) && filterTag != "Add")
            {
                _currentFolderPath = filterTag;
            }
            ApplyFilters();
        }

        private bool IsGlobalFilter(string tag)
        {
            return tag == "all";
        }

        private string _statusFilter = "all";

        /// <summary>
        /// Status shown in the toolbar dropdown: all / running / enabled / disabled / snoozed.
        /// This is independent of the folder selection, so a folder can be narrowed by status.
        /// </summary>
        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (_statusFilter == value) return;
                _statusFilter = string.IsNullOrEmpty(value) ? "all" : value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        /// <summary>Applies the toolbar status dropdown on top of the folder/search filters.</summary>
        private IEnumerable<ScheduledTaskModel> ApplyStatusFilter(IEnumerable<ScheduledTaskModel> query)
        {
            switch (_statusFilter)
            {
                case "running":
                    return query.Where(t => t.State == "Running");
                case "enabled":
                    return query.Where(t => t.IsEnabled);
                case "disabled":
                    return query.Where(t => !t.IsEnabled);
                case "snoozed":
                    // Only tasks this app suspended for the active global snooze — an empty result
                    // simply means nothing is currently suspended.
                    var suspended = new HashSet<string>(
                        Services.SettingsService.SnoozeDisabledTaskPaths ?? new List<string>(),
                        StringComparer.OrdinalIgnoreCase);
                    return suspended.Count == 0
                        ? Enumerable.Empty<ScheduledTaskModel>()
                        : query.Where(t => suspended.Contains(t.Path));
                default:
                    return query;
            }
        }

        /// <summary>Cycles sort: same column toggles Asc/Desc, new column defaults to Asc.</summary>
        public void SortBy(string column)
        {
            if (SortColumn == column) SortAscending = !SortAscending;
            else { SortColumn = column; SortAscending = true; }
            ApplyFilters();
        }

        /// <summary>Clears any active sort.</summary>
        public void ClearSort()
        {
            SortColumn = "";
            SortAscending = true;
            ApplyFilters();
        }

        public void ApplyFilters()
        {
            if (_allTasks == null) return;

            var query = _allTasks.AsEnumerable();

            // Hidden Visibility Filter
            if (!Services.SettingsService.ShowHiddenTasks)
            {
                query = query.Where(t => !t.IsHidden);
            }

            // Search Filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(t => 
                    (t.Name != null && t.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Category != null && t.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Tags != null && t.Tags.Any(tag => tag.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                );
            }

            // Folder filter — "all" spans every folder, anything else pins to one folder
            if (!IsGlobalFilter(_filterTag))
            {
                query = query.Where(t =>
                {
                    var taskDir = System.IO.Path.GetDirectoryName(t.Path);
                    if (string.IsNullOrEmpty(taskDir)) taskDir = "\\";
                    return taskDir.Equals(_currentFolderPath, StringComparison.OrdinalIgnoreCase);
                });
            }

            // Toolbar status dropdown
            query = ApplyStatusFilter(query);

            var results = SortColumn switch
            {
                "Name"    => SortAscending ? query.OrderBy(t => t.Name)         : query.OrderByDescending(t => t.Name),
                "Status"  => SortAscending ? query.OrderBy(t => t.State)        : query.OrderByDescending(t => t.State),
                "NextRun" => SortAscending ? query.OrderBy(t => t.NextRunTime)  : query.OrderByDescending(t => t.NextRunTime),
                "LastRun" => SortAscending ? query.OrderBy(t => t.LastRunTime)  : query.OrderByDescending(t => t.LastRunTime),
                _         => query
            };
            UpdateFilteredTasksCollection(results.ToList());
        }

        private void UpdateFilteredTasksCollection(List<ScheduledTaskModel> results)
        {
            // Optimization: Handle initial load or empty state efficiently (O(N))
            if (FilteredTasks.Count == 0)
            {
                foreach (var taskModel in results) FilteredTasks.Add(taskModel);
                return;
            }

            // LoadTasksAsync always builds brand-new ScheduledTaskModel instances, so a
            // reference-based diff below would never find a match and this "preserve scroll
            // position" logic degenerated into remove-everything/insert-everything on every single
            // refresh. Reusing the existing instance (keyed by Path) and updating its fields in
            // place is what actually keeps scroll position, selection, and toggle state stable
            // across a refresh (see 3.4).
            var existingByPath = new Dictionary<string, ScheduledTaskModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in FilteredTasks)
            {
                if (!string.IsNullOrEmpty(t.Path)) existingByPath.TryAdd(t.Path, t);
            }

            var reconciled = new List<ScheduledTaskModel>(results.Count);
            foreach (var fresh in results)
            {
                if (!string.IsNullOrEmpty(fresh.Path) && existingByPath.TryGetValue(fresh.Path, out var existing))
                {
                    existing.UpdateFrom(fresh);
                    reconciled.Add(existing);
                }
                else
                {
                    reconciled.Add(fresh);
                }
            }

            var resultsSet = new HashSet<ScheduledTaskModel>(reconciled);
            var currentSet = new HashSet<ScheduledTaskModel>(FilteredTasks);

            // Removing items that are no longer in the filtered results
            for (int i = FilteredTasks.Count - 1; i >= 0; i--)
            {
                if (!resultsSet.Contains(FilteredTasks[i]))
                {
                    currentSet.Remove(FilteredTasks[i]);
                    FilteredTasks.RemoveAt(i);
                }
            }

            // Inserting or moving items to match the results list
            for (int i = 0; i < reconciled.Count; i++)
            {
                var taskModel = reconciled[i];
                if (!currentSet.Contains(taskModel))
                {
                    FilteredTasks.Insert(i, taskModel);
                    currentSet.Add(taskModel);
                }
                else
                {
                    int oldIndex = FilteredTasks.IndexOf(taskModel);
                    if (oldIndex != i)
                    {
                        FilteredTasks.Move(oldIndex, i);
                    }
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
