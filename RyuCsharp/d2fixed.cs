using System.Diagnostics;
using System;
using System.Runtime.CompilerServices;
using System.Numerics;

using uint8_t = System.Byte;
using int32_t = System.Int32;
using uint16_t = System.UInt16;
using uint32_t = System.UInt32;
using int64_t = System.Int64;
using uint64_t = System.UInt64;
namespace RyuCsharp
{
    partial class Ryu
    {
        const int POW10_ADDITIONAL_BITS = 120;

        // Returns the low 64 bits of the high 128 bits of the 256-bit product of a and b.
        static uint64_t umul256_hi128_lo64(uint64_t aHi, uint64_t aLo, uint64_t bHi, uint64_t bLo)
        {
            uint64_t b00Lo = umul128(aLo, bLo, out uint64_t b00Hi);
            uint64_t b01Lo = umul128(aLo, bHi, out uint64_t b01Hi);
            uint64_t b10Lo = umul128(aHi, bLo, out uint64_t b10Hi);
            uint64_t b11Lo = umul128(aHi, bHi, out uint64_t b11Hi);
            uint64_t temp1Lo = b10Lo + b00Hi;
            uint64_t temp1Hi = b10Hi;
            if (temp1Lo < b10Lo)
                ++temp1Hi;
            uint64_t temp2Lo = b01Lo + temp1Lo;
            uint64_t temp2Hi = b01Hi;
            if (temp2Lo < b01Lo)
                ++temp2Hi;
            return b11Lo + temp1Hi + temp2Hi;
        }

        static uint32_t uint128_mod1e9(uint64_t vHi, uint64_t vLo)
        {
            // After multiplying, we're going to shift right by 29, then truncate to uint32_t.
            // This means that we need only 29 + 32 = 61 bits, so we can truncate to uint64_t before shifting.
            uint64_t multiplied = umul256_hi128_lo64(vHi, vLo, 0x89705F4136B4A597u, 0x31680A88F8953031u);

            // For uint32_t truncation, see the mod1e9() comment in d2s_intrinsics.h.
            uint32_t shifted = (uint32_t)(multiplied >> 29);

            return ((uint32_t)vLo) - 1000000000u * shifted;
        }

        static uint32_t mulShift_mod1e9(uint64_t m,in (uint64_t offset0, uint64_t offset1, uint64_t offset2) mul, int32_t j)
        {
            uint64_t high0;                                   // 64
            uint64_t low0 = umul128(m, mul.offset0, out high0); // 0
            uint64_t high1;                                   // 128
            uint64_t low1 = umul128(m, mul.offset1, out high1); // 64
            uint64_t high2;                                   // 192
            uint64_t low2 = umul128(m, mul.offset2, out high2); // 128
            uint64_t s0low = low0;              // 0
            uint64_t s0high = low1 + high0;     // 64
            uint32_t c1 = s0high < low1 ? 1U : 0;
            uint64_t s1low = low2 + high1 + c1; // 128
            uint32_t c2 = s1low < low2 ? 1U : 0; // high1 + c1 can't overflow, so compare against low2
            uint64_t s1high = high2 + c2;       // 192

            Debug.Assert(j >= 128);
            Debug.Assert(j <= 180);
            uint32_t dist = (uint32_t)(j - 128); // dist: [0, 52]
            uint64_t shiftedhigh = s1high >> (int)dist;
            uint64_t shiftedlow = shiftright128(s1low, s1high, dist);
            return uint128_mod1e9(shiftedhigh, shiftedlow);
        }

