using System;

namespace MediaCommon
{
    public ref struct BitReader
    {
        private int _offset;
        private int _position;
        private readonly ReadOnlySpan<byte> _buffer;

        public BitReader(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0) throw new ArgumentException("Empty buffer", nameof(buffer));
            _position = 0;
            _offset = 0;
            _buffer = buffer;
        }

        public long ReadLong(int bits)
        {
            int value = 0;
            if (bits <= 0 || bits > 64) throw new ArgumentException("number of bits must be postive and less than 32", nameof(bits));
            while (bits > 0)
            {
                int currentByte = _buffer[_position];
                var bitsToRead = Math.Min(8 - _offset, bits);
                if (bitsToRead < 8)
                {
                    _offset = (_offset + bitsToRead) % 8;
                    if (_offset == 0)
                        ++_position;
                    currentByte &= (-1 << bitsToRead);
                }
                else
                {
                    ++_position;
                }
                value = value << 8 | currentByte;
                bits -= bitsToRead;
            }

            return 0;
        }

        public int Read(int bits)
        {
            if (bits <= 0 || bits > 32) throw new ArgumentException("number of bits must be postive and less than 32", nameof(bits));
            return (int)ReadLong(bits);
        }

        public int ReadGolomb()
        {
            int n;
            for (n = 0; Read(1) == 0; n++);
            return (1 << n) + Read(n) - 1;
        }

        public int Look(int v)
        {
            throw new NotImplementedException();
        }
    }
}
