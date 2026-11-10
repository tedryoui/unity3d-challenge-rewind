using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace DefaultNamespace
{
    public struct RingBuffer<T> : IDisposable
        where T : struct
    {
        private uint _tailIndex;
        private uint _headIndex;

        private uint _size;
        private uint _count;

        private NativeArray<T> _buffer;
        
        public uint Count => _count;
        
        public RingBuffer(uint size)
        {
            _size  = size;
            _count = 0;

            _tailIndex = 0;
            _headIndex = 0;
            
            _buffer = new NativeArray<T>((int)_size, Allocator.Persistent);
        }

        public ref readonly T this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (index >= _count)
                    ThrowIndexOutOfRangeException(index);
                
                return ref _buffer.AsReadOnlySpan()[(int)EvaluateIndex(index)];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint EvaluateIndex(uint index)
        {
            uint pureIndex = _headIndex + index;
            return pureIndex >= _size ? pureIndex - _size : pureIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(T value)
        {
            _buffer[(int)_tailIndex] = value;

            _tailIndex = _tailIndex + 1 >= _size ? 0 : _tailIndex + 1;

            if (_count == _size)
            {
                _headIndex = _tailIndex;
            }
            else
            {
                _count++;
            }
        }
        
        public void Clear()
        {
            _headIndex = 0;
            _tailIndex = 0;
            _count     = 0;
        }

        public void Dispose()
        {
            if (_buffer.IsCreated)
                _buffer.Dispose();
        }

        private static void ThrowIndexOutOfRangeException(uint index)
        {
            throw new IndexOutOfRangeException($"Wrong index passed - { index }!");
        }
    }
}