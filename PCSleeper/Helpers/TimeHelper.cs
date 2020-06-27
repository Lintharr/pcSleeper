namespace PCSleeper
{
    /// <summary>
    /// Small helper to translate seconds/minutes into system/timer ticks and the other way around.
    /// </summary>
    internal static class TimeHelper
    {
        internal static uint Seconds(uint howManySeconds)
        {
            return howManySeconds * 1000;
        }

        internal static uint Minutes(uint howManyMinutes)
        {
            return howManyMinutes * 60000;
        }

        internal static uint ConvertTicksToSeconds(uint ticks)
        {
            return ticks / 1000;
        }

        internal static string ConvertTicksToTime(uint ticks)
        {
            uint? minutes = null;
            uint? seconds = null;
            if (ticks > 60000)
                minutes = ticks / 60000;
            if (minutes.HasValue)
                seconds = (ticks - (minutes * 60000)) / 1000;
            else
                seconds = ticks / 1000;
            return $@"{minutes.GetValueOrDefault()}m{seconds.GetValueOrDefault()}s";
        }
    }
}