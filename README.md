# RyuCsharp

使用 C# 实现的 [Ryu](https://github.com/ulfjack/ryu) ，一个高性能浮点数转换为字符串的算法。


Ryu, implemented by C#, a high-performance algorithm for converting floats to strings.



### 2023-09-21 RyuCsharp update descriptions:

- To avoid managed memory fragmentation, the implementation was changed from pointers to references. (The difference between pointer and reference is that the former is an unmanaged pointer and the latter is a managed pointer.)
- Inherit the open source license of the parent project to make it easier for everyone to use.
- fix bugs.
- Publish [nuget package](https://www.nuget.org/packages/RyuCsharp/).

### Usage example:

```C#

{
    // Array
    char[] charArray = new char[32]; // Need to ensure that the memory size is sufficient.
    var writtenLength = Ryu.d2s_buffered_n(3.1415926D, ref charArray[0]);
    Console.WriteLine(new string(charArray, 0, writtenLength)); // Output: 3.1415926
}

{
    // Span
    Span<char> charSpan = (new char[32]).AsSpan();
    var writtenLength = Ryu.d2s_buffered_n(3.1415926D, ref charSpan[0]);
    Console.WriteLine(charSpan.Slice(0, writtenLength).ToString()); // Output: 3.1415926
}

{
    // Pointer
    char* pChars = stackalloc char[32];
    var writtenLength = Ryu.d2s_buffered_n(3.1415926D, ref *pChars);
    Console.WriteLine(new string(pChars, 0, writtenLength)); // Output: 3.1415926
}

```
