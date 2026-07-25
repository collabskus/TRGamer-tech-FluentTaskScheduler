using System;
using System.Globalization;
using System.Xml;

namespace FluentTaskScheduler.Services
{
    /// <summary>Small helpers around the ISO-8601 durations Task Scheduler settings are stored as
    /// (e.g. "PT90M", "P1D"), shared by the edit-dialog validation and <see cref="TaskServiceWrapper"/>.</summary>
    public static class DurationUtil
    {
        /// <summary>Validates an ISO-8601 duration without throwing.</summary>
        public static bool TryParseIsoDuration(string? value, out TimeSpan result)
        {
            if (string.IsNullOrWhiteSpace(value)) { result = TimeSpan.Zero; return false; }
            try { result = XmlConvert.ToTimeSpan(value); return true; }
            catch { result = TimeSpan.Zero; return false; }
        }

        /// <summary>Canonical wire format for trigger start times: invariant-culture, so it round-trips
        /// identically regardless of the app's display language.</summary>
        public const string ScheduleInfoFormat = "yyyy-MM-dd HH:mm:ss";

        public static string FormatScheduleInfo(DateTime value) =>
            value.ToString(ScheduleInfoFormat, CultureInfo.InvariantCulture);

        public static DateTime? TryParseScheduleInfo(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParseExact(value, ScheduleInfoFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                return exact;
            // Tolerate legacy/foreign-culture values written before this fix.
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose))
                return loose;
            return null;
        }
    }
}
