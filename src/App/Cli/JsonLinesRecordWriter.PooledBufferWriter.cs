using System.Buffers;

namespace DataMorph.App.Cli;

internal partial struct JsonLinesRecordWriter
{
    private sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[]? _buffer;
        private int _position;

        public PooledBufferWriter(int initialSize)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(initialSize);
        }

        public ReadOnlyMemory<byte> WrittenMemory
        {
            get
            {
                ObjectDisposedException.ThrowIf(_buffer is null, typeof(PooledBufferWriter));
                return _buffer.AsMemory(0, _position);
            }
        }

        public void Clear() => _position = 0;

        public void Advance(int count) => _position += count;

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_position);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_position);
        }

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_buffer))]
        private void EnsureCapacity(int sizeHint)
        {
            ObjectDisposedException.ThrowIf(_buffer is null, typeof(PooledBufferWriter));

            var required = _position + Math.Max(sizeHint, 1);
            if (required > _buffer.Length)
            {
                var newSize = Math.Max(_buffer.Length * 2, required);
                var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
                Array.Copy(_buffer, 0, newBuffer, 0, _position);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = newBuffer;
            }
        }

        public void Dispose()
        {
            if (_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }
        }
    }
}
