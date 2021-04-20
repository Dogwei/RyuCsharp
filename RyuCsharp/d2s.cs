using System.Diagnostics;
using System;
using uint8_t = System.Byte;
using int32_t = System.Int32;
using uint32_t = System.UInt32;
using int64_t = System.Int64;
using uint64_t = System.UInt64;

namespace RyuCsharp
{
    partial class Ryu
    {
        static int32_t decimalLength17(uint64_t v)
        {
            // This is slightly faster than a loop.
            // The average output length is 16.38 digits, so we check high-to-low.
            // Function precondition: v is not an 18, 19, or 20-digit number.
            // (17 digits are sufficient for round-tripping.)
            Debug.Assert(v < 100000000000000000L);
            if (v >= 10000000000000000L) { return 17; }
            if (v >= 1000000000000000L) { return 16; }
            if (v >= 100000000000000L) { return 15; }
            if (v >= 10000000000000L) { return 14; }
            if (v >= 1000000000000L) { return 13; }
            if (v >= 100000000000L) { return 12; }
            if (v >= 10000000000L) { return 11; }
            if (v >= 1000000000L) { return 10; }
            if (v >= 100000000L) { return 9; }
            if (v >= 10000000L) { return 8; }
            if (v >= 1000000L) { return 7; }
            if (v >= 100000L) { return 6; }
            if (v >= 10000L) { return 5; }
            if (v >= 1000L) { return 4; }
            if (v >= 100L) { return 3; }
            if (v >= 10L) { return 2; }
            return 1;
        }

