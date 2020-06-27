using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace PCSleeper
{
    internal static class PcManager
    {
        /// <summary>
        /// This app may install itself onto the computer if ran with admin privileges and the user gives consent. It does so by registering a key in Windows Registry (regedit) and installing the executable in Program Files. It may then start whenever system is powered on.
        /// </summary>
        private const string _appRegistryKeyName = "pcSleeper";

        internal static bool IsAppAlreadyRunning()
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1)
            {
                MessageBox.Show("PcSleeper is already running!");
                System.Diagnostics.Process.GetCurrentProcess().Kill(); //this line should render the return below (or any further code) kinda pointless
                return true;
            }

            return false;
        }

        internal static void TryToInstallThisApp()
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

        internal static void MakePcSleep()
        {
            Logger.LogInfo("Sending PC to sleep.");
            bool retVal = Application.SetSuspendState(PowerState.Suspend, false, false);

            if (retVal == false)
            {
                Logger.LogError("Could not suspend the system.");
                MessageBox.Show("Could not suspend the system.");
            }
        }
    }
}