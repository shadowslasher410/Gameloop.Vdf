```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 9700X 3.80GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v4


```
| Method            | DataSize | Mean      | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|------------------ |--------- |----------:|----------:|----------:|--------:|-------:|----------:|
| DeserializeSk2    | 100      |  6.736 μs | 0.0098 μs | 0.0091 μs |  1.5869 | 0.1144 |  25.95 KB |
| DeserializeVdfNet | 100      | 18.206 μs | 0.0297 μs | 0.0263 μs |  2.4414 | 0.2136 |  40.17 KB |
| DeserializeVdfNet | 1000     | 59.248 μs | 0.1762 μs | 0.1648 μs | 14.4043 | 6.4697 | 237.06 KB |
| DeserializeSk2    | 1000     | 70.312 μs | 0.1180 μs | 0.1104 μs | 13.5498 | 4.5166 | 222.83 KB |
