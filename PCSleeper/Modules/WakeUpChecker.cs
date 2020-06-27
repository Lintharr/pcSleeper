using Microsoft.Win32;
using System;
using System.Timers;

namespace PCSleeper
{
    internal class WakeUpChecker
    {
        /// <summary>
        /// Timer which is created whenever system wakes up, lives for a maximum of <see cref="WakeUpCheckerMaxLifespan"/> and checks every <see cref="CheckIntervalOfWakeUpChecker"/> whether the PC has been idle for <see cref="WakeUpIdleTimeLimit"/>. If the condtions are met, it makes the PC go back to sleep.
        /// An anti-cat measure.
        /// </summary>
        private System.Timers.Timer _wakeUpChecker { get; set; } = null;

        /// <summary>
        /// Property holding the exact time when <see cref="_wakeUpChecker"/> has been created.
        /// </summary>
        private DateTime? WakeUpCheckerStartTime { get; set; } = null;

        private uint CheckIntervalOfWakeUpChecker = TimeHelper.Minutes(1); //TODO: Config

        private uint WakeUpIdleTimeLimit = TimeHelper.Minutes(2); //TODO: Config

        private TimeSpan WakeUpCheckerMaxLifespan = new TimeSpan(0, 10, 0); //TODO: Config

        private Action<bool> ChangeTrayIcon;

        private Action<uint> DisplayNotification;

        internal WakeUpChecker(Action<uint> displayNotificationMethod, Action<bool> changeTrayIconMethod)
        {
            DisplayNotification = displayNotificationMethod;
            ChangeTrayIcon = changeTrayIconMethod;
            AttachToWindowsWakeUpEvent();
        }

        private void AttachToWindowsWakeUpEvent() //https://stackoverflow.com/questions/18206183/event-to-detect-system-wake-up-from-sleep-in-c-sharp
        {
            Logger.LogInfo("Attaching to Windows wake up event.");
            SystemEvents.PowerModeChanged += OnPowerChange;
        }

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Logger.LogInfo("System woke up!");
                CreateWakeUpChecker();
            }
        }

        private void CreateWakeUpChecker()
        {
            if (_wakeUpChecker != null)
            {
                Logger.LogWarning($@"{nameof(_wakeUpChecker)} already exists! Resetting timer.");
                WakeUpCheckerStartTime = DateTime.UtcNow;
                return;
            }
            Logger.LogInfo($@"Creating {nameof(_wakeUpChecker)}.");
            _wakeUpChecker = new System.Timers.Timer();
            _wakeUpChecker.Elapsed += new ElapsedEventHandler(OnTimedWakeUpEvent);
            _wakeUpChecker.Interval = CheckIntervalOfWakeUpChecker;
            WakeUpCheckerStartTime = DateTime.UtcNow;
            _wakeUpChecker.Enabled = true;
            ChangeTrayIcon(true);
        }

        private void OnTimedWakeUpEvent(object source, ElapsedEventArgs e)
        {
            var idleTime = Win32_IdleHandler.GetIdleTime();
            Logger.LogInfo($@"{nameof(_wakeUpChecker)} - PC idle time: {TimeHelper.ConvertTicksToTime(idleTime)}.");

            if (idleTime > TimeHelper.Seconds(50))
            {
                DisplayNotification(8);
                Logger.LogInfo("Displayed Windows notification.");
            }

            if ((DateTime.UtcNow - WakeUpCheckerStartTime) > WakeUpCheckerMaxLifespan)
            {
                Logger.LogInfo($@"{nameof(_wakeUpChecker)} managed to stay up through its lifespan. Disposing...");
                NullifyWakeUpChecker(null, null); //delete it if user has been active longer than max lifespan
            }

            if (idleTime > WakeUpIdleTimeLimit)
            {
                NullifyWakeUpChecker(null, null); //in case it wasn't already disposed of above
                PcManager.MakePcSleep();
            }
        }

        public void NullifyWakeUpChecker(object sender, EventArgs e)
        {
            WakeUpCheckerStartTime = null;
            DisposeOfWakeUpChecker();
        }

        public void DisposeOfWakeUpChecker()
        {
            SystemEvents.PowerModeChanged -= OnPowerChange;
            if (_wakeUpChecker != null)
            {
                _wakeUpChecker.Elapsed -= new ElapsedEventHandler(OnTimedWakeUpEvent);
                _wakeUpChecker.Close();
                _wakeUpChecker = null;
                ChangeTrayIcon(false);
                Logger.LogInfo($@"Disposed of {nameof(_wakeUpChecker)}.");
            }
        }
    }
}