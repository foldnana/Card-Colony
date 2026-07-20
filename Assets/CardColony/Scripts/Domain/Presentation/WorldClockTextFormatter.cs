using System;
using System.Globalization;
using CardColony.TimeSystem;

namespace CardColony.Presentation
{
    public static class WorldClockTextFormatter
    {
        public static string Format(ActionDrivenWorldClock clock)
        {
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            int wholeMinute = (int)Math.Floor(clock.MinuteOfDay);
            int hour = wholeMinute / 60;
            int minute = wholeMinute % 60;
            return string.Format(
                CultureInfo.InvariantCulture,
                "第{0}天\n{1:00}:{2:00}",
                clock.DayNumber,
                hour,
                minute);
        }
    }
}
