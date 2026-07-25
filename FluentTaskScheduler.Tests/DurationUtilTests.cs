using System;
using System.Globalization;
using System.Threading;
using FluentTaskScheduler.Services;

namespace FluentTaskScheduler.Tests
{
    // Covers items 1.3, 1.4, 1.15: ISO-8601 duration validation and culture-invariant schedule
    // round-tripping.
    public class DurationUtilTests
    {
        [Theory]
        [InlineData("PT15M")]
        [InlineData("PT90M")]
        [InlineData("P1D")]
        [InlineData("PT1H")]
        public void TryParseIsoDuration_AcceptsValidIsoDurations(string input)
        {
            Assert.True(DurationUtil.TryParseIsoDuration(input, out var result));
            Assert.True(result > TimeSpan.Zero);
        }

        [Theory]
        [InlineData("1 hour")]
        [InlineData("90 minutes")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not a duration")]
        public void TryParseIsoDuration_RejectsNonIsoInput(string? input)
        {
            Assert.False(DurationUtil.TryParseIsoDuration(input, out _));
        }

        [Fact]
        public void FormatScheduleInfo_RoundTripsThroughTryParseScheduleInfo()
        {
            var original = new DateTime(2026, 3, 5, 14, 30, 0);
            string formatted = DurationUtil.FormatScheduleInfo(original);
            var parsed = DurationUtil.TryParseScheduleInfo(formatted);

            Assert.Equal(original, parsed);
        }

        [Fact]
        public void FormatScheduleInfo_IsInvariantAcrossCultures()
        {
            var original = new DateTime(2026, 3, 5, 14, 30, 0);
            var previousCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string germanFormatted = DurationUtil.FormatScheduleInfo(original);

                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
                string englishFormatted = DurationUtil.FormatScheduleInfo(original);

                Assert.Equal(germanFormatted, englishFormatted);
                Assert.Equal(original, DurationUtil.TryParseScheduleInfo(germanFormatted));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
            }
        }

        [Fact]
        public void TryParseScheduleInfo_ReturnsNullInsteadOfSilentlyDefaultingOnGarbageInput()
        {
            // The old behavior (plain DateTime.TryParse swallowing failures) silently produced
            // "today at 9 AM" on bad input; the fixed helper must surface the failure as null so
            // the caller can show a validation error instead (see item 1.15).
            Assert.Null(DurationUtil.TryParseScheduleInfo("not a date"));
            Assert.Null(DurationUtil.TryParseScheduleInfo(""));
            Assert.Null(DurationUtil.TryParseScheduleInfo(null));
        }
    }
}
