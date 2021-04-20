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
        static readonly (uint64_t offset0, uint64_t offset1)[] DOUBLE_POW5_INV_SPLIT2 = new (uint64_t, uint64_t)[13] {
          (                    1u, 2305843009213693952u ),
          (  5955668970331000884u, 1784059615882449851u ),
          (  8982663654677661702u, 1380349269358112757u ),
          (  7286864317269821294u, 2135987035920910082u ),
          (  7005857020398200553u, 1652639921975621497u ),
          ( 17965325103354776697u, 1278668206209430417u ),
          (  8928596168509315048u, 1978643211784836272u ),
          ( 10075671573058298858u, 1530901034580419511u ),
          (   597001226353042382u, 1184477304306571148u ),
          (  1527430471115325346u, 1832889850782397517u ),
          ( 12533209867169019542u, 1418129833677084982u ),
          (  5577825024675947042u, 2194449627517475473u ),
          ( 11006974540203867551u, 1697873161311732311u )
        };
        static readonly uint32_t[] POW5_INV_OFFSETS = new uint32_t[19]{
          0x54544554, 0x04055545, 0x10041000, 0x00400414, 0x40010000, 0x41155555,
          0x00000454, 0x00010044, 0x40000000, 0x44000041, 0x50454450, 0x55550054,
          0x51655554, 0x40004000, 0x01000001, 0x00010500, 0x51515411, 0x05555554,
          0x00000000
        };

        static readonly (uint64_t offset0, uint64_t offset1)[] DOUBLE_POW5_SPLIT2 = new (uint64_t, uint64_t)[13] {
          (                    0u, 1152921504606846976u ),
          (                    0u, 1490116119384765625u ),
          (  1032610780636961552u, 1925929944387235853u ),
          (  7910200175544436838u, 1244603055572228341u ),
          ( 16941905809032713930u, 1608611746708759036u ),
          ( 13024893955298202172u, 2079081953128979843u ),
          (  6607496772837067824u, 1343575221513417750u ),
          ( 17332926989895652603u, 1736530273035216783u ),
          ( 13037379183483547984u, 2244412773384604712u ),
          (  1605989338741628675u, 1450417759929778918u ),
          (  9630225068416591280u, 1874621017369538693u ),
          (   665883850346957067u, 1211445438634777304u ),
          ( 14931890668723713708u, 1565756531257009982u )
        };
        static readonly uint32_t[] POW5_OFFSETS = new uint32_t[21] {
          0x00000000, 0x00000000, 0x00000000, 0x00000000, 0x40000000, 0x59695995,
          0x55545555, 0x56555515, 0x41150504, 0x40555410, 0x44555145, 0x44504540,
          0x45555550, 0x40004000, 0x96440440, 0x55565565, 0x54454045, 0x40154151,
          0x55559155, 0x51405555, 0x00000105
        };

        const int32_t POW5_TABLE_SIZE = 26;
        static readonly uint64_t[] DOUBLE_POW5_TABLE = new uint64_t[POW5_TABLE_SIZE] {
            1ul, 5ul, 25ul, 125ul, 625ul, 3125ul, 15625ul, 78125ul, 390625ul,
            1953125ul, 9765625ul, 48828125ul, 244140625ul, 1220703125ul, 6103515625ul,
            30517578125ul, 152587890625ul, 762939453125ul, 3814697265625ul,
            19073486328125ul, 95367431640625ul, 476837158203125ul,
            2384185791015625ul, 11920928955078125ul, 59604644775390625ul,
            298023223876953125ul //, 1490116119384765625ul
        };

        // Computes 5^i in the form required by Ryu, and stores it in the given pointer.
        static void double_computePow5(uint32_t i, Span<uint64_t> result)
        {
            uint32_t @base = i / POW5_TABLE_SIZE;
            uint32_t base2 = @base * POW5_TABLE_SIZE;
            uint32_t offset = i - base2;
            var mul = DOUBLE_POW5_SPLIT2[@base];
            if (offset == 0)
            {
                result[0] = mul.offset0;
                result[1] = mul.offset1;
                return;
            }
            uint64_t m = DOUBLE_POW5_TABLE[offset];
            uint64_t low1 = umul128(m, mul.offset0, out uint64_t high1);
            uint64_t low0 = umul128(m, mul.offset1, out uint64_t high0);
            uint64_t sum = high0 + low1;
            if (sum < high0)
            {
                ++high1; // overflow into high1
            }
            // high1 | sum | low0
            uint32_t delta = (uint32_t)(pow5bits((int32_t)i) - pow5bits((int32_t)base2));
            result[0] = shiftright128(low0, sum, delta) + ((POW5_OFFSETS[i / 16] >> (int32_t)((i % 16) << 1)) & 3);
            result[1] = shiftright128(sum, high1, delta);
        }

        // Computes 5^-i in the form required by Ryu, and stores it in the given pointer.
        static void double_computeInvPow5(uint32_t i, Span<uint64_t> result)
        {
            uint32_t @base = (i + POW5_TABLE_SIZE - 1) / POW5_TABLE_SIZE;
            uint32_t base2 = @base * POW5_TABLE_SIZE;
            uint32_t offset = base2 - i;
            var mul = DOUBLE_POW5_INV_SPLIT2[@base]; // 1/5^base2
            if (offset == 0)
            {
                result[0] = mul.offset0;
                result[1] = mul.offset1;
                return;
            }
            uint64_t m = DOUBLE_POW5_TABLE[offset];
            uint64_t low1 = umul128(m, mul.offset1, out uint64_t high1);
            uint64_t low0 = umul128(m, mul.offset0 - 1, out uint64_t high0);
            uint64_t sum = high0 + low1;
            if (sum < high0)
            {
                ++high1; // overflow into high1
            }
            // high1 | sum | low0
            uint32_t delta = (uint32_t)(pow5bits((int32_t)base2) - pow5bits((int32_t)i));
            result[0] = shiftright128(low0, sum, delta) + 1 + ((POW5_INV_OFFSETS[i / 16] >> (int32_t)((i % 16) << 1)) & 3);
            result[1] = shiftright128(sum, high1, delta);
        }
    }
}