﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AzureWebFarm.Services
{
    public class BackgroundWorkerService : IDisposable
    {
        private readonly string _executablePath;
        private readonly Dictionary<string, List<Executable>> _executables;
        private readonly ExecutableFinder _executableFinder;

        public BackgroundWorkerService(string sitesPath, string executablePath)
        {
            _executablePath = executablePath;
            _executables = new Dictionary<string, List<Executable>>();
            _executableFinder = new ExecutableFinder(sitesPath);
        }

        public void Update(string siteName)
        {
            lock (_executables)
            {
                if (!_executables.ContainsKey(siteName))
                    _executables[siteName] = new List<Executable>();

                DisposeSite(siteName);
                _executables[siteName] = new List<Executable>();

                _executables[siteName].AddRange(_executableFinder.FindExecutables(siteName));

                foreach (var e in _executables[siteName])
                {
                    e.Copy(Path.Combine(_executablePath, siteName));
                    e.Execute();
                }
            }
        }

        public void DisposeSite(string siteName)
        {
            foreach (var e in _executables[siteName])
            {
                e.Dispose();
            }
        }

        public void Dispose()
        {
            ForEachExecutable(e => e.Dispose());
        }

        public void Ping()
        {
            ForEachExecutable(e => e.Ping());
        }

        public void Wait(TimeSpan maxWait)
        {
            ForEachExecutable(e => e.Wait(maxWait));
        }

        private void ForEachExecutable(Action<Executable> action)
        {
            lock (_executables)
            {
                foreach (var e in _executables.Keys.SelectMany(site => _executables[site]))
                {
                    action(e);
                }
            }
        }
    }

    public class ExecutableFinder
    {
        private readonly string _sitesPath;

        public ExecutableFinder(string sitesPath)
        {
            _sitesPath = sitesPath;
        }

        public IEnumerable<Executable> FindExecutables(string siteName)
        {
            var subDirs = Directory.EnumerateDirectories(Path.Combine(_sitesPath, siteName, "bin"));
            foreach (var d in subDirs)
            {
                var subDir = d.Split(Path.DirectorySeparatorChar).Last();
                var exe = new Executable(Path.Combine(_sitesPath, siteName, "bin"), subDir);
                
                if (exe.Exists())
                    yield return exe;
            }
        } 
    }

    public class Executable : IDisposable
    {
        private readonly string _basePath;
        private string _executionPath;
        private readonly string _exeName;
        private Process _process;
        private static volatile object _lockObject = new object();

        public Executable(string basePath, string exeName)
        {
            _basePath = basePath;
            _exeName = exeName;
        }

        public string GetOriginalDirPath()
        {
            return Path.Combine(_basePath, _exeName);
        }

        public string GetOriginalExePath()
        {
            return Path.Combine(GetOriginalDirPath(), string.Format("{0}.exe", _exeName));
        }

        public string GetExecutionDirPath()
        {
            return Path.Combine(_executionPath, _exeName);
        }

        public string GetExecutionExePath()
        {
            return Path.Combine(GetExecutionDirPath(), string.Format("{0}.exe", _exeName));
        }

        public bool Exists()
        {
            return File.Exists(GetOriginalExePath());
        }

        public void Copy(string executionPath)
        {
            if (IsRunning())
                throw new InvalidOperationException("The executable is already running!");

            _executionPath = executionPath;

            if (!Directory.Exists(GetExecutionDirPath()))
                Directory.CreateDirectory(GetExecutionDirPath());

            foreach (var f in Directory.GetFiles(GetOriginalDirPath(), "*.*", SearchOption.AllDirectories))
            {
                File.Copy(f, f.Replace(GetOriginalDirPath(), GetExecutionDirPath()));
            }

            var webConfigPath = Path.Combine(_basePath, "..", "web.config");
            if (File.Exists(webConfigPath))
            {
                File.Copy(webConfigPath, Path.Combine(GetExecutionDirPath(), "web.config"));
            }
        }

        public void Wait(TimeSpan maxWait)
        {
            if (_process != null)
                _process.WaitForExit(Convert.ToInt32(maxWait.TotalMilliseconds));
        }

        public void Execute()
        {
            if (IsRunning())
                throw new InvalidOperationException("The executable is already running!");

            if (string.IsNullOrEmpty(_executionPath))
                throw new InvalidOperationException("You must call .Copy() before .Execute()");

            var startInfo = new ProcessStartInfo(GetExecutionExePath())
            {
                WorkingDirectory = GetExecutionDirPath(),
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                ErrorDialog = false,
                UseShellExecute = false
            };

            _process = Start(startInfo);
        }

        public bool IsRunning()
        {
            return _process != null && !_process.HasExited;
        }

        public void Ping()
        {
            if (_process == null)
                return;

            if (IsRunning())
                return;

            if (_process.ExitCode != 0)
                _process.Start();
        }

        // http://social.msdn.microsoft.com/Forums/en/netfxbcl/thread/f6069441-4ab1-4299-ad6a-b8bb9ed36be3
        private Process Start(ProcessStartInfo startInfo)
        {
            Process process;

            lock (_lockObject)
            {
                using (new ChangeErrorMode(ErrorModes.FailCriticalErrors | ErrorModes.NoGpFaultErrorBox))
                {
                    process = Process.Start(startInfo);
                    process.PriorityClass = ProcessPriorityClass.Idle;
                }
            }

            return process;
        }

        public void Dispose()
        {
            if (_process != null)
            {
                if (IsRunning())
                    _process.Kill();
                _process.Dispose();
                _process = null;
            }

            if (!string.IsNullOrEmpty(_executionPath))
            {
                for (var i = 0; i <= 5; i++)
                {
                    try
                    {
                        Directory.Delete(GetExecutionDirPath(), true);
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(200));
                        if (i == 5)
                            throw;
                    }
                    catch(UnauthorizedAccessException)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(200));
                        if (i == 5)
                            throw;
                    }
                }
            }
        }

        [Flags]
        public enum ErrorModes
        {
            Default = 0x0,
            FailCriticalErrors = 0x1,
            NoGpFaultErrorBox = 0x2,
            NoAlignmentFaultExcept = 0x4,
            NoOpenFileErrorBox = 0x8000
        }

        public struct ChangeErrorMode : IDisposable
        {
            private readonly int _oldMode;

            public ChangeErrorMode(ErrorModes mode)
            {
                _oldMode = SetErrorMode((int)mode);
            }

            void IDisposable.Dispose() { SetErrorMode(_oldMode); }

            [DllImport("kernel32.dll")]
            private static extern int SetErrorMode(int newMode);
        }
    }
}
