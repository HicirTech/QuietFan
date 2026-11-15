using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QuietFan
{
    static class Program
    {
        public const string ExitEventName = @"Local\QuietFan.Exit";
        const string InstanceMutexName = @"Local\QuietFan.SingleInstance";
        const int ExitWaitMs = 10000;
        const int UsageError = 2;

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        /// <summary>
        /// QuietFan.exe            starts the tray app (one instance per user session)
        /// QuietFan.exe --exit     closes the running instance, which restores the boost setting
        /// QuietFan.exe --restore  like --exit; when none is running, restores the saved boost setting
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "--exit":
                        StopRunningInstance();
                        return 0;
                    case "--restore":
                        if (!StopRunningInstance()) TrayApp.RestoreFromSettings();
                        return 0;
                    default:
                        return UsageError;
                }
            }

            bool created;
            using (Mutex instance = new Mutex(true, InstanceMutexName, out created))
            {
                if (!created) return 0;
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
                instance.ReleaseMutex();
            }
            return 0;
        }

        /// <summary>
        /// Asks a running QuietFan to exit and waits until its process has ended, so an uninstaller can delete the
        /// file right after; false when none is running.
        /// </summary>
        static bool StopRunningInstance()
        {
            EventWaitHandle exitEvent;
            if (!EventWaitHandle.TryOpenExisting(ExitEventName, out exitEvent)) return false;
            Process[] running = OtherInstancesInSession();
            using (exitEvent) exitEvent.Set();
            foreach (Process process in running)
            {
                using (process) process.WaitForExit(ExitWaitMs);
            }
            return true;
        }

        static Process[] OtherInstancesInSession()
        {
            List<Process> others = new List<Process>();
            using (Process self = Process.GetCurrentProcess())
            {
                foreach (Process process in Process.GetProcessesByName(self.ProcessName))
                {
                    if (process.Id != self.Id && process.SessionId == self.SessionId) others.Add(process);
                    else process.Dispose();
                }
            }
            return others.ToArray();
        }
    }
}
