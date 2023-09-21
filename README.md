# RyuCsharp

使用 C# 实现的 [Ryu](https://github.com/ulfjack/ryu) ，一个高性能浮点数转换为字符串的算法。


Ryu, implemented by C#, a high-performance algorithm for converting floats to strings.



##### 2023-09-21 RyuCsharp update descriptions:

- To avoid managed memory fragmentation, the implementation was changed from pointers to references. (The difference between pointer and reference is that the former is an unmanaged pointer and the latter is a managed pointer.)
- Inherit the open source license of the parent project to make it easier for everyone to use.
