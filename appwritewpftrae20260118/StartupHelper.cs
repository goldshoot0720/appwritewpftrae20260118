using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace appwritewpftrae20260118
{
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "AppwriteSubscriptionViewer";

        // 需要移除的其他競爭 auto-start 項目
        private static readonly string[] CompetingRegistryEntries =
        {
            "UnoAppwriteTrae",
            "AvaloniaAppwriteSubscriptionManager",
            "AvaloniaAppwriteApp"
        };

        // 需要終止的其他競爭進程名稱 (不含 .exe)
        private static readonly string[] CompetingProcessNames =
        {
            "unoappwritetrae20260119",
            "avaloniaappwritetrae20260119"
        };

        public static void EnsureRunOnStartup()
        {
            try
            {
                RemoveCompetingEntries();
                KillCompetingProcesses();

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

        private static void RemoveCompetingEntries()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
                {
                    if (key == null) return;
                    foreach (var name in CompetingRegistryEntries)
                    {
                        try
                        {
                            if (key.GetValue(name) != null)
                            {
                                key.DeleteValue(name, throwOnMissingValue: false);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void KillCompetingProcesses()
        {
            try
            {
                foreach (var processName in CompetingProcessNames)
                {
                    try
                    {
                        var processes = Process.GetProcessesByName(processName);
                        foreach (var proc in processes)
                        {
                            try
                            {
                                proc.Kill();
                            }
                            catch
                            {
                            }
                            finally
                            {
                                proc.Dispose();
                            }
                        }
                    }
                    catch
                    {
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
