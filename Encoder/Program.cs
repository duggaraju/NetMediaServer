using Microsoft.Extensions.Logging;
using Packager;

CancellationToken cancellationToken = default;
var factory = LoggerFactory.Create(builder => builder
    .SetMinimumLevel(LogLevel.Trace)
    .AddSimpleConsole(opts => opts.SingleLine = true));
var logger = factory.CreateLogger<Program>();

var usePipesForInput = OperatingSystem.IsWindows(); // Linux bug. NamedPipeServer in .net core uses Unix domain socket instead of named pipe and doesn't work with shaka.
var usePipesForOutput = false;

var input = new Uri(args[0], UriKind.Absolute);
var output = new Uri(args[1], UriKind.Absolute);
var outputDir = output.Scheme != Uri.UriSchemeFile ? Path.GetTempPath() : args[1];

var preset = await Presets.GetAdaptivePresetAsync(input, logger, cancellationToken);
var encoder = new Encoder(preset, logger);
var packager = new ShakaPackager(logger);

var videoCount = preset.Videos.Count;
var audioCount = preset.Audios.Count;
var streamCount = videoCount + audioCount;

var inputs = new List<PackagerInput>(streamCount);
var inputPipes = new List<LocalPipe>();
var outputs = new List<PackagerOutput>();
var outputPipes = new List<LocalPipe>();

if (usePipesForInput)
{
    inputPipes.AddRange(Enumerable.Range(0, streamCount).Select(layer => new LocalPipe()));
    inputs = new List<PackagerInput>(inputPipes.Select((p, i) => 
        new PackagerInput(p.PipePath,  i < videoCount ? TrackType.Video : TrackType.Audio, null)));
}
else
{
    inputs.AddRange(Enumerable.Range(0, videoCount).Select(i => new PackagerInput($"_video_{i}.mp4", TrackType.Video, null)));
    inputs.AddRange(Enumerable.Range(0, audioCount).Select(i => new PackagerInput($"_audio_{i}.mp4", TrackType.Audio, null)));
}

var outputFiles = new List<PackagerOutput>();
outputFiles.AddRange(Enumerable.Range(0, videoCount)
    .Select(i => $"video_{i}")
    .Select(f => new PackagerOutput(f + ".mp4", f + ".m3u8")));
outputFiles.AddRange(Enumerable.Range(0, audioCount)
    .Select(i => $"audio_{i}")
    .Select(f => new PackagerOutput(f + ".mp4", f + ".m3u8")));

if (usePipesForOutput)
{
    outputs.AddRange(outputFiles
        .Select(i => Tuple.Create(new LocalPipe(i.Path), new LocalPipe(i.ManifestPath)))
        .Select(item =>
        {
            var (media, manifest) = item;
            outputPipes.Add(media);
            outputPipes.Add(manifest);
            return new PackagerOutput(media.PipePath, manifest.PipePath);
        }));
}
else
{
    outputs.AddRange(outputFiles
        .Select(o => new PackagerOutput(Path.Combine(outputDir, o.Path), Path.Combine(outputDir, o.ManifestPath))));
}

var manifest = new PackagerOutput(
    Path.Combine(outputDir, "manifest.mpd"),
    Path.Combine(outputDir, "manifest.m3u8"));
outputs.Add(manifest);

var tasks = new List<Task>();
Task ffmpeg;
if (!usePipesForInput)
{
    var files = inputs.Select(i => i.Path).ToArray();
    ffmpeg = encoder.EncodeToFileAsync(input, files, cancellationToken);
    await ffmpeg;
}
else
{
    ffmpeg = encoder.EncodeToPipeAsync(input, inputPipes.Take(streamCount).ToArray(), cancellationToken);
    tasks.Add(ffmpeg);
}

var shaka = packager.RunAsync(inputs, outputs, manifest, CancellationToken.None)
    .ContinueWith(t => inputPipes.ForEach(p => p.Post()));
tasks.Add(shaka);

BlobUploader? uploader = null;
if (output.Scheme != Uri.UriSchemeFile)
{
    uploader = new BlobUploader(output, prefix: "/packager/");

    if (usePipesForOutput)
    {
        tasks.AddRange(
            outputPipes.Select(async pipe => await pipe.ProcessAsync(
                async stream => await uploader.UploadAsync(pipe.File, cancellationToken),
                cancellationToken)));
    }
}

await Task.WhenAll(tasks);

// upload files. Even when using pipes manifests are uploaded last.
if (uploader != null)
{
    await Task.WhenAll(outputs
        .SelectMany(o => new[] { o.Path, o.ManifestPath })
        .Select(o => uploader.UploadAsync(o, cancellationToken)));
}


