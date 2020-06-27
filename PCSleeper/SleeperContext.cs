using System;
using System.Windows.Forms;

namespace PCSleeper
{
    internal class SleeperContext : ApplicationContext, IDisposable
    {
        #region Fields & Properties

        private SleepChecker _sleepChecker;
        private WakeUpChecker _wakeUpChecker;

        /// <summary>
        /// This app features a windows tray icon allowing simple app management.
        /// </summary>
        private NotifyIcon TrayIcon { get; set; }

        /// <summary>
        /// Allows setup of a global keyboard shortcut to kill <see cref="WakeUpChecker"/>.
        /// </summary>
        private KeyboardHotKeyHook HotKeyHook { get; set; } = new KeyboardHotKeyHook();

        private ModifierKeys HotkeyModifier = ModifierKeys.Control | ModifierKeys.Alt; //TODO: Config

        private System.Windows.Forms.Keys HotkeyKey = System.Windows.Forms.Keys.K; //TODO: Config

        #endregion Fields & Properties

        internal SleeperContext()
        {
            Logger.LogInfo("App starting.");
            if (PcManager.IsAppAlreadyRunning())
                return;

            PcManager.TryToInstallThisApp();
            _sleepChecker = new SleepChecker();
            _wakeUpChecker = new WakeUpChecker(DisplayNotification, ChangeTrayIcon);
            InitializeTrayIcon();
            InitializeHotKeyHook();
        }

        #region Launch app stuff

        private void InitializeTrayIcon()
        {
            TrayIcon = new NotifyIcon()
            {
                Icon = Properties.Resources.sleepIco,
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem($"Break wake up timer ({HotkeyModifier} + {HotkeyKey})", _wakeUpChecker.NullifyWakeUpChecker),
                    new MenuItem("Toggle info logging", ToggleLogger),
                    new MenuItem("Exit/Kill", AppExit),
                }),
                Visible = true,
                Text = "PcSleeper",
                BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Warning,
                BalloonTipTitle = "PCSleeper",
                BalloonTipText = $@"Pc will be sent to sleep in 1 minute. Click here to deactivate wake up timer. (Or use hotkey: {HotkeyModifier} + {HotkeyKey})",
            };
            TrayIcon.BalloonTipClicked += new EventHandler(WindowsNotificationClicked);
            TrayIcon.ContextMenu.MenuItems[1].Checked = ConfigManager.EnableLogging;
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

        private void WindowsNotificationClicked(object sender, EventArgs e)
        {
            _wakeUpChecker.NullifyWakeUpChecker(sender, e);
        }

        #endregion Launch app stuff

        #region HotKeyHook

        private void InitializeHotKeyHook()
        {
            // register the event that is fired after the key press.
            HotKeyHook.KeyPressed += new EventHandler<KeyPressedEventArgs>(Hook_KeyPressed);
            // register the keys combination as hot key.
            HotKeyHook.RegisterHotKey(HotkeyModifier, HotkeyKey); //TODO: Config.
        }

        private void Hook_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            Logger.LogInfo($"HotKeyHook used. Nullifying {nameof(WakeUpChecker)}.");
            _wakeUpChecker.NullifyWakeUpChecker(null, null);
        }

        #endregion HotKeyHook

        private void ChangeTrayIcon(bool changeToActive)
        {
            Logger.LogInfo($@"Changing icon to {(changeToActive ? "Active" : "Default")}.");
            TrayIcon.Icon = changeToActive ? Properties.Resources.sleepIcoActiveAlt : Properties.Resources.sleepIco;
        }

        private void DisplayNotification(uint seconds)
        {
            TrayIcon.ShowBalloonTip((int)TimeHelper.Seconds(seconds)); //Shows Windows notification //TODO: Seconds to config
        }

        public new void Dispose()
        {
            Logger.LogInfo("App closing.");
            _sleepChecker.DisposeOfSleepChecker();
            _sleepChecker = null;
            _wakeUpChecker.DisposeOfWakeUpChecker();
            _wakeUpChecker = null;
            HotKeyHook.KeyPressed -= new EventHandler<KeyPressedEventArgs>(Hook_KeyPressed);
            HotKeyHook.Dispose();
            TrayIcon.BalloonTipClicked -= new EventHandler(WindowsNotificationClicked);
            TrayIcon.Visible = false;
            TrayIcon.Dispose();
            base.Dispose();
        }
    }
}