using int32_t = System.Int32;
using uint64_t = System.UInt64;

namespace RyuCsharp
{
    // A floating decimal representing m * 10^e.
    struct floating_decimal_64
    {
        public uint64_t mantissa;
        // Decimal exponent's range is -324 to 308
        // inclusive, and can fit in a short if needed.
        public int32_t exponent;
    }
}