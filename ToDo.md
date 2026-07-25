# To Do:

## Data-loss / silently corrupts existing data
1. Script Editor "Save" wipes all saved script templates (`ScriptEditorPage.xaml.cs` `SaveButton_Click` saves from a fresh, unloaded `ScriptLibraryViewModel`, overwriting `user_templates.json`).
2. Editing an existing task silently clears `OnlyIfAC` / `OnlyIfNetwork` / `WakeToRun` (`MainPage.xaml.cs` `EditTask_Click` never populates them from the task being edited).
3. Folder rename/move can silently drop a task (`TaskServiceWrapper.CopyFolderContents` swallows copy failures, caller deletes source folder anyway).
4. "Restart on failure" can be silently dropped on save if `RestartInterval` fails to parse (`ConfigureTaskDefinition`).

## Confirmed — feature doesn't do what it claims
5. Task expiration date/time (`EditTaskExpires`/`ExpirationDate`/`ExpirationTime`) is completely unwired.
6. "Stop task if runs longer than" (`EditTaskStopAfter`) is completely unwired.
7. Idle-trigger duration, Event-trigger fields (Log/Source/EventId), and idle Conditions settings are completely unwired.
8. History date filter (Today/Yesterday/This Week/All Time) is a no-op.
9. Dashboard "Run History" chart over-counts failures (treats every non-"Task Completed" event as a failure instead of checking for "Task Failed").
10. Dashboard chart/recent-activity/failed-list silently caps at the 20 most-recently-run tasks.
11. Import Settings updates the language dropdown visually but doesn't actually re-localize the running app.
12. Tray icon is never removed on Exit — `TrayIconService.Dispose()` has zero call sites, leaves a ghost icon.
13. "Task Started"/"Task Failed" toast notifications don't route back into the app on click.
14. Quick Actions status can be clobbered mid-run by a stale 5-second reset timer from a previous run.
15. `sfc`/`DISM` Quick Actions redirect stdout but never read it — pipe-buffer deadlock risk.
16. `VeloPackUpdateService.ApplyAndRestart` swallows all exceptions with zero user-facing feedback.
17. Stats-panel "Failed" count and the "Failed" tile's click-filter use different predicates (disagree on "Task Registered").
18. `WhatsNewDialog` can throw if GitHub returns `null` for a release's name/body/URL.

## Lower severity (leaks, dead code, minor UX)
19. `EventLogReader` never disposed in `DiscoverTasksFromEventLog`/`GetTaskHistory` — native handle leak.
20. `Error_Log.txt`/`Crash_Log.txt` have no size cap (only the main log rotates).
21. Batch Stop doesn't clear the spinning-ring state (`IsRunning`) the way single-task Stop does.
22. History detail dialog omits `ExitCode`.
23. Ctrl+N can throw if pressed while another dialog is already open.
24. Dashboard language-switch edge case: ja-JP "All Tags/Categories" strings missing from a hardcoded comparison list, can zero out the dashboard.
25. `NlmInterop.cs` (~150 lines of raw COM interop) is dead code — network profiles are read from the registry instead.
26. `SettingsService.Load()`/`Save()` swallow I/O exceptions with no logging.

# Changelog:

## [V1.8.1] - 2026-05-05
1. Fixed Issue #5 ARM 64 portable crashes on startup (removed unsupported PublishSingleFile; portable is now ZIP-only).
2. Fix Issue #16 Drag & Drop causes Unhandled Exception when run as admin.
3. Add Language switching with #17 -> Thanks to @Chan-Yuu for the great job!
4. Add System Maintenance Quick Actions
5. Add Integrated Script Editor
6. Let's see what we can do to make it even better.
