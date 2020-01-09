using int32_t = System.Int32;
using uint32_t = System.UInt32;

namespace RyuCsharp
{
    // A floating decimal representing m * 10^e.
    struct floating_decimal_32
    {
        public uint32_t mantissa;
        // Decimal exponent's range is -45 to 38
        // inclusive, and can fit in a short if needed.
        public int32_t exponent;
    }
}