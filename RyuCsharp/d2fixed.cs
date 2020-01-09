using System;
using System.Collections.Generic;
using System.Text;
using uint8_t = System.Byte;
using int32_t = System.Int32;
using uint16_t = System.UInt16;
using uint32_t = System.UInt32;
using int64_t = System.Int64;
using uint64_t = System.UInt64;
namespace RyuCsharp
{
    unsafe partial class Ryu
    {
        const int POW10_ADDITIONAL_BITS = 120;

        // Returns the low 64 bits of the high 128 bits of the 256-bit product of a and b.
        static uint64_t umul256_hi128_lo64(
   uint64_t aHi, uint64_t aLo, uint64_t bHi, uint64_t bLo) {
            uint64_t b00Hi;
            uint64_t b00Lo = umul128(aLo, bLo, &b00Hi);
            uint64_t b01Hi;
            uint64_t b01Lo = umul128(aLo, bHi, &b01Hi);
            uint64_t b10Hi;
            uint64_t b10Lo = umul128(aHi, bLo, &b10Hi);
            uint64_t b11Hi;
            uint64_t b11Lo = umul128(aHi, bHi, &b11Hi);
            uint64_t temp1Lo = b10Lo + b00Hi;
            uint64_t temp1Hi = b10Hi;
            if (temp1Lo < b10Lo) ++temp1Hi;
            uint64_t temp2Lo = b01Lo + temp1Lo;
            uint64_t temp2Hi = b01Hi;
            if (temp2Lo < b01Lo) ++temp2Hi;
            return b11Lo + temp1Hi + temp2Hi;
        }

        static uint32_t uint128_mod1e9(uint64_t vHi, uint64_t vLo)
        {
            // After multiplying, we're going to shift right by 29, then truncate to uint32_t.
            // This means that we need only 29 + 32 = 61 bits, so we can truncate to uint64_t before shifting.
            uint64_t multiplied = umul256_hi128_lo64(vHi, vLo, 0x89705F4136B4A597u, 0x31680A88F8953031u);

            // For uint32_t truncation, see the mod1e9() comment in d2s_intrinsics.h.
            uint32_t shifted = (uint32_t)(multiplied >> 29);

            return ((uint32_t)vLo) - 1000000000 * shifted;
        }

        static uint32_t mulShift_mod1e9(uint64_t m, uint64_t* mul, int32_t j) {
            uint64_t high0;                                   // 64
            uint64_t low0 = umul128(m, mul[0], &high0); // 0
            uint64_t high1;                                   // 128
            uint64_t low1 = umul128(m, mul[1], &high1); // 64
            uint64_t high2;                                   // 192
            uint64_t low2 = umul128(m, mul[2], &high2); // 128
            uint64_t s0low = low0;              // 0
            uint64_t s0high = low1 + high0;     // 64
            uint32_t c1 = s0high < low1 ? 1U : 0;
            uint64_t s1low = low2 + high1 + c1; // 128
            uint32_t c2 = s1low < low2 ? 1U : 0; // high1 + c1 can't overflow, so compare against low2
            uint64_t s1high = high2 + c2;       // 192

            assert(j >= 128);
            assert(j <= 180);
            uint32_t dist = (uint32_t)(j - 128); // dist: [0, 52]
            uint64_t shiftedhigh = s1high >> (int)dist;
            uint64_t shiftedlow = shiftright128(s1low, s1high, dist);
            return uint128_mod1e9(shiftedhigh, shiftedlow);
        }

        static void append_n_digits(uint32_t olength, uint32_t digits, char* result)
        {

            uint32_t i = 0;
            while (digits >= 10000)
            {
#if __clang__ // https://bugs.llvm.org/show_bug.cgi?id=38217
         uint32_t c = digits - 10000 * (digits / 10000);
#else
                uint32_t c = digits % 10000;
#endif
                digits /= 10000;
                uint32_t c0 = (c % 100) << 1;
                uint32_t c1 = (c / 100) << 1;
                memcpy(result + olength - i - 2, DIGIT_TABLE + c0, 2);
                memcpy(result + olength - i - 4, DIGIT_TABLE + c1, 2);
                i += 4;
            }
            if (digits >= 100)
            {
                uint32_t c = (digits % 100) << 1;
                digits /= 100;
                memcpy(result + olength - i - 2, DIGIT_TABLE + c, 2);
                i += 2;
            }
            if (digits >= 10)
            {
                uint32_t c = digits << 1;
                memcpy(result + olength - i - 2, DIGIT_TABLE + c, 2);
            }
            else
            {
                result[0] = (char)('0' + digits);
            }
        }