        static void append_n_digits(int32_t olength, uint32_t digits, Span<char> result)
        {

            int32_t i = 0;
            while (digits >= 10000)
            {
                digits = (uint32_t)Math.DivRem((int32_t)digits, 10000, out int32_t c);
                int32_t c1 = (Math.DivRem(c, 100, out int32_t c0)) << 1;
                c0 <<= 1;
                DIGIT_TABLE.AsSpan(c0, 2).CopyTo(result.Slice(olength - i - 2));
                DIGIT_TABLE.AsSpan(c1, 2).CopyTo(result.Slice(olength - i - 4));
                i += 4;
            }
            if (digits >= 100)
            {
                digits = (uint32_t)Math.DivRem((int32_t)digits, 100, out int32_t c);
                c <<= 1;
                DIGIT_TABLE.AsSpan(c, 2).CopyTo(result.Slice(olength - i - 2));
                i += 2;
            }
            if (digits >= 10)
            {
                int32_t c = (int32_t)(digits << 1);
                DIGIT_TABLE.AsSpan(c, 2).CopyTo(result.Slice(olength - i - 2));
            }
            else
            {
                result[0] = (char)('0' + digits);
            }
        }

        static void append_d_digits(int32_t olength, uint32_t digits, Span<char> result)
        {
            int32_t i = 0;
            while (digits >= 10000)
            {
                digits = (uint32_t)Math.DivRem((int32_t)digits, 10000, out int32_t c);
                int32_t c1 = Math.DivRem(c, 100, out int32_t c0) << 1;
                c0 <<= 1;
                DIGIT_TABLE.AsSpan(c0, 2).CopyTo(result.Slice(olength + 1 - i - 2));
                DIGIT_TABLE.AsSpan(c1, 2).CopyTo(result.Slice(olength + 1 - i - 4));
                i += 4;
            }
            if (digits >= 100)
            {
                digits = (uint32_t)Math.DivRem((int32_t)digits, 100, out int32_t c);
                c <<= 1;
                DIGIT_TABLE.AsSpan(c, 2).CopyTo(result.Slice(olength + 1 - i - 2));
                i += 2;
            }
            if (digits >= 10)
            {
                uint32_t c = digits << 1;
                result[2] = DIGIT_TABLE[c + 1];
                result[1] = '.';
                result[0] = DIGIT_TABLE[c];
            }
            else
            {
                result[1] = '.';
                result[0] = (char)('0' + digits);
            }
        }

        static void append_c_digits(int32_t count, uint32_t digits, Span<char> result)
        {
            int32_t i = 0;
            for (; i < count - 1; i += 2)
            {
                digits = (uint)Math.DivRem((int32_t)digits, 100, out int32_t rem);
                int32_t c = rem << 1;
                DIGIT_TABLE.AsSpan(c, 2).CopyTo(result.Slice(count - i - 2));
            }

            if (i < count)
            {
                result[count - i - 1] = (char)('0' + (digits % 10));
            }
        }

        static void append_nine_digits(uint32_t digits, Span<char> result)
        {
            if (digits == 0)
            {
                result.Slice(0, 9).Fill('0');
                return;
            }

            for (int32_t i = 0; i < 5; i += 4)
            {
                digits = (uint)Math.DivRem((int)digits, 10000, out int32_t c);
                int32_t c1 = Math.DivRem(c, 100, out int rem) << 1;
                int32_t c0 = rem << 1;
                DIGIT_TABLE.AsSpan(c0, 2).CopyTo(result.Slice(7 - i));
                DIGIT_TABLE.AsSpan(c1, 2).CopyTo(result.Slice(5 - i));
            }
            result[0] = (char)('0' + digits);
        }

        static uint32_t indexForExponent(uint32_t e)
        {
            return (e + 15) / 16;
        }

        static uint32_t pow10BitsForIndex(uint32_t idx)
        {
            return 16 * idx + POW10_ADDITIONAL_BITS;
        }

        static uint32_t lengthForIndex(uint32_t idx)
        {
            // +1 for ceil, +16 for mantissa, +8 to round up when dividing by 9
            return (log10Pow2(16 * (int32_t)idx) + 1 + 16 + 8) / 9;
        }

        static int32_t copy_special_str_printf(Span<char> result, bool sign, uint64_t mantissa)
        {
            if (mantissa != 0)
            {
                "nan".AsSpan().CopyTo(result);
                return 3;
            }
            if (sign)
            {
                result[0] = '-';
            }
            "Infinity".AsSpan().CopyTo(result.Slice((sign ? 1 : 0)));
            return (sign ? 1 : 0) + 8;
        }

