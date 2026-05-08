# Vdf.NET

[![NuGet](https://shields.io)](https://nuget.org)

A high-performance, easy-to-use Valve Data Format (VDF) parser for .NET, supporting KV1, KV2, and KV3.

## Features

- **Blazing Fast**: Optimized using `ReadOnlySpan<char>` and `SearchValues` for vectorized parsing.
- **Multi-Format Support**: Full compatibility with **KV1**, **KV2**, and **KV3**.
- **Native JSON Support**: Built-in conversion to and from `System.Text.Json` nodes.
- **Dynamic Support**: Access VDF structures naturally using C# `dynamic`.
- **Conditionals**: Native evaluation of platform conditionals like `[$WINDOWS || $LINUX]`.

## Installation

```bash
dotnet add package Gameloop.Vdf
```

## Quick Start

### Basic Deserialization
```csharp
using Gameloop.Vdf;

string vdf = File.ReadAllText("config.vdf");
dynamic root = VdfConvert.Deserialize(vdf);

// Access values directly
Console.WriteLine(root.Value.SteamDefaultDialog); 
```

### Specifying Formats (KV1, KV2, KV3)
```csharp
var settings = new VdfSerializerSettings 
{ 
    Format = KeyValuesFormat.Kv3,
    UsesEscapeSequences = true
};

string output = VdfConvert.Serialize(myProperty, settings);
```

## Supported Escapes

When `UsesEscapeSequences` is enabled, Vdf.NET handles the following C-style escape characters commonly used in Source Engine files:


| Escape | Character | Description |
| :--- | :--- | :--- |
| `\n` | `0x0A` | New Line |
| `\r` | `0x0D` | Carriage Return |
| `\t` | `0x09` | Horizontal Tab |
| `\v` | `0x0B` | Vertical Tab |
| `\b` | `0x08` | Backspace |
| `\f` | `0x0C` | Form Feed |
| `\a` | `0x07` | Bell |
| `\\` | `0x5C` | Backslash |
| `\"` | `0x22` | Double Quote |
| `\'` | `0x27` | Single Quote |
| `\?` | `0x3F` | Question Mark |

## License

Vdf.NET is released under the [MIT license](https://opensource.org).