        static void append_d_digits(uint32_t olength, uint32_t digits, char* result)
        {
            uint32_t i = 0;
            while (digits >= 10000)
            {
#if __clang__ // https://bugs.llvm.org/show_bug.cgi?id=38217
         uint32_t c = digits - 10000 * (digits / 10000);
#else
                uint32_t c = digits % 10000;
#endif
                digits /= 10000;
                 uint32_t c0 = (c % 100) << 1;
                 uint32_t c1 = (c / 100) << 1;
                memcpy(result + olength + 1 - i - 2, DIGIT_TABLE + c0, 2);
                memcpy(result + olength + 1 - i - 4, DIGIT_TABLE + c1, 2);
                i += 4;
            }
            if (digits >= 100)
            {
                uint32_t c = (digits % 100) << 1;
                digits /= 100;
                memcpy(result + olength + 1 - i - 2, DIGIT_TABLE + c, 2);
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

        static void append_c_digits(uint32_t count, uint32_t digits, char* result)
        {
            uint32_t i = 0;
            for (; i < count - 1; i += 2)
            {
                uint32_t c = (digits % 100) << 1;
                digits /= 100;
                memcpy(result + count - i - 2, DIGIT_TABLE + c, 2);
            }
            if (i < count)
            {
                char c = (char)('0' + (digits % 10));
                result[count - i - 1] = c;
            }
        }

        static void append_nine_digits(uint32_t digits, char* result)
        {
            if (digits == 0)
            {
                memset(result, '0', 9);
                return;
            }

            for (uint32_t i = 0; i < 5; i += 4)
            {
#if __clang__ // https://bugs.llvm.org/show_bug.cgi?id=38217
         uint32_t c = digits - 10000 * (digits / 10000);
#else
                uint32_t c = digits % 10000;
#endif
                digits /= 10000;
                 uint32_t c0 = (c % 100) << 1;
                 uint32_t c1 = (c / 100) << 1;
                memcpy(result + 7 - i, DIGIT_TABLE + c0, 2);
                memcpy(result + 5 - i, DIGIT_TABLE + c1, 2);
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

        static int copy_special_str_printf(char* result, bool sign, uint64_t mantissa)
        {
#if _MSC_VER
  // TODO: Check that -nan is expected output on Windows.
  if (sign) {
    result[0] = '-';
  }
  if (mantissa != 0) {
    if (mantissa < (1ull << (DOUBLE_MANTISSA_BITS - 1))) {
      memcpy(result + sign, "nan(snan)", 9);
      return sign + 9;
    }
    memcpy(result + sign, "nan", 3);
    return sign + 3;
  }
#else
            if (mantissa != 0)
            {
                memcpy(result, "nan", 3);
                return 3;
            }
            if (sign)
            {
                result[0] = '-';
            }
#endif
            memcpy(result + (sign ? 1 : 0), "Infinity", 8);
            return (sign ? 1 : 0) + 8;
        }

        public static int d2fixed_buffered_n(double d, uint32_t precision, char* result)
        {
            uint64_t bits = double_to_bits(d);


            // Decode bits into sign, mantissa, and exponent.
            bool ieeeSign = ((bits >> (DOUBLE_MANTISSA_BITS + DOUBLE_EXPONENT_BITS)) & 1) != 0;
            uint64_t ieeeMantissa = bits & ((1ul << DOUBLE_MANTISSA_BITS) -1);
            uint32_t ieeeExponent = (uint32_t)((bits >> DOUBLE_MANTISSA_BITS) & ((1u << DOUBLE_EXPONENT_BITS) - 1));

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
                    memset(result + index2, '0', precision);
                    index2 += (int)precision;
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
                    uint32_t digits = mulShift_mod1e9(m2 << 8, POW10_SPLIT[(uint)(POW10_OFFSET[(uint)idx] + i)], (int32_t)(j + 8));
                    if (nonzero)
                    {
                        append_nine_digits(digits, result + index);
                        index += 9;
                    }
                    else if (digits != 0)
                    {
                        uint32_t olength = decimalLength9(digits);
                        append_n_digits(olength, digits, result + index);
                        index += (int)olength;
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

                uint32_t blocks = precision / 9 + 1;
                // 0 = don't round up; 1 = round up unconditionally; 2 = round up if odd.
                int roundUp = 0;
                uint32_t i = 0;
                if (blocks <= MIN_BLOCK_2[(uint)idx])
                {
                    i = blocks;
                    memset(result + index, '0', precision);
                    index += (int)precision;
                }
                else if (i < MIN_BLOCK_2[(uint)idx])
                {
                    i = MIN_BLOCK_2[(uint)idx];
                    memset(result + index, '0', 9 * i);
                    index += (int)(9 * i);
                }
                for (; i < blocks; ++i)
                {
                    int32_t j = ADDITIONAL_BITS_2 + (-e2 - 16 * idx);
                    uint32_t p = POW10_OFFSET_2[(uint)idx] + i - MIN_BLOCK_2[(uint)idx];
                    if (p >= POW10_OFFSET_2[(uint)idx + 1])
                    {
                        // If the remaining digits are all 0, then we might as well use memset.
                        // No rounding required in this case.
                        uint32_t fill = precision - 9 * i;
                        memset(result + index, '0', fill);
                        index += (int)fill;
                        break;
                    }
                    // Temporary: j is usually around 128, and by shifting a bit, we push it to 128 or above, which is
                    // a slightly faster code path in mulShift_mod1e9. Instead, we can just increase the multipliers.
                    uint32_t digits = mulShift_mod1e9(m2 << 8, POW10_SPLIT_2[p], j + 8);

                    if (i < blocks - 1)
                    {
                        append_nine_digits(digits, result + index);
                        index += 9;
                    }
                    else
                    {
                        uint32_t maximum = precision - 9 * i;
                        uint32_t lastDigit = 0;
                        for (uint32_t k = 0; k < 9 - maximum; ++k)
                        {
                            lastDigit = digits % 10;
                            digits /= 10;
                        }

                        if (lastDigit != 5)
                        {
                            roundUp = lastDigit > 5 ? 1 : 0;
                        }
                        else
                        {
                            // Is m * 10^(additionalDigits + 1) / 2^(-e2) integer?
                            int32_t requiredTwos = -e2 - (int32_t)precision - 1;
                            bool trailingZeros = requiredTwos <= 0
                             || (requiredTwos < 60 && multipleOfPowerOf2(m2, (uint32_t)requiredTwos));
                            roundUp = trailingZeros ? 2 : 1;

                        }
                        if (maximum > 0)
                        {
                            append_c_digits(maximum, digits, result + index);
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
                memset(result + index, '0', precision);
                index += (int)precision;
            }
            return index;
        }

        static void d2fixed_buffered(double d, uint32_t precision, char* result)
        {
            int len = d2fixed_buffered_n(d, precision, result);
            result[len] = '\0';
        }



        public static int d2exp_buffered_n(double d, uint32_t precision, char* result)
        {
            uint64_t bits = double_to_bits(d);


            // Decode bits into sign, mantissa, and exponent.
             bool ieeeSign = ((bits >> (DOUBLE_MANTISSA_BITS + DOUBLE_EXPONENT_BITS)) & 1) != 0;
             uint64_t ieeeMantissa = bits & ((1ul << DOUBLE_MANTISSA_BITS) -1);
             uint32_t ieeeExponent = (uint32_t)((bits >> DOUBLE_MANTISSA_BITS) & ((1u << DOUBLE_EXPONENT_BITS) - 1));

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
                    memset(result + index2, '0', precision);
                    index2 += (int)precision;
                }
                memcpy(result + index2, "e+00", 4);
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
            uint32_t printedDigits = 0;
            uint32_t availableDigits = 0;
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
                    digits = mulShift_mod1e9(m2 << 8, POW10_SPLIT[(uint)(POW10_OFFSET[(uint)idx] + i)], (int32_t)(j + 8));
                    if (printedDigits != 0)
                    {
                        if (printedDigits + 9 > precision)
                        {
                            availableDigits = 9;
                            break;
                        }
                        append_nine_digits(digits, result + index);
                        index += 9;
                        printedDigits += 9;
                    }
                    else if (digits != 0)
                    {
                        availableDigits = decimalLength9(digits);
                        exp = i * 9 + (int32_t)availableDigits - 1;
                        if (availableDigits > precision)
                        {
                            break;
                        }
                        if (printDecimalPoint)
                        {
                            append_d_digits(availableDigits, digits, result + index);
                            index += (int)(availableDigits + 1); // +1 for decimal point
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
                        append_nine_digits(digits, result + index);
                        index += 9;
                        printedDigits += 9;
                    }
                    else if (digits != 0)
                    {
                        availableDigits = decimalLength9(digits);
                        exp = -(i + 1) * 9 + (int32_t)availableDigits - 1;
                        if (availableDigits > precision)
                        {
                            break;
                        }
                        if (printDecimalPoint)
                        {
                            append_d_digits(availableDigits, digits, result + index);
                            index += (int)(availableDigits + 1); // +1 for decimal point
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

            uint32_t maximum = precision - printedDigits;

            if (availableDigits == 0)
            {
                digits = 0;
            }
            uint32_t lastDigit = 0;
            if (availableDigits > maximum)
            {
                for (uint32_t k = 0; k < availableDigits - maximum; ++k)
                {
                    lastDigit = digits % 10;
                    digits /= 10;
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
                int32_t rexp = (int32_t)precision - exp;
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
                    memset(result + index, '0', maximum);
                }
                else
                {
                    append_c_digits(maximum, digits, result + index);
                }
                index += (int)maximum;
            }
            else
            {
                if (printDecimalPoint)
                {
                    append_d_digits(maximum, digits, result + index);
                    index += (int)(maximum + 1); // +1 for decimal point
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
                 int32_t c = exp % 10;
                memcpy(result + index, DIGIT_TABLE + (uint)(2 * (exp / 10)), 2);
                result[index + 2] = (char)('0' + c);
                index += 3;
            }
            else
            {
                memcpy(result + index, DIGIT_TABLE + (uint)(2 * exp), 2);
                index += 2;
            }

            return index;
        }

        static void d2exp_buffered(double d, uint32_t precision, char* result)
        {
            int len = d2exp_buffered_n(d, precision, result);
            result[len] = '\0';
        }

    }
}