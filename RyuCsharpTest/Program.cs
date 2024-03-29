﻿using RyuCsharp;
using System;
using System.Runtime.CompilerServices;

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
            Test(5737722933969577e-231);
        }

        public static void Test(double val)
        {
            const int buffer_length = 2000;

            var buffer = new char[2000];

            var str1 = new string(buffer, 0, Ryu.d2s_buffered_n(val, ref buffer[0]));
            double val1;
            var eq1 = Ryu.s2d_n(ref buffer[0], str1.Length, out val1);
            Empty(ref buffer[0], buffer_length);

            var str2 = new string(buffer, 0, Ryu.d2exp_buffered_n(val, 10, ref buffer[0]));
            double val2;
            var eq2 = Ryu.s2d_n(ref buffer[0], str2.Length, out val2);
            Empty(ref buffer[0], buffer_length);

            var str3 = new string(buffer, 0, Ryu.d2fixed_buffered_n(val, 10, ref buffer[0]));
            double val3;
            var eq3 = Ryu.s2d_n(ref buffer[0], str3.Length, out val3);
            Empty(ref buffer[0], buffer_length);

            Console.WriteLine($"Value: {val}, d2s: [{str1} -- s2d: {val1}], d2exp(10): [{str2} -- s2d: {val2}], d2fixed(10): [{str3} -- s2d: {val3}]");
        }

        public static void Empty(ref char buffer, int length)
        {
            Unsafe.InitBlock(
                ref Unsafe.As<char, byte>(ref buffer), 
                0, 
                checked((uint)length * sizeof(char))
                );
        }
    }
}