        public static int d2fixed_buffered_n(double d, int32_t precision, Span<char> result)
        {
            if (precision < 0)
                throw new ArgumentException(nameof(precision));

            (bool ieeeSign, uint64_t ieeeMantissa, uint32_t ieeeExponent) = Parse(d);

            // Case distinction; exit early for the easy cases.
            if (ieeeExponent == ((1u << DOUBLE_EXPONENT_BITS) - 1u))
            {
                return copy_special_str_printf(result, ieeeSign, ieeeMantissa);
            }
            if (ieeeExponent == 0 && ieeeMantissa == 0)
            {
                int index2 = 0;
                if (ieeeSign)
                {
                    result[index2++] = '-';
                }
                result[index2++] = '0';
                if (precision > 0)
                {
                    result[index2++] = '.';
                    result.Slice(index2, precision).Fill('0');
                    index2 += precision;
                }
                return index2;
            }

            int32_t e2;
            uint64_t m2;
            if (ieeeExponent == 0)
            {
                e2 = 1 - DOUBLE_BIAS - DOUBLE_MANTISSA_BITS;
                m2 = ieeeMantissa;
            }
            else
            {
                e2 = (int32_t)ieeeExponent - DOUBLE_BIAS - DOUBLE_MANTISSA_BITS;
                m2 = (1ul << DOUBLE_MANTISSA_BITS) | ieeeMantissa;
            }


            int index = 0;
            bool nonzero = false;
            if (ieeeSign)
            {
                result[index++] = '-';
            }
            if (e2 >= -52)
            {
                uint32_t idx = e2 < 0 ? 0 : indexForExponent((uint32_t)e2);
                uint32_t p10bits = pow10BitsForIndex(idx);
                int32_t len = (int32_t)lengthForIndex(idx);

                for (int32_t i = len - 1; i >= 0; --i)
                {
                    uint32_t j = (uint32_t)(p10bits - e2);
                    // Temporary: j is usually around 128, and by shifting a bit, we push it to 128 or above, which is
                    // a slightly faster code path in mulShift_mod1e9. Instead, we can just increase the multipliers.
                    uint32_t digits = mulShift_mod1e9(m2 << 8, POW10_SPLIT[(uint32_t)(POW10_OFFSET[idx] + i)], (int32_t)(j + 8));
                    if (nonzero)
                    {
                        append_nine_digits(digits, result.Slice(index));
                        index += 9;
                    }
                    else if (digits != 0)
                    {
                        int32_t olength = decimalLength9(digits);
                        append_n_digits(olength, digits, result.Slice(index));
                        index += olength;
                        nonzero = true;
                    }
                }
            }
            if (!nonzero)
            {
                result[index++] = '0';
            }
            if (precision > 0)
            {
                result[index++] = '.';
            }
            if (e2 < 0)
            {
                int32_t idx = -e2 / 16;

                uint32_t blocks = ((uint32_t)precision / 9) + 1;
                // 0 = don't round up; 1 = round up unconditionally; 2 = round up if odd.
                int roundUp = 0;
                uint32_t i = 0;
                if (blocks <= MIN_BLOCK_2[(uint)idx])
                {
                    i = blocks;
                    result.Slice(index, precision).Fill('0');
                    index += precision;
                }
                else if (i < MIN_BLOCK_2[(uint)idx])
                {
                    i = MIN_BLOCK_2[(uint32_t)idx];
                    result.Slice(index, (int32_t)(9 * i)).Fill('0');
                    index += (int32_t)(9 * i);
                }
                for (; i < blocks; ++i)
                {
                    int32_t j = ADDITIONAL_BITS_2 + (-e2 - 16 * idx);
                    uint32_t p = POW10_OFFSET_2[(uint)idx] + i - MIN_BLOCK_2[(uint)idx];
                    if (p >= POW10_OFFSET_2[(uint)idx + 1])
                    {
                        // If the remaining digits are all 0, then we might as well use memset.
                        // No rounding required in this case.
                        int32_t fill = precision - (int32_t)(9 * i);
                        result.Slice(index, fill).Fill('0');
                        index += fill;
                        break;
                    }
                    // Temporary: j is usually around 128, and by shifting a bit, we push it to 128 or above, which is
                    // a slightly faster code path in mulShift_mod1e9. Instead, we can just increase the multipliers.
                    uint32_t digits = mulShift_mod1e9(m2 << 8, POW10_SPLIT_2[p], j + 8);

                    if (i < blocks - 1)
                    {
                        append_nine_digits(digits, result.Slice(index));
                        index += 9;
                    }
                    else
                    {
                        uint32_t maximum = (uint32_t)(precision - (int32_t)(9 * i));
                        int32_t lastDigit = 0;
                        for (uint32_t k = 0; k < 9 - maximum; ++k)
                        {
                            digits = (uint)Math.DivRem((int)digits, 10, out lastDigit);
                        }

                        if (lastDigit != 5)
                        {
                            roundUp = lastDigit > 5 ? 1 : 0;
                        }
                        else
                        {
                            // Is m * 10^(additionalDigits + 1) / 2^(-e2) integer?
                            int32_t requiredTwos = -e2 - precision - 1;
                            bool trailingZeros = requiredTwos <= 0
                             || (requiredTwos < 60 && multipleOfPowerOf2(m2, (uint32_t)requiredTwos));
                            roundUp = trailingZeros ? 2 : 1;

                        }
                        if (maximum > 0)
                        {
                            append_c_digits((int32_t)maximum, digits, result.Slice(index));
                            index += (int)maximum;
                        }
                        break;
                    }
                }

                if (roundUp != 0)
                {
                    int roundIndex = index;
                    int dotIndex = 0; // '.' can't be located at index 0
                    while (true)
                    {
                        --roundIndex;
                        char c;
                        if (roundIndex == -1 || (c = result[roundIndex]) == '-')
                        {
                            result[roundIndex + 1] = '1';
                            if (dotIndex > 0)
                            {
                                result[dotIndex] = '0';
                                result[dotIndex + 1] = '.';
                            }
                            result[index++] = '0';
                            break;
                        }
                        if (c == '.')
                        {
                            dotIndex = roundIndex;
                            continue;
                        }
                        else if (c == '9')
                        {
                            result[roundIndex] = '0';
                            roundUp = 1;
                            continue;
                        }
                        else
                        {
                            if (roundUp == 2 && c % 2 == 0)
                            {
                                break;
                            }
                            result[roundIndex] = (char)(c + 1);
                            break;
                        }
                    }
                }
            }
            else
            {
                result.Slice(index, precision).Fill('0');
                index += precision;
            }
            return index;
        }

