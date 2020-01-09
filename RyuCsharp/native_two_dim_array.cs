using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuCsharp
{
    unsafe class native_two_dim_array<T> where T : unmanaged
    {
        readonly T[,] context;
        GCHandle gch;

        native_two_dim_array(T[,] value)
        {
            context = value;
            gch = GCHandle.Alloc(value, GCHandleType.Pinned);
        }
        ~native_two_dim_array()
        {
            gch.Free();
        }

        public T* this[uint index] => (T*)Unsafe.AsPointer(ref context[index, 0]);


        public static implicit operator native_two_dim_array<T>(T[,] value) => new native_two_dim_array<T>(value);
    }
}