using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static RtmpCore.RtmpContext;

namespace RtmpCore
{
    public class TransMuxSession
    {
        private readonly ILogger _logger = RtmpLogging.LoggerFactory.CreateLogger<TransMuxSession>();
        private readonly IOptions<ServerConfiguration> _rtmpConfig;
        private readonly IOptions<TransMuxerConfiguration> _muxConfig;

        public TransMuxSession(IOptions<ServerConfiguration> rtmpConfig, IOptions<TransMuxerConfiguration> muxConfig)
        {
            _rtmpConfig = rtmpConfig ?? throw new ArgumentNullException(nameof(rtmpConfig));
            _muxConfig = muxConfig ?? throw new ArgumentNullException(nameof(muxConfig));
        }


        public Task RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource<int>();
            var process = new Process
            {
                StartInfo = startInfo
            };
            process.EnableRaisingEvents = true;
            cancellationToken.Register(() =>
            {
                process.Kill();
                process.Dispose();
                source.TrySetCanceled();
            });
            process.Exited += (sender, args) =>
            {
                source.SetResult(process.ExitCode);
            };
            return source.Task;
        }

        public async Task StartAsync(RtmpEventArgs args, CancellationToken cancellationToken)
        {
            var pipe = new Pipe();
            var config = _muxConfig.Value;
            using var process = new Process();
            process.StartInfo.FileName = config.Ffmpeg;
            process.StartInfo.Arguments = $"{config.GloblArguments}-i rtmp://localhost:{_rtmpConfig.Value.Port}{args.StreamPath} -c:v {config.VideoCodec} -c:a {config.AudioCodec} -f dash {config.DashParameters}  http://localhost:5000{args.StreamPath}.mpd";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.EnableRaisingEvents = true;
            process.Start();

            var readTask = ReadFromStream(pipe.Writer, process.StandardOutput.BaseStream, cancellationToken);
            var processTask = ProcessLogs(pipe.Reader, cancellationToken);
            await Task.WhenAll(readTask, processTask);
            _logger.LogInformation($"ffmpeg exited with status code: {process.ExitCode}");
        }

        private async Task ProcessLogs(PipeReader reader, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                try
                {
                    var position = result.Buffer.PositionOf((byte)'\n');
                    if (position != null)
                    {
                        var line =  GetString(result.Buffer.Slice(0, position.Value));
                        _logger.LogWarning("Transmux: {0}", line);
                        reader.AdvanceTo(position.Value);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse chunks");
                    break;
                }

                if (result.IsCompleted)
                    break;
            }

        }

        private string GetString(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                return Encoding.UTF8.GetString(buffer.First.Span);
            }

            return string.Create((int)buffer.Length, buffer, (span, sequence) =>
            {
                foreach (var segment in sequence)
                {
                    Encoding.UTF8.GetChars(segment.Span, span);

                    span = span.Slice(segment.Length);
                }
            });
        }

        private async Task ReadFromStream(PipeWriter writer, Stream stream, CancellationToken cancellationToken)
        {
            const int minimumBufferSize = 128;

            while (true)
            {
                var memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    var bytesRead = await stream.ReadAsync(memory, cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Network read failed");
                    break;
                }

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Tell the PipeReader that there's no more data coming
            writer.Complete();
        }
    }
}
