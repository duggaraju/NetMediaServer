using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Packager
{
    internal class BlobUploader
    {
        private readonly BlobContainerClient _container;
        private readonly string _prefix;

        public BlobUploader(Uri uri, TokenCredential? credential = null, string? prefix = null)
        {
            _container = credential == null ? new BlobContainerClient(uri) : new BlobContainerClient(uri, credential);
            _prefix = prefix ?? string.Empty;
        }

        public async Task UploadAsync(string filepath, CancellationToken cancellationToken)
        {
            var filename = _prefix + Path.GetFileName(filepath);
            var blob = _container.GetBlockBlobClient(filename);
            using var content = File.OpenRead(filepath);
            await blob.UploadAsync(content, cancellationToken: cancellationToken);
        }

        public async Task UploadPipe(string filename, LocalPipe pipe, CancellationToken cancellationToken)
        {
            var blob = _container.GetBlockBlobClient(filename);
            await pipe.ProcessAsync(
                async stream => await blob.UploadAsync(stream, cancellationToken: cancellationToken),
                cancellationToken);
        }
    }
}
