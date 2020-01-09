using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuCsharp
{
    unsafe class native_one_dim_array<T> where T : unmanaged
    {
        readonly T[] context;
        GCHandle gch;
        native_one_dim_array(T[] value)
        {
            context = value;
            gch = GCHandle.Alloc(value, GCHandleType.Pinned);
        }

        ~native_one_dim_array()
        {
            gch.Free();
        }
        public T this[uint index] => context[index];

        public static T* operator +(native_one_dim_array<T> left, uint right)
        {
            return (T*)Unsafe.AsPointer(ref left.context[right]);
        }

        public static implicit operator native_one_dim_array<T>(T[] value) => new native_one_dim_array<T>(value);
    }
}