        static floating_decimal_64 d2d(uint64_t ieeeMantissa, uint32_t ieeeExponent)
        {
            int32_t e2;
            uint64_t m2;
            if (ieeeExponent == 0)
            {
                // We subtract 2 so that the bounds computation has 2 additional bits.
                e2 = 1 - DOUBLE_BIAS - DOUBLE_MANTISSA_BITS - 2;
                m2 = ieeeMantissa;
            }
            else
            {
                e2 = (int32_t)ieeeExponent - DOUBLE_BIAS - DOUBLE_MANTISSA_BITS - 2;
                m2 = (1ul << DOUBLE_MANTISSA_BITS) | ieeeMantissa;
            }
            bool even = (m2 & 1) == 0;
            bool acceptBounds = even;


            // Step 2: Determine the interval of valid decimal representations.
            uint64_t mv = 4 * m2;
            // Implicit bool -> int32_t conversion. True is 1, false is 0.
            uint32_t mmShift = (ieeeMantissa != 0 || ieeeExponent <= 1) ? 1U : 0;
            // We would compute mp and mm like this:
            // uint64_t mp = 4 * m2 + 2;
            // uint64_t mm = mv - 1 - mmShift;

            // Step 3: Convert to a decimal power base using 128-bit arithmetic.
            uint64_t vr, vp, vm;
            int32_t e10;
            bool vmIsTrailingZeros = false;
            bool vrIsTrailingZeros = false;
            if (e2 >= 0)
            {
                // I tried special-casing q == 0, but there was no effect on performance.
                // This expression is slightly faster than max(0, log10Pow2(e2) - 1).
                uint32_t q = log10Pow2(e2);
                if (e2 > 3) --q;
                e10 = (int32_t)q;
                int32_t k = DOUBLE_POW5_INV_BITCOUNT + pow5bits((int32_t)q) - 1;
                int32_t i = -e2 + (int32_t)q + k;

                vr = mulShiftAll64(m2, DOUBLE_POW5_INV_SPLIT[q], i, out vp, out vm, mmShift);

                if (q <= 21)
                {
                    // This should use q <= 22, but I think 21 is also safe. Smaller values
                    // may still be safe, but it's more difficult to reason about them.
                    // Only one of mp, mv, and mm can be a multiple of 5, if any.
                    uint32_t mvMod5 = ((uint32_t)mv) - 5 * ((uint32_t)div5(mv));
                    if (mvMod5 == 0)
                    {
                        vrIsTrailingZeros = multipleOfPowerOf5(mv, q);
                    }
                    else if (acceptBounds)
                    {
                        // Same as min(e2 + (~mm & 1), pow5Factor(mm)) >= q
                        // <=> e2 + (~mm & 1) >= q && pow5Factor(mm) >= q
                        // <=> true && pow5Factor(mm) >= q, since e2 >= q.
                        vmIsTrailingZeros = multipleOfPowerOf5(mv - 1 - mmShift, q);
                    }
                    else
                    {
                        // Same as min(e2 + 1, pow5Factor(mp)) >= q.
                        if (multipleOfPowerOf5(mv + 2, q)) --vp;
                    }
                }
            }
            else
            {
                // This expression is slightly faster than max(0, log10Pow5(-e2) - 1).
                uint32_t q = log10Pow5(-e2);
                if (-e2 > 1) --q;
                e10 = (int32_t)q + e2;
                int32_t i = -e2 - (int32_t)q;
                int32_t k = pow5bits(i) - DOUBLE_POW5_BITCOUNT;
                int32_t j = (int32_t)q - k;
                vr = mulShiftAll64(m2, DOUBLE_POW5_SPLIT[i], j, out vp, out vm, mmShift);
                if (q <= 1)
                {
                    // {vr,vp,vm} is trailing zeros if {mv,mp,mm} has at least q trailing 0 bits.
                    // mv = 4 * m2, so it always has at least two trailing 0 bits.
                    vrIsTrailingZeros = true;
                    if (acceptBounds)
                    {
                        // mm = mv - 1 - mmShift, so it has 1 trailing 0 bit iff mmShift == 1.
                        vmIsTrailingZeros = mmShift == 1;
                    }
                    else
                    {
                        // mp = mv + 2, so it always has at least one trailing 0 bit.
                        --vp;
                    }
                }
                else if (q < 63)
                { // TODO(ulfjack): Use a tighter bound here.
                  // We want to know if the full product has at least q trailing zeros.
                  // We need to compute min(p2(mv), p5(mv) - e2) >= q
                  // <=> p2(mv) >= q && p5(mv) - e2 >= q
                  // <=> p2(mv) >= q (because -e2 >= q)
                    vrIsTrailingZeros = multipleOfPowerOf2(mv, q);

                }
            }

            // Step 4: Find the shortest decimal representation in the interval of valid representations.
            int32_t removed = 0;
            uint8_t lastRemovedDigit = 0;
            uint64_t output;
            // On average, we remove ~2 digits.
            if (vmIsTrailingZeros || vrIsTrailingZeros)
            {
                // General case, which happens rarely (~0.7%).
                for (; ; )
                {
                    uint64_t vpDiv10 = div10(vp);
                    uint64_t vmDiv10 = div10(vm);
                    if (vpDiv10 <= vmDiv10)
                    {
                        break;
                    }
                    uint32_t vmMod10 = ((uint32_t)vm) - 10 * ((uint32_t)vmDiv10);
                    uint64_t vrDiv10 = div10(vr);
                    uint32_t vrMod10 = ((uint32_t)vr) - 10 * ((uint32_t)vrDiv10);
                    vmIsTrailingZeros &= vmMod10 == 0;
                    vrIsTrailingZeros &= lastRemovedDigit == 0;
                    lastRemovedDigit = (uint8_t)vrMod10;
                    vr = vrDiv10;
                    vp = vpDiv10;
                    vm = vmDiv10;
                    ++removed;
                }
                if (vmIsTrailingZeros)
                {
                    for (; ; )
                    {
                        uint64_t vmDiv10 = div10(vm);
                        uint32_t vmMod10 = ((uint32_t)vm) - (10 * ((uint32_t)vmDiv10));
                        if (vmMod10 != 0)
                        {
                            break;
                        }
                        uint64_t vpDiv10 = div10(vp);
                        uint64_t vrDiv10 = div10(vr);
                        uint32_t vrMod10 = ((uint32_t)vr) - (10 * ((uint32_t)vrDiv10));
                        vrIsTrailingZeros &= lastRemovedDigit == 0;
                        lastRemovedDigit = (uint8_t)vrMod10;
                        vr = vrDiv10;
                        vp = vpDiv10;
                        vm = vmDiv10;
                        ++removed;
                    }
                }
                if (vrIsTrailingZeros && lastRemovedDigit == 5 && vr % 2 == 0)
                {
                    // Round even if the exact number is .....50..0.
                    lastRemovedDigit = 4;
                }
                // We need to take vr + 1 if vr is outside bounds or we need to round up.
                output = vr;
                if ((vr == vm && (!acceptBounds || !vmIsTrailingZeros)) || lastRemovedDigit >= 5) ++output;
            }
            else
            {
                // Specialized for the common case (~99.3%). Percentages below are relative to this.
                bool roundUp = false;
                uint64_t vpDiv100 = div100(vp);
                uint64_t vmDiv100 = div100(vm);
                if (vpDiv100 > vmDiv100)
                { // Optimization: remove two digits at a time (~86.2%).
                    uint64_t vrDiv100 = div100(vr);
                    uint32_t vrMod100 = ((uint32_t)vr) - (100 * ((uint32_t)vrDiv100));
                    roundUp = vrMod100 >= 50;
                    vr = vrDiv100;
                    vp = vpDiv100;
                    vm = vmDiv100;
                    removed += 2;
                }
                // Loop iterations below (approximately), without optimization above:
                // 0: 0.03%, 1: 13.8%, 2: 70.6%, 3: 14.0%, 4: 1.40%, 5: 0.14%, 6+: 0.02%
                // Loop iterations below (approximately), with optimization above:
                // 0: 70.6%, 1: 27.8%, 2: 1.40%, 3: 0.14%, 4+: 0.02%
                for (; ; )
                {
                    uint64_t vpDiv10 = div10(vp);
                    uint64_t vmDiv10 = div10(vm);
                    if (vpDiv10 <= vmDiv10)
                    {
                        break;
                    }
                    uint64_t vrDiv10 = div10(vr);
                    uint32_t vrMod10 = ((uint32_t)vr) - (10 * ((uint32_t)vrDiv10));
                    roundUp = vrMod10 >= 5;
                    vr = vrDiv10;
                    vp = vpDiv10;
                    vm = vmDiv10;
                    ++removed;
                }

                // We need to take vr + 1 if vr is outside bounds or we need to round up.
                output = vr;
                if (vr == vm || roundUp) ++output;
            }
            int32_t exp = e10 + removed;

            floating_decimal_64 fd = new floating_decimal_64
            {
                exponent = exp,
                mantissa = output,
            };
            return fd;
        }

