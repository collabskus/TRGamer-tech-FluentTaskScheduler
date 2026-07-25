using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentTaskScheduler.Services;

namespace FluentTaskScheduler.Tests
{
    // SettingsService is a process-wide static singleton, so these tests must not run concurrently
    // with each other or with other test classes that touch it (see StaticServicesCollection).
    [Collection("StaticServices")]
    public class SettingsServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public SettingsServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "FTS_Tests_" + Guid.NewGuid().ToString("N"));
            SettingsService.UseStorageForTests(_tempDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void SettingChanged_IsReadableImmediately_EvenBeforeDebouncedSaveFlushes()
        {
            SettingsService.ConfirmDelete = false;
            Assert.False(SettingsService.ConfirmDelete);
        }

        [Fact]
        public void Flush_WritesPendingChangeToDiskImmediately()
        {
            SettingsService.IsMicaEnabled = false;
            SettingsService.Flush();

            string settingsFile = Path.Combine(_tempDir, "settings.json");
            Assert.True(File.Exists(settingsFile));

            var onDisk = JsonSerializer.Deserialize<JsonDocument>(File.ReadAllText(settingsFile));
            Assert.False(onDisk!.RootElement.GetProperty("IsMicaEnabled").GetBoolean());
        }

        [Fact]
        public async Task RapidSuccessiveWrites_AreCoalescedIntoOneDebouncedSave()
        {
            // Covers item 1.8: continuous window-resize events must not thrash the disk with one
            // write per tick.
            for (int i = 0; i < 50; i++)
            {
                SettingsService.SetWindowSize(1000 + i, 700 + i);
            }

            // Nothing should have hit disk yet — the debounce window hasn't elapsed.
            string settingsFile = Path.Combine(_tempDir, "settings.json");
            Assert.False(File.Exists(settingsFile));

            SettingsService.Flush();

            Assert.True(File.Exists(settingsFile));
            Assert.Equal(1049, SettingsService.WindowWidth);
            Assert.Equal(749, SettingsService.WindowHeight);
        }

        [Fact]
        public void ConcurrentWrites_FromMultipleThreads_DoNotCorruptTheFile()
        {
            // Covers item 1.8: Save() is called from the UI thread, the snooze timer thread, and
            // pipeline/reminder threads concurrently.
            var threads = new Thread[8];
            for (int t = 0; t < threads.Length; t++)
            {
                int captured = t;
                threads[t] = new Thread(() =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        SettingsService.ReminderLeadMinutes = captured * 100 + i;
                    }
                });
                threads[t].Start();
            }
            foreach (var th in threads) th.Join();

            SettingsService.Flush();

            string settingsFile = Path.Combine(_tempDir, "settings.json");
            string json = File.ReadAllText(settingsFile);
            // A corrupted/interleaved write would fail to parse.
            var doc = JsonSerializer.Deserialize<JsonDocument>(json);
            Assert.NotNull(doc);
        }

        [Fact]
        public void SaveSnoozeState_WritesImmediately_NotDebounced()
        {
            // Covers item 1.6c: the disabled-task list must be durable *before* SnoozeService starts
            // disabling tasks, so a crash mid-sweep is recoverable. SaveSnoozeState must therefore
            // bypass the debounce window entirely.
            SettingsService.SaveSnoozeState(true, DateTime.UtcNow.AddHours(1), false, "", new() { "\\Some\\Task" });

            string settingsFile = Path.Combine(_tempDir, "settings.json");
            Assert.True(File.Exists(settingsFile));

            var onDisk = JsonSerializer.Deserialize<JsonDocument>(File.ReadAllText(settingsFile));
            Assert.True(onDisk!.RootElement.GetProperty("IsSnoozed").GetBoolean());
        }

        [Fact]
        public void Load_AfterSaveImmediate_RoundTripsAllFields()
        {
            SettingsService.EnableTaskPipelines = false;
            SettingsService.SavedCategories = new() { "A", "B" };
            SettingsService.Flush();

            SettingsService.Load();

            Assert.False(SettingsService.EnableTaskPipelines);
            Assert.Equal(new[] { "A", "B" }, SettingsService.SavedCategories);
        }
    }
}
