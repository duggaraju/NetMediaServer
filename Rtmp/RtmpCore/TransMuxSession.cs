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
        private Process _process;

        public TransMuxSession(IOptions<ServerConfiguration> rtmpConfig, IOptions<TransMuxerConfiguration> muxConfig)
        {
            _rtmpConfig = rtmpConfig ?? throw new ArgumentNullException(nameof(rtmpConfig));
            _muxConfig = muxConfig ?? throw new ArgumentNullException(nameof(muxConfig));
        }

        public void Stop()
        {
            if (_process != null)
            {
                if (_process.HasExited)
                    _logger.LogInformation($"ffmpeg exited with status code: {_process.ExitCode}");
                else
                {
                    try
                    {
                        _process.Kill();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "failed to stop process");
                    }
                }
                _process = null;
            }
        }

        public void Start(RtmpEventArgs args, CancellationToken cancellationToken)
        {
            var config = _muxConfig.Value;
            _process = new Process();
            _process.StartInfo.FileName = config.Ffmpeg;
            _process.StartInfo.Arguments = $"{config.GlobalArguments} -i rtmp://localhost:{_rtmpConfig.Value.Port}{args.StreamPath} -c:v {config.VideoCodec} -c:a {config.AudioCodec} -f dash {config.DashParameters}  http://localhost:{config.HttpPort}{args.StreamPath}.mpd";
            _process.StartInfo.UseShellExecute = false;
            if (config.RedirectStdOut)
                _process.StartInfo.RedirectStandardOutput = true;
            if (config.RedirectStdErr)
                _process.StartInfo.RedirectStandardError = true;
            _process.EnableRaisingEvents = true;
            _logger.LogInformation($"Startin ffmpeg process {config.Ffmpeg} with command line: {_process.StartInfo.Arguments}");
            try
            {
                _process.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to start ffmpeg {0} {1}", config.Ffmpeg, _process.StartInfo.Arguments);
                throw;
            }

            if (config.RedirectStdOut)
                _ = ProcessStreamAsync(_process.StandardOutput.BaseStream, cancellationToken);

            if (config.RedirectStdErr)
                _ = ProcessStreamAsync(_process.StandardError.BaseStream, cancellationToken);


            _process.WaitForExit();
        }

        private async Task ProcessStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            var stdOutPipe = new Pipe();
            var readTask = ReadFromStream(stdOutPipe.Writer, stream, cancellationToken);
            var processTask = ProcessLogs(stdOutPipe.Reader, cancellationToken);
            await Task.WhenAll(readTask, processTask);
        }

        private async Task ProcessLogs(PipeReader reader, CancellationToken cancellationToken, bool stdOut = true)
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                try
                {
                    var position = result.Buffer.PositionOf((byte)'\r');
                    if (position != null)
                    {
                        var line =  GetString(result.Buffer.Slice(0, position.Value));
                        if (stdOut)
                            _logger.LogDebug("Ffmpeg out: {0}", line);
                        else
                            _logger.LogError("Ffmpeg err: {0}", line);
                        reader.AdvanceTo(position.Value);
                    }
                    else
                    {
                        reader.AdvanceTo(result.Buffer.GetPosition(0));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read from pipe");
                    break;
                }

                if (result.IsCompleted)
                    break;
            }

        }

        private static string GetString(ReadOnlySequence<byte> buffer)
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
                var result = await writer.FlushAsync();
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
