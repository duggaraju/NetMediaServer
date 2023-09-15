
using FFMpegCore;
using FFMpegCore.Arguments;
using Microsoft.Extensions.Logging;

namespace Packager
{
    internal class Encoder
    {
        private readonly ILogger _logger;
        private readonly Preset _preset;

        public Encoder(Preset preset, ILogger logger)
        {
            _logger = logger;
            _preset = preset;
        }

        public async Task EncodeToFileAsync(Uri uri, IList<string> outputs, CancellationToken cancellationToken)
        {
            var ffargs = uri.Scheme == Uri.UriSchemeFile ? FFMpegArguments.FromFileInput(uri.OriginalString, true) : FFMpegArguments.FromUrlInput(uri);
            await EncodeAsync(EncodeToFile(ffargs, outputs), cancellationToken);
        }

        public async Task EncodeToPipeAsync(Uri uri, IList<LocalPipe> outputs, CancellationToken cancellationToken)
        {
            var ffargs = uri.Scheme == Uri.UriSchemeFile ? FFMpegArguments.FromFileInput(uri.LocalPath, true) : FFMpegArguments.FromUrlInput(uri);
            await EncodeAsync(EncodeToPipe(ffargs, outputs), cancellationToken);
        }

        public async Task EncodeAsync(FFMpegArgumentProcessor processor, CancellationToken cancellationToken)
        {
            _logger.LogInformation("running ffmpeg {args}", processor.Arguments);
            processor
                .NotifyOnError(s => _logger.LogInformation("Ffmpeg: {s}", s));

            await processor
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(throwOnError: true);
        }

        private FFMpegArgumentProcessor EncodeToFile(FFMpegArguments args, IList<string> outputs)
        {
            var audio = _preset.Audios.Last();
            var output = outputs.Last();
            return args.OutputToFile(output, overwrite: true, options =>
            {
                foreach (var (layer, output) in _preset.Videos.Zip(outputs.Take(_preset.Videos.Count)))
                {
                    options.AddVideo(layer);
                    options.WithArgument(new OutputArgument(output, overwrite: true));
                }

                options.AddAudio(audio);
            });
        }

        private FFMpegArgumentProcessor EncodeToPipe(FFMpegArguments args, IList<LocalPipe> outputs)
        {
            var audio = _preset.Audios.Last();
            var output = outputs.Last();

            return args.OutputToPipe(output, options =>
            {
                foreach (var (layer, output) in _preset.Videos.Zip(outputs.Take(_preset.Videos.Count)))
                {
                    options.AddVideo(layer);
                    options.WithArgument(new OutputPipeArgument(output));
                }

                options.AddAudio(audio);
                foreach (var (layer, output) in _preset.Audios.Zip(outputs.Skip(_preset.Videos.Count)))
                {
                    // options.AddAudio(layer);
                    // options.WithArgument(new OutputPipeArgument(output));
                }
            });
        }
    }
}
