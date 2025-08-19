using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PMM_Windows_Helper
{
    public class WingetInstallerService
    {
        public class InstallResult
        {
            public string WingetId { get; set; }
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public bool Success => ExitCode == 0;
        }

        public async Task<string> GetLatestVersionAsync(string wingetId, CancellationToken ct = default(CancellationToken))
        {
            // Cách 1: winget show --id ... --exact
            var (code, outp) = await RunAsync($"show --id {wingetId} --exact", ct);

            if (code != 0 || string.IsNullOrWhiteSpace(outp))
                return "—";

            // Parse “Version” hoặc “Latest Version”
            // Ví dụ dòng: "Version: 1.2.3" hoặc "Latest Version: 1.2.3"
            var m = Regex.Match(outp, @"(?mi)^(?:Latest\s+Version|Version)\s*:\s*(.+)$");
            if (m.Success) return m.Groups[1].Value.Trim();

            // Cách 2 (dự phòng): winget search --id ...
            var (code2, outp2) = await RunAsync($"search --id {wingetId} --exact", ct);
            var m2 = Regex.Match(outp2 ?? "", @"(?mi)^\s*" + Regex.Escape(wingetId) + @"\s+([0-9][\w\.\-]+)\s", RegexOptions.Multiline);
            if (m2.Success) return m2.Groups[1].Value.Trim();

            return "—";
        }

        public async IAsyncEnumerable<InstallResult> InstallSelectedAsync(IEnumerable<string> wingetIds, IProgress<string> log, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default(CancellationToken))
        {
            foreach (var id in wingetIds)
            {
                ct.ThrowIfCancellationRequested();
                log?.Report($"[INFO] Installing {id} ...");

                var args = $"install --id {id} --silent --accept-package-agreements --accept-source-agreements";
                var (exit, output) = await RunAsync(args, ct);

                log?.Report($"[INFO] {id} ExitCode={exit}");
                if (!string.IsNullOrEmpty(output))
                    log?.Report(output);

                yield return new InstallResult { WingetId = id, ExitCode = exit, Output = output };
            }
        }

        public async Task<(int exitCode, string output)> RunAsync(string wingetArgs, CancellationToken ct = default(CancellationToken))
        {
            // Kiểm tra winget tồn tại
            try
            {
                var ver = await ExecAsync("winget", "-v", ct);
                if (ver.exitCode != 0) return (ver.exitCode, "winget not available");
            }
            catch
            {
                return (-1, "winget not available");
            }

            return await ExecAsync("winget", wingetArgs, ct);
        }

        private async Task<(int exitCode, string output)> ExecAsync(string file, string args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var sb = new StringBuilder();
            using (var p = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                var tcs = new TaskCompletionSource<int>();
                p.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };

                p.Exited += (s, e) => tcs.TrySetResult(p.ExitCode);

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                using (ct.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } }))
                {
                    var exit = await tcs.Task;
                    return (exit, sb.ToString());
                }
            }
        }
    }
}