        static int to_chars(floating_decimal_64 v, bool sign, Span<char> result)
        {
            // Step 5: Print the decimal representation.
            int index = 0;
            if (sign)
            {
                result[index++] = '-';
            }

            uint64_t output = v.mantissa;
            int32_t olength = decimalLength17(output);


            // Print the decimal digits.
            // The following code is equivalent to:
            // for (uint32_t i = 0; i < olength - 1; ++i) {
            //   const uint32_t c = output % 10; output /= 10;
            //   result[index + olength - i] = (char) ('0' + c);
            // }
            // result[index] = '0' + output % 10;

            int32_t i = 0;
            // We prefer 32-bit operations, even on 64-bit platforms.
            // We have at most 17 digits, and uint32_t can store 9 digits.
            // If output doesn't fit into uint32_t, we cut off 8 digits,
            // so the rest will fit into uint32_t.
            if ((output >> 32) != 0)
            {
                // Expensive 64-bit division.
                uint64_t q = div1e8(output);
                uint32_t output3 = ((uint32_t)output) - (100000000 * ((uint32_t)q));
                output = q;

                output3 = (uint)Math.DivRem((int)output3, 10000, out int32_t c);
                int32_t d = (int32_t)(output3 % 10000);
                int32_t c1 = Math.DivRem(c, 100, out int c0) << 1;
                c0 <<= 1;
                int32_t d1 = Math.DivRem(d, 100, out int d0) << 1;
                d0 <<= 1;
                DIGIT_TABLE.AsSpan(c0, 2).CopyTo(result.Slice(index + olength - i - 1));
                DIGIT_TABLE.AsSpan(c1, 2).CopyTo(result.Slice(index + olength - i - 3));
                DIGIT_TABLE.AsSpan(d0, 2).CopyTo(result.Slice(index + olength - i - 5));
                DIGIT_TABLE.AsSpan(d1, 2).CopyTo(result.Slice(index + olength - i - 7));
                i += 8;
            }
            uint32_t output2 = (uint32_t)output;
            while (output2 >= 10000)
            {
                output2 = (uint32_t)Math.DivRem((int32_t)output2, 10000, out int32_t c);
                int32_t c1 = Math.DivRem(c, 100, out int c0) << 1;
                c0 <<= 1;
                DIGIT_TABLE.AsSpan(c0, 2).CopyTo(result.Slice(index + olength - i - 1));
                DIGIT_TABLE.AsSpan(c1, 2).CopyTo(result.Slice(index + olength - i - 3));
                i += 4;
            }
            if (output2 >= 100)
            {
                output2 = (uint32_t)Math.DivRem((int32_t)output2 , 100, out int32_t c);
                c <<= 1;
                DIGIT_TABLE.AsSpan(c, 2).CopyTo(result.Slice(index + olength - i - 1));
                i += 2;
            }
            if (output2 >= 10)
            {
                uint32_t c = output2 << 1;
                // We can't use memcpy here: the decimal dot goes between these two digits.
                result[index + olength - i] = DIGIT_TABLE[c + 1];
                result[index] = DIGIT_TABLE[c];
            }
            else
            {
                result[index] = (char)('0' + output2);
            }

            // Print decimal point if needed.
            if (olength > 1)
            {
                result[index + 1] = '.';
                index += olength + 1;
            }
            else
            {
                ++index;
            }

            // Print the exponent.
            result[index++] = 'E';
            int32_t exp = v.exponent + olength - 1;
            if (exp < 0)
            {
                result[index++] = '-';
                exp = -exp;
            }

            if (exp >= 100)
            {
                DIGIT_TABLE.AsSpan(2 * Math.DivRem(exp, 10, out var c), 2).CopyTo(result.Slice(index));
                result[index + 2] = (char)('0' + c);
                index += 3;
            }
            else if (exp >= 10)
            {
                DIGIT_TABLE.AsSpan(2 * exp, 2).CopyTo(result.Slice(index));
                index += 2;
            }
            else
            {
                result[index++] = (char)('0' + exp);
            }

            return index;
        }

