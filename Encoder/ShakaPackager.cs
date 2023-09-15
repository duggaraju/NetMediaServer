
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Packager
{
    public enum TrackType
    {
        Audio,
        Video,
        Text
    }

    public record PackagerOptions(
        string WorkingDirectory,
        bool EncryptContent,
        string? LicenseUrl,
        string? KeyId,
        string? EncryptionKey);

    public record PackagerInput(string Path, TrackType Type, string? Language);

    public record PackagerOutput(string Path, string ManifestPath);

    internal class ShakaPackager
    {
        public static readonly string PackagerPath = AppContext.BaseDirectory;
        public static readonly string Packager = Path.Combine(PackagerPath, GetExecutableName());
        private readonly PackagerOptions _options = new PackagerOptions(Environment.CurrentDirectory, false, null, null, null);
        private readonly ILogger _logger;
        private readonly TaskCompletionSource _taskCompletionSource = new TaskCompletionSource();

        public static string GetExecutableName()
        {
            var prefix = "packager";
            var suffix = OperatingSystem.IsLinux() ? "-linux-x64" : OperatingSystem.IsMacOS() ? "-osx-x64" : "-win-x64.exe";
            return prefix + suffix;
        }

        public ShakaPackager(ILogger logger)
        {
            _logger = logger;
        }

        private IEnumerable<string> GetArguments(IList<PackagerInput> inputs, IList<PackagerOutput> outputs, PackagerOutput manifest)
        {
            const string EncryptionLabel = "cenc";
            var drm_label = _options.EncryptContent ? $",drm_label={EncryptionLabel}" : string.Empty;
            var arguments = inputs
                .Select((item, i) =>
                {
                    var stream = item.Type.ToString().ToLowerInvariant();
                    var language = string.IsNullOrEmpty(item.Language) || item.Language == "und" ? string.Empty : $",language={item.Language}";
                    var encryption = item.Type == TrackType.Text ? string.Empty : drm_label;
                    return $"stream={stream},in={item.Path},format=mp4,out={outputs[i].Path},playlist_name={outputs[i].ManifestPath}{language}{encryption}";
                }).ToList();

            if (_options.EncryptContent)
            {
                arguments.Add("--enable_raw_key_encryption");
                arguments.Add("--protection_scheme");
                arguments.Add("cbcs");
                arguments.Add("--keys");
                arguments.Add($"label={EncryptionLabel}:key_id={_options.KeyId}:key={_options.EncryptionKey}");
                arguments.Add("--hls_key_uri");
                arguments.Add(_options.LicenseUrl!);
                arguments.Add("--clear_lead");
                arguments.Add("0");
            }

            var usePipe = true;
            if (usePipe)
            {
                arguments.Add("--io_block_size");
                arguments.Add("16656");
            }

            var vlog = 0;
            arguments.Add($"--vmodule=*={vlog}");
            arguments.Add("--temp_dir");
            arguments.Add(_options.WorkingDirectory);

            arguments.Add("--mpd_output");
            arguments.Add(manifest.Path);

            arguments.Add("--hls_master_playlist_output");
            arguments.Add(manifest.ManifestPath);
            return arguments;
        }

        private static string Escape(string arg) => arg.Contains(' ') ? $"\"{arg}\"" : arg;

        protected Process StartProcess(
            string command,
            IEnumerable<string> arguments,
            Action<int> onExit,
            Action<string?> stdOut,
            Action<string?> stdError)
        {
            var argumentString = string.Join(" ", arguments.Select(Escape));
            _logger.LogDebug("Starting packager {command} arguments: {args}", command, argumentString);
            var processStartInfo = new ProcessStartInfo(command, argumentString)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (s, args) => stdOut(args.Data);
            process.ErrorDataReceived += (s, args) => stdError(args.Data);
            process.Exited += (s, args) =>
            {
                if (process.ExitCode != 0)
                {
                    _logger.LogError("Packager {command} finished with exit code {code}", command, process.ExitCode);
                }

                onExit(process.ExitCode);
                process.Dispose();
            };
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return process;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start process {command} with error: {ex}", command, ex);
                throw;
            }
        }

        public Task RunAsync(IList<PackagerInput> inputs, IList<PackagerOutput> outputs, PackagerOutput manifest, CancellationToken cancellationToken)
        {
            var arguments = GetArguments(inputs, outputs, manifest);
            var process = StartProcess(Packager, arguments,
                exit =>
                {
                    if (exit == 0)
                    {
                        _taskCompletionSource.SetResult();
                    }
                    else
                    {
                        _logger.LogError("Shaka packager failed with error code {code}", exit);
                        _taskCompletionSource.SetException(new Win32Exception(exit, $"{Packager} failed"));
                    }
                },
                s => { },
                LogProcessOutput);
            cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception)
                {
                }
            });

            return _taskCompletionSource.Task;
        }

        public const string ShakaLogPattern = @"\d+/\d+:(?<level>\w+):";
        public static readonly Regex ShakaLogRegEx = new(ShakaLogPattern, RegexOptions.Compiled);

        public static LogLevel GetLogLevel(string level)
        {
            return level switch
            {
                "FATAL" => LogLevel.Critical,
                "ERROR" => LogLevel.Error,
                "INFO" => LogLevel.Trace,
                "WARN" => LogLevel.Warning,
                "VERBOSE1" => LogLevel.Trace,
                "VERBOSE2" => LogLevel.Trace,
                _ => LogLevel.Information
            };
        }

        public static LogLevel GetLineLogLevel(string line)
        {
            var match = ShakaLogRegEx.Match(line);
            var group = match.Groups["level"];
            return match.Success && group.Success ? GetLogLevel(group.Value) : LogLevel.Information;
        }

        private void LogProcessOutput(string? line)
        {
            if (line != null)
            {
                _logger.Log(GetLineLogLevel(line), 0, line);
            }
        }
    }
}
