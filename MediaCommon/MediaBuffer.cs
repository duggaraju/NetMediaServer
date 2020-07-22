using System;
using System.Buffers;

namespace MediaCommon
{
    public class MediaBuffer
    {
        private readonly IMemoryOwner<byte> _buffer;

        public int Length { get; }

        public ReadOnlyMemory<byte> Memory => Length == 0 ? _buffer.Memory : _buffer.Memory.Slice(0, Length);

        public MediaBuffer(IMemoryOwner<byte> buffer, int length)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Length = length;
        }

        public void Release()
        {
            _buffer.Dispose();
        }

    }
}
