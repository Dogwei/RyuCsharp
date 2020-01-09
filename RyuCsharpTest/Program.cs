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
        }

        public static void Test(double val)
        {
            const int buffer_length = 2000;

            var buffer = stackalloc char[2000];

            var str1 = new string(buffer, 0, Ryu.d2s_buffered_n(val, buffer));
            double val1;
            var eq1 = Ryu.s2d_n(buffer, str1.Length, &val1);
            Empty(buffer, buffer_length);

            var str2 = new string(buffer, 0, Ryu.d2exp_buffered_n(val, 10, buffer));
            double val2;
            var eq2 = Ryu.s2d_n(buffer, str2.Length, &val2);
            Empty(buffer, buffer_length);

            var str3 = new string(buffer, 0, Ryu.d2fixed_buffered_n(val, 10, buffer));
            double val3;
            var eq3 = Ryu.s2d_n(buffer, str3.Length, &val3);
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