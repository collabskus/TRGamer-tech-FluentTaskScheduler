# To Do:

1. ***More TBD***

# Changelog:

## [Unreleased] - Full-app correctness audit
Ran a systematic audit of every feature in the app after finding the trigger start-time/recurrence
bugs. Found and fixed 26 issues, from silent data loss to dead code:

**Data-loss / silently corrupted existing data:**
1. Script Editor "Save" wiped all saved script templates on every save.
2. Editing an existing task silently cleared `OnlyIfAC` / `OnlyIfNetwork` / `WakeToRun`.
3. Folder rename/move could silently drop a task if copying it failed.
4. "Restart on failure" could be silently dropped on save if the interval failed to parse.

**Features that didn't do what they claimed:**
5. Task expiration date/time was completely unwired.
6. "Stop task if runs longer than" was completely unwired.
7. Idle-trigger duration, Event-trigger fields, and idle Conditions settings were completely unwired.
8. History date filter (Today/Yesterday/This Week/All Time) was a no-op.
9. Dashboard "Run History" chart over-counted failures.
10. Dashboard chart/recent-activity/failed-list silently capped at the 20 most-recently-run tasks.
11. Import Settings didn't actually re-localize the running app.
12. Tray icon was never removed on Exit, leaving a ghost icon.
13. "Task Started"/"Task Failed" toast notifications didn't route back into the app on click.
14. Quick Actions status could be clobbered mid-run by a stale reset timer from a previous run.
15. `sfc`/`DISM` Quick Actions had a stdout pipe-buffer deadlock risk.
16. Update-apply failures were silently swallowed with zero user-facing feedback.
17. Stats-panel "Failed" count and the "Failed" filter disagreed on what counted as a failure.
18. `WhatsNewDialog` could throw if GitHub returned `null` for a release's fields.

**Lower severity (leaks, dead code, minor UX):**
19. `EventLogReader` was never disposed — native handle leak.
20. `Error_Log.txt`/`Crash_Log.txt` had no size cap.
21. Batch Stop didn't clear the spinning-ring state the way single-task Stop does.
22. History detail dialog omitted `ExitCode`.
23. Ctrl+N could throw if pressed while another dialog was already open.
24. Dashboard language-switch edge case could zero out the dashboard for languages missing from a hardcoded string list (e.g. ja-JP) — replaced with a language-independent flag.
25. Removed ~150 lines of dead COM interop code (`NlmInterop.cs`).
26. `SettingsService` load/save failures are now logged instead of silently swallowed.

## [V1.8.1] - 2026-05-05

## [V1.8.1] - 2026-05-05
1. Fixed Issue #5 ARM 64 portable crashes on startup (removed unsupported PublishSingleFile; portable is now ZIP-only).
2. Fix Issue #16 Drag & Drop causes Unhandled Exception when run as admin.
3. Add Language switching with #17 -> Thanks to @Chan-Yuu for the great job!
4. Add System Maintenance Quick Actions
5. Add Integrated Script Editor
6. Let's see what we can do to make it even better.
