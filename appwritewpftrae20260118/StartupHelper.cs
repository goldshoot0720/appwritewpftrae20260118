using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;

namespace appwritewpftrae20260118
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "AppwriteSubscriptionViewer";

        public static void EnsureRunOnStartup()
        {
            try
            {
                var exePath = GetExecutablePath();
                if (string.IsNullOrWhiteSpace(exePath)) return;

                var command = $"\"{exePath}\" /background";

                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ??
                                 Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    var current = key.GetValue(AppName) as string;
                    if (!string.Equals(current, command, StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue(AppName, command);
                    }
                }
            }
            catch
            {
            }
        }

        private static string GetExecutablePath()
        {
            try
            {
                var location = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrWhiteSpace(location)) return null;
                return Path.GetFullPath(location);
            }
            catch
            {
                return null;
            }
        }
    }
}
