﻿using Microsoft.Win32;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Timers;
using System.Windows.Forms;

namespace PCSleeper
{
    internal class SleeperContext : ApplicationContext, IDisposable
    {
        #region Fields & Properties

        /// <summary>
        /// Timer which runs all the time this app is alive and checks every <see cref="CheckIntervalOfSleepChecker"/> whether it is night time (as according to <see cref="NightStartHour"/> and <see cref="NightEndHour"/>) and whether the PC has been idle for <see cref="NightIdleTimeLimit"/>
        /// and if so, makes the computer sleep. Won't make it sleep if XInput gamepad is connected.
        /// </summary>
        private System.Timers.Timer SleepChecker { get; set; } = null;

        /// <summary>
        /// Timer which is created whenever system wakes up, lives for a maximum of <see cref="WakeUpCheckerMaxLifespan"/> and checks every <see cref="CheckIntervalOfWakeUpChecker"/> whether the PC has been idle for <see cref="WakeUpIdleTimeLimit"/>. If the condtions are met, it makes the PC go back to sleep.
        /// An anti-cat measure.
        /// </summary>
        private System.Timers.Timer WakeUpChecker { get; set; } = null;

        /// <summary>
        /// Time property which decides when the night - and the possiblity to make the PC go to sleep - starts.
        /// </summary>
        private TimeSpan NightStartHour { get; } = new TimeSpan(0, 0, 0);

        /// <summary>
        /// Time property which decides when the night - and the possiblity to make the PC go to sleep - ends.
        /// </summary>
        private TimeSpan NightEndHour { get; } = new TimeSpan(7, 0, 0);

        /// <summary>
        /// Property holding the exact time when <see cref="WakeUpChecker"/> has been created.
        /// </summary>
        private DateTime? WakeUpCheckerStartTime { get; set; } = null;

        private uint CheckIntervalOfSleepChecker = TimeHelper.Minutes(15);
        private uint NightIdleTimeLimit = TimeHelper.Minutes(30);

        private uint CheckIntervalOfWakeUpChecker = TimeHelper.Minutes(1);
        private uint WakeUpIdleTimeLimit = TimeHelper.Minutes(2);
        private TimeSpan WakeUpCheckerMaxLifespan = new TimeSpan(0, 15, 0);

        /// <summary>
        /// This app features a windows tray icon to allow for simpler closing/killing of the app.
        /// </summary>
        private NotifyIcon TrayIcon;

        /// <summary>
        /// This app may install itself onto the computer if ran with admin privileges and the user gives consent. It does so by registering a key in Windows Registry (regedit) and installing the executable in Program Files. It may then start whenever system is powered on.
        /// </summary>
        private string _appRegistryKeyName = "pcSleeper";

        #endregion Fields & Properties

        internal SleeperContext()
        {
            Logger.LogInfo("App starting.");
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            {
                MessageBox.Show("PcSleeper is already running!");
                System.Diagnostics.Process.GetCurrentProcess().Kill(); //this line should render the return below kinda pointless
                return;
            }

            TryToInstallThisApp();
            InitializeTrayIcon();
            CreateSleeperChecker();
            AttachToWindowsWakeUpEvent(); //https://stackoverflow.com/questions/18206183/event-to-detect-system-wake-up-from-sleep-in-c-sharp
        }

        #region Launch app stuff