        static bool d2d_small_int(uint64_t ieeeMantissa, uint32_t ieeeExponent, out floating_decimal_64 v)
        {
            if (ieeeMantissa >= (1ul << DOUBLE_MANTISSA_BITS))
                throw new ArgumentOutOfRangeException(nameof(ieeeMantissa));

            uint64_t m2 = (1ul << DOUBLE_MANTISSA_BITS) | ieeeMantissa;
            int32_t e2 = (int32_t)ieeeExponent - DOUBLE_BIAS - DOUBLE_MANTISSA_BITS;

            if (e2 > 0)
            {
                v = default;
                // f = m2 * 2^e2 >= 2^53 is an integer.
                // Ignore this case for now.
                return false;
            }

            if (e2 < -52)
            {
                v = default;
                // f < 1.
                return false;
            }

            // Since 2^52 <= m2 < 2^53 and 0 <= -e2 <= 52: 1 <= f = m2 / 2^-e2 < 2^53.
            // Test if the lower -e2 bits of the significand are 0, i.e. whether the fraction is 0.
            uint64_t mask = (1ul << -e2) - 1;
            uint64_t fraction = m2 & mask;
            if (fraction != 0)
            {
                v = default;
                return false;
            }

            // f is an integer in the range [1, 2^53).
            // Note: mantissa might contain trailing (decimal) 0's.
            // Note: since 2^53 < 10^16, there is no need to adjust decimalLength17().
            v.mantissa = m2 >> -e2;
            v.exponent = 0;
            return true;
        }

        public static int d2s_buffered_n(double f, Span<char> result)
        {
            (bool ieeeSign, uint64_t ieeeMantissa, uint32_t ieeeExponent) = Parse(f);

            // Case distinction; exit early for the easy cases.
            if (ieeeExponent == ((1u << DOUBLE_EXPONENT_BITS) - 1u) || (ieeeExponent == 0 && ieeeMantissa == 0))
            {
                return copy_special_str(result, ieeeSign, ieeeExponent != 0, ieeeMantissa != 0);
            }

            bool isSmallInt = d2d_small_int(ieeeMantissa, ieeeExponent, out floating_decimal_64 v);
            if (isSmallInt)
            {
                // For small integers in the range [1, 2^53), v.mantissa might contain trailing (decimal) zeros.
                // For scientific notation we need to move these zeros into the exponent.
                // (This is not needed for fixed-point notation, so it might be beneficial to trim
                // trailing zeros in to_chars only if needed - once fixed-point notation output is implemented.)
                for (; ; )
                {
                    uint64_t q = div10(v.mantissa);
                    uint32_t r = ((uint32_t)v.mantissa) - (10 * ((uint32_t)q));
                    if (r != 0)
                    {
                        break;
                    }
                    v.mantissa = q;
                    ++v.exponent;
                }
            }
            else
            {
                v = d2d(ieeeMantissa, ieeeExponent);
            }

            return to_chars(v, ieeeSign, result);
        }

        public static Span<char> d2s_buffered(double f, Span<char> result)
        {
            int index = d2s_buffered_n(f, result);

            // Terminate the string.
            return result.Slice(0, index);
        }

    }
}