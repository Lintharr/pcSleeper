namespace PCSleeper
{
    /// <summary>
    /// Small helper to translate seconds/minutes into system/timer ticks.
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
    }
}