        private void TryToInstallThisApp()
        {
            RegistryKey startupAppRegistryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (IsRunAsAdministrator())
            {
                if (startupAppRegistryKey.GetValue(_appRegistryKeyName) == null)
                {
                    if (MessageBox.Show("PcSleeper hasn't been set to start at startup. Would you like to add it there?\r\nIt will also create PcSleeper folder in Program Files.", "PcSleeper", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\PcSleeper"))
                            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\PcSleeper");
                        File.Copy(Application.ExecutablePath.ToString(), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\PcSleeper\pcSleeper.exe");
                        File.Copy(Application.ExecutablePath.ToString(), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\PcSleeper\log4net.dll");
                        File.Copy(Application.ExecutablePath.ToString(), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\PcSleeper\log4net.config");
                        startupAppRegistryKey.SetValue(_appRegistryKeyName, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\PcSleeper\pcSleeper.exe");
                    }
                }
            }
            else if (startupAppRegistryKey.GetValue(_appRegistryKeyName) == null)
            {
                MessageBox.Show("Run this app as admin if you want it to be able to install itself and start at system startup!");
            }
        }

        internal static bool IsRunAsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void InitializeTrayIcon()
        {
            TrayIcon = new NotifyIcon()
            {
                Icon = new Icon(@"D:\Kyass\coding stuff\MyProjects\PCSleeper\PCSleeper\sleepIco.ico"),
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Log info", ToggleLogger),
                    new MenuItem("Exit/Kill", AppExit),
                }),
                Visible = true,
                Text = "PcSleeper"
            };
            TrayIcon.ContextMenu.MenuItems[0].Checked = ConfigManager.EnableLogging;
        }

        private void ToggleLogger(object sender, EventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            ConfigManager.EnableLogging = !ConfigManager.EnableLogging;
            menuItem.Checked = ConfigManager.EnableLogging;
        }

        private void AppExit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            TrayIcon.Visible = false;
            Dispose();
            Application.Exit();
        }

        #endregion Launch app stuff

        #region SleeperChecker

        private void CreateSleeperChecker()
        {
            Logger.LogInfo($@"Creating {nameof(SleepChecker)}.");
            SleepChecker = new System.Timers.Timer();
            SleepChecker.Elapsed += new ElapsedEventHandler(OnTimedSleepCheckerEvent);
            SleepChecker.Interval = CheckIntervalOfSleepChecker;
            SleepChecker.Enabled = true;
        }

        private void OnTimedSleepCheckerEvent(object source, ElapsedEventArgs e)
        {
            //App has to target x86 for this gamepad checking thing to work, otherwise compiler throws runtime errors.
            GamePadState xboxControllerCurrentState = GamePad.GetState(PlayerIndex.One); // Get the current gamepad state. // Process input only if controller is connected.
            var idleTime = Win32_IdleHandler.GetIdleTime();
            Logger.LogInfo($@"{nameof(SleepChecker)} - PC idle time: {TimeHelper.ConvertTicksToTime(idleTime)}. Is game pad connected: {xboxControllerCurrentState.IsConnected}.");
            if (IsItNightTime() && !xboxControllerCurrentState.IsConnected && idleTime > NightIdleTimeLimit)
            {
                MakePcSleep();
            }
        }

        private bool IsItNightTime()
        {
            TimeSpan currentTime = DateTime.Now.TimeOfDay;
            return (currentTime > NightStartHour) && (currentTime < NightEndHour);
        }

        #endregion SleeperChecker

        #region WakeUpChecker

        private void AttachToWindowsWakeUpEvent()
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
            if (WakeUpChecker != null)
            {
                Logger.LogWarning($@"{nameof(WakeUpChecker)} already exists!");
                return;
            }
            Logger.LogInfo($@"Creating {nameof(WakeUpChecker)}.");
            WakeUpChecker = new System.Timers.Timer();
            WakeUpChecker.Elapsed += new ElapsedEventHandler(OnTimedWakeUpEvent);
            WakeUpChecker.Interval = CheckIntervalOfWakeUpChecker;
            WakeUpCheckerStartTime = DateTime.UtcNow;
            WakeUpChecker.Enabled = true;
        }

        private void OnTimedWakeUpEvent(object source, ElapsedEventArgs e)
        {
            var idleTime = Win32_IdleHandler.GetIdleTime();
            Logger.LogInfo($@"{nameof(WakeUpChecker)} - PC idle time: {TimeHelper.ConvertTicksToTime(idleTime)}.");
            if ((DateTime.UtcNow - WakeUpCheckerStartTime) > WakeUpCheckerMaxLifespan)
            {
                WakeUpCheckerStartTime = null;
                DisposeOfWakeUpChecker();
            }

            if (idleTime > WakeUpIdleTimeLimit)
            {
                MakePcSleep();
            }
        }

        #endregion WakeUpChecker

        private void MakePcSleep()
        {
            Logger.LogInfo("Sending PC to sleep.");
            bool retVal = Application.SetSuspendState(PowerState.Suspend, false, false);

            if (retVal == false)
            {
                Logger.LogError("Could not suspend the system.");
                MessageBox.Show("Could not suspend the system.");
            }
        }

        public new void Dispose()
        {
            Logger.LogInfo("App closing.");
            SystemEvents.PowerModeChanged -= OnPowerChange;
            SleepChecker.Elapsed -= new ElapsedEventHandler(OnTimedSleepCheckerEvent);
            SleepChecker.Close();
            SleepChecker = null;
            DisposeOfWakeUpChecker();
            TrayIcon.Visible = false;
            base.Dispose();
        }

        private void DisposeOfWakeUpChecker()
        {
            if (WakeUpChecker != null)
            {
                WakeUpChecker.Elapsed -= new ElapsedEventHandler(OnTimedWakeUpEvent);
                WakeUpChecker.Close();
                WakeUpChecker = null;
                Logger.LogInfo($@"Disposed of {nameof(WakeUpChecker)}.");
            }
        }
    }
}