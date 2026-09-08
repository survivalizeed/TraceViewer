using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TraceViewer.Core.Dumpulator
{
    public class ExecutionContextInfo
    {
        public string DumpPath { get; set; } = "";
        public ulong CurrentIp { get; set; } = 0;
        public int CurrentRow { get; set; } = -1;
        public Dictionary<string, ulong> Registers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class DumpulatorRunner
    {
        private Process? _runningProcess;
        private readonly object _syncLock = new();
        private CancellationTokenSource? _cts;

        public event Action<string>? OutputReceived;
        public event Action<string>? ErrorReceived;
        public event Action<int>? ProcessExited;
        public event Action<string>? StatusChanged;

        public bool IsRunning
        {
            get
            {
                lock (_syncLock)
                {
                    return _runningProcess != null && !_runningProcess.HasExited;
                }
            }
        }

        public static string ResolvePythonPath()
        {
            // 1. Try python on PATH
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(1500);
                    if (proc.ExitCode == 0) return "python";
                }
            }
            catch
            {
                // Ignore and check known paths
            }

            // 2. Known standard install locations
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidatePaths =
            [
                Path.Combine(localAppData, "Programs", "Python", "Python313", "python.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python311", "python.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python310", "python.exe"),
                @"C:\Python313\python.exe",
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Program Files\Python313\python.exe",
                @"C:\Program Files\Python312\python.exe"
            ];

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path)) return path;
            }

            return "python";
        }

        public async Task<bool> RunScriptAsync(string userScript, ExecutionContextInfo context, string? customPythonPath = null)
        {
            if (IsRunning)
            {
                StatusChanged?.Invoke("A script is already running. Please stop it first.");
                return false;
            }

            string pythonExe = string.IsNullOrWhiteSpace(customPythonPath) ? ResolvePythonPath() : customPythonPath;

            // Generate preamble and script content
            var fullScript = new StringBuilder();
            fullScript.AppendLine("# ====================================================================");
            fullScript.AppendLine("# Injected by TraceViewer Dumpulator Bridge");
            fullScript.AppendLine("# ====================================================================");
            fullScript.AppendLine("import sys, os");
            fullScript.AppendLine($"DUMP_PATH = r\"{context.DumpPath.Replace("\"", "\\\"")}\"");
            fullScript.AppendLine($"CURRENT_IP = {context.CurrentIp}  # 0x{context.CurrentIp:X}");
            fullScript.AppendLine($"CURRENT_ROW = {context.CurrentRow}");
            fullScript.AppendLine("TRACE_REGS = {");
            foreach (var kv in context.Registers)
            {
                fullScript.AppendLine($"    '{kv.Key.ToLowerInvariant()}': {kv.Value},  # 0x{kv.Value:X}");
            }
            fullScript.AppendLine("}");
            fullScript.AppendLine("# ====================================================================");
            fullScript.AppendLine();
            fullScript.AppendLine(userScript);

            string tempDir = Path.Combine(Path.GetTempPath(), "TraceViewer", "Dumpulator");
            Directory.CreateDirectory(tempDir);
            string scriptPath = Path.Combine(tempDir, $"dumpulator_run_{DateTime.Now:yyyyMMdd_HHmmss}.py");

            await File.WriteAllTextAsync(scriptPath, fullScript.ToString(), Encoding.UTF8);

            string workingDir = !string.IsNullOrWhiteSpace(context.DumpPath) && File.Exists(context.DumpPath)
                ? Path.GetDirectoryName(context.DumpPath) ?? tempDir
                : tempDir;

            StatusChanged?.Invoke($"[Process Started] {Path.GetFileName(scriptPath)} via {Path.GetFileName(pythonExe)}");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"-u \"{scriptPath}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Set PYTHONUNBUFFERED=1 to ensure real-time log output
            startInfo.Environment["PYTHONUNBUFFERED"] = "1";

            _cts = new CancellationTokenSource();

            return await Task.Run(() =>
            {
                try
                {
                    lock (_syncLock)
                    {
                        _runningProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                    }

                    _runningProcess.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            OutputReceived?.Invoke(e.Data);
                        }
                    };

                    _runningProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            ErrorReceived?.Invoke(e.Data);
                        }
                    };

                    if (!_runningProcess.Start())
                    {
                        StatusChanged?.Invoke("[Error] Failed to start Python process.");
                        return false;
                    }

                    _runningProcess.BeginOutputReadLine();
                    _runningProcess.BeginErrorReadLine();

                    _runningProcess.WaitForExit();
                    int exitCode = _runningProcess.ExitCode;

                    lock (_syncLock)
                    {
                        _runningProcess = null;
                    }

                    StatusChanged?.Invoke($"[Process Exited] Exit code: {exitCode}");
                    ProcessExited?.Invoke(exitCode);
                    return exitCode == 0;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"[Execution Exception] {ex.Message}");
                    ErrorReceived?.Invoke(ex.ToString());
                    return false;
                }
                finally
                {
                    lock (_syncLock)
                    {
                        _runningProcess = null;
                    }
                }
            });
        }

        public void Stop()
        {
            lock (_syncLock)
            {
                if (_runningProcess != null && !_runningProcess.HasExited)
                {
                    try
                    {
                        StatusChanged?.Invoke("[Stopping] Terminating Python process tree...");
                        _runningProcess.Kill(entireProcessTree: true);
                        _runningProcess = null;
                        StatusChanged?.Invoke("[Process Terminated] Stopped by user.");
                    }
                    catch (Exception ex)
                    {
                        StatusChanged?.Invoke($"[Stop Error] {ex.Message}");
                    }
                }
            }
        }
    }
}
