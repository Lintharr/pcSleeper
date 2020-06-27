using System;
using System.Runtime.InteropServices;

namespace PCSleeper
{
    internal struct LastInputInfo
    {
        internal uint cbSize;
        internal uint dwTime;
    }

    internal class Win32_IdleHandler
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