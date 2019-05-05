using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.Win32;
using System.IO;
using System.Security.Principal;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace PCSleeper
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SleeperContext());
        }
    }

    internal class SleeperContext : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private TimeSpan _nightStartHour = new TimeSpan(0, 0, 0);
        private TimeSpan _nightEndHour = new TimeSpan(7, 0, 0);
        private uint CheckInterval = TimeHelper.Minutes(15);
        private uint IdleTimeLimit = TimeHelper.Minutes(30);
        private string _appRegistryKeyName = "pcSleeper";

        internal SleeperContext()
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            {
                MessageBox.Show("PcSleeper is already running!");
                System.Diagnostics.Process.GetCurrentProcess().Kill(); //this line should render the return below kinda pointless
                return;
            }

            TryToInstallThisApp();

            System.Timers.Timer sleepChecker = new System.Timers.Timer();
            sleepChecker.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            sleepChecker.Interval = CheckInterval;
            sleepChecker.Enabled = true;

            //Initialize Tray Icon
            _trayIcon = new NotifyIcon()
            {
                Icon = new Icon(@"D:\Kyass\coding stuff\MyProjects\PCSleeper\PCSleeper\sleepIco.ico"),
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Exit/Kill", Exit)
                }),
                Visible = true,
                Text = "PcSleeper"
            };
        }

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
                        startupAppRegistryKey.SetValue(_appRegistryKeyName, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\PcSleeper\pcSleeper.exe");
                    }
                }
            }
            else if (startupAppRegistryKey.GetValue(_appRegistryKeyName) == null)
            {
                MessageBox.Show("Run this app as admin if you want it to be able to install itself and start at system startup!");
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            _trayIcon.Visible = false;

            Application.Exit();
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            //App has to target x86 for this gamepad checking thing to work, otherwise compiler throws runtime errors.
            GamePadState xboxControllerCurrentState = GamePad.GetState(PlayerIndex.One); // Get the current gamepad state. // Process input only if controller is connected.
            if (IsItNightTime() && !xboxControllerCurrentState.IsConnected && Win32_IdleHander.GetIdleTime() > IdleTimeLimit)
            {
                MakePcSleep();
            }
        }

        private bool IsItNightTime()
        {
            TimeSpan currentTime = DateTime.Now.TimeOfDay;
            return (currentTime > _nightStartHour) && (currentTime < _nightEndHour);
        }

        private void MakePcSleep()
        {
            bool retVal = Application.SetSuspendState(PowerState.Suspend, false, false);

            if (retVal == false)
                MessageBox.Show("Could not suspend the system.");
        }

        internal static bool IsRunAsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

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

    internal struct LastInputInfo
    {
        internal uint cbSize;
        internal uint dwTime;
    }
    internal class Win32_IdleHander
    {
        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo plii);

        [DllImport("Kernel32.dll")]
        private static extern uint GetLastError();

        internal static uint GetIdleTime()
        {
            LastInputInfo lastUserInput = new LastInputInfo();
            lastUserInput.cbSize = (uint)Marshal.SizeOf(lastUserInput);
            GetLastInputInfo(ref lastUserInput);

            return ((uint)Environment.TickCount - lastUserInput.dwTime);
        }

        internal static long GetTickCount()
        {
            return Environment.TickCount;
        }

        internal static long GetLastInputTime()
        {
            LastInputInfo lastUserInput = new LastInputInfo();
            lastUserInput.cbSize = (uint)Marshal.SizeOf(lastUserInput);
            if (!GetLastInputInfo(ref lastUserInput))
            {
                throw new Exception(GetLastError().ToString());
            }

            return lastUserInput.dwTime;
        }
    }
}
