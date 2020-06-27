using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Timers;

namespace PCSleeper
{
    internal class SleepChecker
    {
        /// <summary>
        /// Timer which runs all the time this app is alive and checks every <see cref="CheckIntervalOfSleepChecker"/> whether it is night time (as according to <see cref="NightStartHour"/> and <see cref="NightEndHour"/>) and whether the PC has been idle for <see cref="NightIdleTimeLimit"/>
        /// and if so, makes the computer sleep. Won't make it sleep if XInput gamepad is connected.
        /// </summary>
        private System.Timers.Timer _sleepChecker { get; set; } = null;

        /// <summary>
        /// Time property which decides when the night - and the possiblity to make the PC go to sleep - starts.
        /// </summary>
        private TimeSpan NightStartHour { get; } = new TimeSpan(0, 0, 0); //TODO: Config

        /// <summary>
        /// Time property which decides when the night - and the possiblity to make the PC go to sleep - ends.
        /// </summary>
        private TimeSpan NightEndHour { get; } = new TimeSpan(7, 0, 0); //TODO: Config

        private uint CheckIntervalOfSleepChecker = TimeHelper.Minutes(15); //TODO: Config

        private uint NightIdleTimeLimit = TimeHelper.Minutes(30); //TODO: Config

        internal SleepChecker()
        {
            CreateSleepChecker();
        }

        private void CreateSleepChecker()
        {
            Logger.LogInfo($@"Creating {nameof(_sleepChecker)}.");
            _sleepChecker = new System.Timers.Timer();
            _sleepChecker.Elapsed += new ElapsedEventHandler(OnTimedSleepCheckerEvent);
            _sleepChecker.Interval = CheckIntervalOfSleepChecker;
            _sleepChecker.Enabled = true;
        }

        private void OnTimedSleepCheckerEvent(object source, ElapsedEventArgs e)
        {
            //App has to target x86 for this gamepad checking thing to work, otherwise compiler throws runtime errors.
            GamePadState xboxControllerCurrentState = GamePad.GetState(PlayerIndex.One); // Get the current gamepad state. // Process input only if controller is connected.
            var idleTime = Win32_IdleHandler.GetIdleTime();
            Logger.LogInfo($@"{nameof(_sleepChecker)} - PC idle time: {TimeHelper.ConvertTicksToTime(idleTime)}.{(xboxControllerCurrentState.IsConnected ? " Game pad is connected!" : "")}");
            if (IsItNightTime() && !xboxControllerCurrentState.IsConnected && idleTime > GetTimeWithTolerance(NightIdleTimeLimit))
            {
                PcManager.MakePcSleep();
            }
        }

        private bool IsItNightTime()
        {
            TimeSpan currentTime = DateTime.Now.TimeOfDay;
            return (currentTime > NightStartHour) && (currentTime < NightEndHour);
        }

        private uint GetTimeWithTolerance(uint timeLimit) => Convert.ToUInt32(timeLimit * (1 - GetTolerancePercent())); //TODO: add some flag in config to use this feature?

        private double GetTolerancePercent() => 10f / 100; //TODO: move 10 to config

        public void DisposeOfSleepChecker()
        {
            _sleepChecker.Elapsed -= new ElapsedEventHandler(OnTimedSleepCheckerEvent);
            _sleepChecker.Close();
            _sleepChecker = null;
        }
    }
}