        public static Span<char> d2fixed_buffered(double d, int32_t precision, Span<char> result)
        {
            int len = d2fixed_buffered_n(d, precision, result);
            return result.Slice(0, len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static (bool ieeeSign, uint64_t ieeeMantissa, uint32_t ieeeExponent) Parse(double d)
        {
            uint64_t bits = (uint64_t)BitConverter.DoubleToInt64Bits(d);

            // Decode bits into sign, mantissa, and exponent.
            bool ieeeSign = ((bits >> (DOUBLE_MANTISSA_BITS + DOUBLE_EXPONENT_BITS)) & 1) != 0;
            uint64_t ieeeMantissa = bits & ((1ul << DOUBLE_MANTISSA_BITS) - 1);
            uint32_t ieeeExponent = (uint32_t)((bits >> DOUBLE_MANTISSA_BITS) & ((1u << DOUBLE_EXPONENT_BITS) - 1));

            return (ieeeSign, ieeeMantissa, ieeeExponent);
        }

        public static int32_t d2exp_buffered_n(double d, int32_t precision, Span<char> result)
        {
            if (precision < 0)
                throw new ArgumentException(nameof(precision));

            (bool ieeeSign, uint64_t ieeeMantissa, uint32_t ieeeExponent) = Parse(d);

            // Case distinction; exit early for the easy cases.
            if (ieeeExponent == ((1u << DOUBLE_EXPONENT_BITS) - 1u))
            {
                return copy_special_str_printf(result, ieeeSign, ieeeMantissa);
            }
            if (ieeeExponent == 0 && ieeeMantissa == 0)
            {
                int index2 = 0;
                if (ieeeSign)
                {
                    result[index2++] = '-';
                }
                result[index2++] = '0';
                if (precision > 0)
                {
                    result[index2++] = '.';
                    result.Slice(index2, precision).Fill('0');
                    index2 += precision;
                }
                "e+00".AsSpan().CopyTo(result.Slice(index2));
                index2 += 4;
                return index2;
            }

            int32_t e2;
            uint64_t m2;
            if (ieeeExponent == 0)
            {
                e2 = 1 - DOUBLE_BIAS - DOUBLE_MANTISSA_BITS;
                m2 = ieeeMantissa;
            }
            else
            {
                e2 = (int32_t)ieeeExponent - DOUBLE_BIAS - DOUBLE_MANTISSA_BITS;
                m2 = (1ul << DOUBLE_MANTISSA_BITS) | ieeeMantissa;
            }


            bool printDecimalPoint = precision > 0;
            ++precision;
            int index = 0;
            if (ieeeSign)
            {
                result[index++] = '-';
            }
            uint32_t digits = 0;
            int32_t printedDigits = 0;
            int32_t availableDigits = 0;
            int32_t exp = 0;
            if (e2 >= -52)
            {
                uint32_t idx = e2 < 0 ? 0 : indexForExponent((uint32_t)e2);
                uint32_t p10bits = pow10BitsForIndex(idx);
                int32_t len = (int32_t)lengthForIndex(idx);

                for (int32_t i = len - 1; i >= 0; --i)
                {
                    uint32_t j = (uint32_t)(p10bits - e2);
                    // Temporary: j is usually around 128, and by shifting a bit, we push it to 128 or above, which is
                    // a slightly faster code path in mulShift_mod1e9. Instead, we can just increase the multipliers.
                    digits = mulShift_mod1e9(m2 << 8, POW10_SPLIT[POW10_OFFSET[idx] + i], (int32_t)(j + 8));
                    if (printedDigits != 0)
                    {
                        if (printedDigits + 9 > precision)
                        {
                            availableDigits = 9;
                            break;
                        }
                        append_nine_digits(digits, result.Slice(index));
                        index += 9;
                        printedDigits += 9;
                    }
                    else if (digits != 0)
                    {
                        availableDigits = decimalLength9(digits);
                        exp = i * 9 + availableDigits - 1;
                        if (availableDigits > precision)
                        {
                            break;
                        }
                        if (printDecimalPoint)
                        {
                            append_d_digits(availableDigits, digits, result.Slice(index));
                            index += (availableDigits + 1); // +1 for decimal point
                        }
                        else
                        {
                            result[index++] = (char)('0' + digits);
                        }
                        printedDigits = availableDigits;
                        availableDigits = 0;
                    }
                }
            }

            if (e2 < 0 && availableDigits == 0)
            {
                int32_t idx = -e2 / 16;

                for (int32_t i = MIN_BLOCK_2[(uint)idx]; i < 200; ++i)
                {
                    int32_t j = ADDITIONAL_BITS_2 + (-e2 - 16 * idx);
                    uint32_t p = POW10_OFFSET_2[(uint)idx] + (uint32_t)i - MIN_BLOCK_2[(uint)idx];
                    // Temporary: j is usually around 128, and by shifting a bit, we push it to 128 or above, which is
                    // a slightly faster code path in mulShift_mod1e9. Instead, we can just increase the multipliers.
                    digits = (p >= POW10_OFFSET_2[(uint)(idx + 1)]) ? 0 : mulShift_mod1e9(m2 << 8, POW10_SPLIT_2[p], j + 8);

                    if (printedDigits != 0)
                    {
                        if (printedDigits + 9 > precision)
                        {
                            availableDigits = 9;
                            break;
                        }
                        append_nine_digits(digits, result.Slice(index));
                        index += 9;
                        printedDigits += 9;
                    }
                    else if (digits != 0)
                    {
                        availableDigits = decimalLength9(digits);
                        exp = -(i + 1) * 9 + availableDigits - 1;
                        if (availableDigits > precision)
                        {
                            break;
                        }
                        if (printDecimalPoint)
                        {
                            append_d_digits(availableDigits, digits, result.Slice(index));
                            index += availableDigits + 1; // +1 for decimal point
                        }
                        else
                        {
                            result[index++] = (char)('0' + digits);
                        }
                        printedDigits = availableDigits;
                        availableDigits = 0;
                    }
                }
            }

            int32_t maximum = precision - printedDigits;

            if (availableDigits == 0)
            {
                digits = 0;
            }
            int32_t lastDigit = 0;
            if (availableDigits > maximum)
            {
                for (uint32_t k = 0; k < availableDigits - maximum; ++k)
                {
                    digits = (uint)Math.DivRem((int)digits, 10, out lastDigit);
                }
            }

            // 0 = don't round up; 1 = round up unconditionally; 2 = round up if odd.
            int roundUp = 0;
            if (lastDigit != 5)
            {
                roundUp = lastDigit > 5 ? 1:0;
            }
            else
            {
                // Is m * 2^e2 * 10^(precision + 1 - exp) integer?
                // precision was already increased by 1, so we don't need to write + 1 here.
                int32_t rexp = precision - exp;
                int32_t requiredTwos = -e2 - rexp;
                bool trailingZeros = requiredTwos <= 0
                  || (requiredTwos < 60 && multipleOfPowerOf2(m2, (uint32_t)requiredTwos));
                if (rexp < 0)
                {
                    int32_t requiredFives = -rexp;
                    trailingZeros = trailingZeros && multipleOfPowerOf5(m2, (uint32_t)requiredFives);
                }
                roundUp = trailingZeros ? 2 : 1;

            }
            if (printedDigits != 0)
            {
                if (digits == 0)
                {
                    result.Slice(index, maximum).Fill('0');
                }
                else
                {
                    append_c_digits(maximum, digits, result.Slice(index));
                }
                index += maximum;
            }
            else
            {
                if (printDecimalPoint)
                {
                    append_d_digits(maximum, digits, result.Slice(index));
                    index += maximum + 1; // +1 for decimal point
                }
                else
                {
                    result[index++] = (char)('0' + digits);
                }
            }

            if (roundUp != 0)
            {
                int roundIndex = index;
                while (true)
                {
                    --roundIndex;
                    char c;
                    if (roundIndex == -1 || ((c = result[roundIndex]) == '-'))
                    {
                        result[roundIndex + 1] = '1';
                        ++exp;
                        break;
                    }
                    if (c == '.')
                    {
                        continue;
                    }
                    else if (c == '9')
                    {
                        result[roundIndex] = '0';
                        roundUp = 1;
                        continue;
                    }
                    else
                    {
                        if (roundUp == 2 && c % 2 == 0)
                        {
                            break;
                        }
                        result[roundIndex] = (char)(c + 1);
                        break;
                    }
                }
            }
            result[index++] = 'e';
            if (exp < 0)
            {
                result[index++] = '-';
                exp = -exp;
            }
            else
            {
                result[index++] = '+';
            }

            if (exp >= 100)
            {
                int q = Math.DivRem(exp, 10, out int c);
                DIGIT_TABLE.AsSpan(2 * q, 2).CopyTo(result.Slice(index));
                result[index + 2] = (char)('0' + c);
                index += 3;
            }
            else
            {
                DIGIT_TABLE.AsSpan(2 * exp, 2).CopyTo(result.Slice(index));
                index += 2;
            }

            return index;
        }

        public static Span<char> d2exp_buffered(double d, int32_t precision, Span<char> result)
        {
            int len = d2exp_buffered_n(d, precision, result);
            return result.Slice(0, len);
        }

    }
}