using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using RyuCsharp;

namespace RyuCsharpTest
{
    unsafe class Program
    {
        static unsafe void Main(string[] args)
        {
            Test(3);
            Test(3.14);
            Test(3.1415926);
            Test(998);
            Test(1218);
            Test(19971218);
            Test(ulong.MaxValue);
            Test(long.MinValue);
            Test(0.1);
            Test(0.00314);
            Test(0.0000000998);
            Test(-0.0000000998);

            Console.WriteLine();

            Test(3.1415926e100);
            Test(double.MaxValue);
            Test(double.MinValue);
            Test(double.Epsilon);
        }

        public static void Test(double val)
        {
            const int buffer_length = 2000;

            var buffer = stackalloc char[buffer_length];

            var str1 = Ryu.d2s_buffered(val, new Span<char>(buffer, buffer_length)).ToString();
            double val1;
            var eq1 = Ryu.s2d_n(new ReadOnlySpan<char>(buffer, 2000), str1.Length, out val1);
            Empty(buffer, buffer_length);

            var str2 = Ryu.d2exp_buffered(val, 10, new Span<char>(buffer, buffer_length)).ToString();
            double val2;
            var eq2 = Ryu.s2d_n(new ReadOnlySpan<char>(buffer, 2000), str2.Length, out val2);
            Empty(buffer, buffer_length);

            var str3 = Ryu.d2fixed_buffered(val, 10, new Span<char>(buffer, buffer_length)).ToString();
            double val3;
            var eq3 = Ryu.s2d_n(new ReadOnlySpan<char>(buffer, buffer_length), str3.Length, out val3);
            Empty(buffer, buffer_length);

            Console.WriteLine($"Value: {val}, d2s: [{str1} -- s2d: {val1}], d2exp(10): [{str2} -- s2d: {val2}], d2fixed(10): [{str3} -- s2d: {val3}]");
        }

        public static void Empty(char* buffer, int length)
        {
            for (int i = 0; i < length; i++)
            {
                buffer[i] = default;
            }
        }
    }
}