# Changelog

## - 2026-05-06
### Added
- **Linux/Unix Support**: Added comprehensive support for Linux and Unix platforms, including handling of platform-specific path separators and line endings.
- **Multi-Dialect Support**: Native support for **KV1** (including binary format), **KV2** (including `#base` inclusion), and **KV3** (JSON-like syntax) via the `KeyValuesFormat` setting.
- **SIMD Acceleration**: Integrated `SearchValues<char>` in `VdfTextReader` and `VdfTextWriter` for vectorized character scanning (AVX2/SSE), significantly increasing parsing throughput.
- **KV3 Native Features**: Added `TypeHint` to `VValue` for explicit typing (e.g., `int:7`, `resource:"path"`) and implemented true `[]` array support.
- **Modern JSON Bridge**: Introduced a high-performance bridge to `System.Text.Json` via `JsonNode` and `JsonElement` extensions, supporting recursive merging and dialect-aware conversion. This makes Gameloop.Vdf.JsonConverter obsolete.
- **Async I/O**: Implemented `IAsyncDisposable` in `VdfWriter` to support non-blocking stream flushing in modern asynchronous pipelines.
- **Tree Navigation**: Added `Root` and `Path` properties to `VToken` for easier tree traversal and debugging.

### Changed
- Retargeted project to use .NET 10 and C# 14.
- **Memory Efficiency**: Replaced internal string manipulation with `ReadOnlySpan<char>` and `stackalloc` where appropriate to minimize heap allocations.
- **Standardized Exception Handling**: Consolidated custom validation logic into native .NET 10 `ArgumentNullException.ThrowIfNull` helpers.
- **Refined State Machine**: Unified the `State` enums across Readers and Writers to support complex structural tokens (Arrays/Conditionals).
- **Primary Constructors**: Migrated all core classes to Primary Constructor syntax for reduced metadata overhead and cleaner IL generation.
- **Test Suite Overhaul**: Rewrote unit tests using xUnit and FluentAssertions.
- **Changelog**: Changed to a .MD format and added entry for this release.

### Removed
- **Legacy Dependencies**: Removed `Newtonsoft.Json` as the primary JSON engine in favor of the built-in `System.Text.Json`.
- **Obsolete Utilities**: Deleted `CollectionUtils.cs`, `ReflectionUtils.cs`, and `NullableAttributes.cs` as their functionality is now native to the .NET 10 runtime.
- **Interface Overhead**: Removed `IVEnumerable<T>` to eliminate virtual call overhead in hot loops.

### Fixed
- Fixed DLR metadata warnings in `DynamicProxyMetaObject` using C# 14 unbound generic `nameof` syntax.
- Fixed string escaping logic in `VdfTextWriter` to utilize high-speed vectorized lookups.
- Corrected linked-list pointer maintenance in `VObject` to prevent `NullReferenceException` during complex tree mutations.

## - 2022-03-05
### Changed
- Updated `MaximumTokenSize` to be adjustable in settings.

## - 2020-06-20
### Fixed
- Fixed `VdfTextReader` treating slashes in quoted values as comments.

## - 2020-05-29
### Added
- Added comment serialization and deserialization support.
- Added `DeepClone` method to `VToken`.
- Added `VToken.DeepEquals` for deep comparison.
- Added support for C# 8's nullable reference types.

### Changed (Breaking)
- `VObject.Children()` now returns an `IEnumerable<VToken>` (was `IEnumerable<VProperty>`).
- Removed `VProperty` parameterless constructor.
- `VObject` dictionary indexer now throws `KeyNotFoundException` instead of returning null.

## - 2016-07-30
### Added
- Added `VToken.Value<T>` accessors.
- Added `IDictionary<string, VToken>` implementation to `VObject`.

### Changed
- Moved `VToken` and subtypes to `Gameloop.Vdf.Linq` namespace.

## - 2017-08-10
### Added
- Added `VdfSerializerSettings.Common` settings preset.

### Changed
- Re-targeted project to .NET Standard 1.0.
- Fixed `VdfConvert.Deserialize` return type to `VProperty`.

## - 2016-03-11
### Added
- Initial release.
