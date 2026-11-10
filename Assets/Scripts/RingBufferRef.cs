using System;
using System.Runtime.CompilerServices;

namespace DefaultNamespace
{
    /// <summary>
    /// Reference-type adapter over the <see cref="RingBuffer{T}"/> struct,
    /// so that all holders share the same buffer state (indices, count).
    /// </summary>
    public sealed class RingBufferRef<T> : IDisposable
        where T : struct
    {
        // Must stay a non-readonly field, otherwise calls would operate on a copy.
        private RingBuffer<T> _buffer;

        public RingBufferRef(uint size)
        {
            _buffer = new RingBuffer<T>(size);
        }

        public uint Count => _buffer.Count;

        public ref readonly T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _buffer[index];
        }

        public void Append(T value) => _buffer.Append(value);

        public void Clear() => _buffer.Clear();

        public void Dispose() => _buffer.Dispose();
    }
}
