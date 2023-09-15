using System.IO.Pipes;
using FFMpegCore.Arguments;
using FFMpegCore.Pipes;

namespace Packager
{
    internal class LocalPipe : PipeArgument, IPipeSink
    {
        public LocalPipe(string? file = null) : base(PipeDirection.Out)
        {
            File = file ?? string.Empty;
            Pre();
        }

        public string File { get; }

        public new string PipePath => OperatingSystem.IsWindows() ? base.PipePath : base.PipePath.Substring(5);

        public override string Text => $"\"{PipePath}\" -y";

        public string GetFormat() => "mp4";

        public async Task ReadAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            await Pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (!Pipe.IsConnected)
            {
                throw new TaskCanceledException();
            }

            await inputStream.CopyToAsync(Pipe, cancellationToken).ConfigureAwait(false);
            Pipe.Close();
        }

        public async Task ProcessAsync(Func<Stream, Task> action, CancellationToken cancellationToken)
        {
            await Pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (!Pipe.IsConnected)
            {
                throw new TaskCanceledException();
            }

            await action(Pipe).ConfigureAwait(false);
        }

        protected override Task ProcessDataAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
