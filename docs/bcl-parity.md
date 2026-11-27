# Parity with the BCL and the C# ecosystem

What a C# programmer reaches for in the BCL or in a common package, and where CΔ answers it. The
answer is one of:

- **std**: a module of [std.md](std.md), with the BCL's names where it keeps their meaning (D149);
- **language**: a construct or comptime replaces the library;
- **package**: a projection outside the std, accepting the library's own rules (D173);
- **open**: not decided; the candidates are where the decision starts.

An area this document lacks is a gap to add here, not a limit.

## Base library

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `Span<T>`, `Memory<T>`, `MemoryExtensions` | `Span<T>`, `Std.Mem` | std | builtins |
| `Array.Sort`, `BinarySearch`, `Reverse`, `Fill` on spans | | open | CΔ, generic over `IComparer<T>` |
| `List<T>`, `Dictionary`, `HashSet`, `Queue`, `Stack` | `Std.Collections` | std | CΔ |
| `SortedDictionary`, `SortedSet`, `PriorityQueue`, `LinkedList`, `BitArray`, a deque | | open | CΔ |
| `FrozenDictionary`, `FrozenSet` | | open | CΔ, a perfect hash built at comptime |
| `ImmutableArray`, immutable collections | | open | CΔ |
| `ArrayPool`, `ObjectPool` | `Pool<T>`, `ObjectPool<T>` | std | CΔ |
| LINQ | `Std.Linq` | std | CΔ |
| reflection, `Activator` | comptime `TypeInfo` (D176) | language | |
| source generators | Roslyn generators (D023) | language | |
| `Convert` (Base64, hex), `BinaryPrimitives`, `BitConverter` | | open | CΔ |
| `Guid`, `Uri`, `Version` | | open | CΔ |
| `Console` (lines, keys, colors, size, redirection) | `Print` only | open | CΔ over termios and the Win32 console |
| `Environment`, `Process`, exit codes | `Std.Process` | std | libc, Win32 |
| `PosixSignalRegistration`, Ctrl+C | | open | libc, Win32 |
| `NativeLibrary.Load` | | open | `dlopen`, `LoadLibrary` |
| `System.CommandLine` | | open | CΔ, options from a struct through `TypeInfo`, help built at comptime |

## Text

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `string`, `StringBuilder`, `Rune` | UTF-8 `string`, `Std.Text` | std | CΔ |
| interpolation, `string.Format`, `ToString(format)` | `Std.Fmt`, specifiers checked at comptime | std | CΔ |
| `int.Parse` and the other numbers | `Std.Text` | std | CΔ |
| `Encoding` (UTF-16, Latin-1) | | open | CΔ; UTF-16 is needed by the wide Win32 API |
| `Regex` | `Std.Regex` | std | PCRE2 |
| normalization, case folding | | open | utf8proc |
| `CultureInfo`, collation, formatting by locale | | open | a package over ICU, whose memory hook is global |
| `System.Text.Json` | `Std.Json` | std | yyjson |
| `System.Xml` | | open | expat, which takes an allocator per parser; writing in CΔ |
| CSV, TOML, YAML | | open | CΔ for CSV; tomlc99 and libyaml as packages |
| templates (Razor, Scriban) | | open | a package: the template read with `Embed` and compiled by a generator |

## Numbers

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `Math`, `MathF`, checked arithmetic | `Std.Math` | std | libm, builtins |
| generic math (`INumber<T>`) | static abstract members (D153) | language | |
| `Random` | `Std.Random` | std | CΔ |
| `Vector2`, `Vector3`, `Vector4`, `Matrix4x4`, `Quaternion`, `Plane` | | open | CΔ |
| `Vector<T>`, `Vector128<T>` and the intrinsics | SIMD vectors, later | std | CΔ |
| `BigInteger` | | open | libtommath |
| `Complex`, `Half`, `decimal` | | open | CΔ |

## Time

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `DateTime` (UTC), `TimeSpan`, `Stopwatch` | `Std.Time` | std | `clock_gettime`, Win32 |
| `TimeZoneInfo`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, ISO 8601 | | open | the system zoneinfo on Unix, the Win32 API |
| `Timer`, `PeriodicTimer` | `Std.Threading.Tasks.Timer` | std | CΔ |

## Files and I/O

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `File`, `Directory`, `Path`, `Stream` | `Std.Fs`, `Std.Io` | std | libc, Win32 |
| `MemoryMappedFile` | | open | `mmap`, `CreateFileMapping` |
| pipes, temporary files | | open | libc, Win32 |
| `FileSystemWatcher` | | open | inotify, `ReadDirectoryChangesW`, FSEvents |
| `System.IO.Compression` | `Std.Compress` | std | zlib, zstd |

## Concurrency

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `Thread`, locks, `Interlocked`, events | `Std.Threading` | std | OS |
| `Task`, `Parallel`, `async` | `IIo`, `Task<T>` (D152) | std | CΔ |
| `Channel<T>`, concurrent collections | `Std.Threading.Channels`, `Std.Collections.Concurrent` | std | CΔ |

## Networking and security

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `Socket`, `Dns`, `IPAddress`, `SslStream` | `Std.Net` | std | sockets, Winsock, BearSSL |
| `HttpClient` | | open | HTTP/1.1 in CΔ over `Std.Net`; curl as a package, whose memory hook is global |
| `ClientWebSocket` | | open | CΔ over `Std.Net` |
| hashes, HMAC, ChaCha20-Poly1305, X25519, Ed25519 | `Std.Crypto` | std | BearSSL, Monocypher |
| AES-GCM, RSA, ECDSA, X.509 validation, Argon2, PBKDF2 | | open | BearSSL, Monocypher |

## Diagnostics and testing

| .NET | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| `Debug`, `Trace`, stack traces | `Std.Debug`, error return traces | std | libc |
| the error return trace as data, for a logger to format | `PrintErrorTrace()` writes it only | open | CΔ, entries of file and line |
| `ILogger` | | open | CΔ, levels decided at comptime so calls below them leave the binary |
| metrics, `EventSource`, tracing | | open | a package |
| xUnit, NUnit | `[Test]`, `Std.Testing` | std | CΔ |
| BenchmarkDotNet | | open | `[Benchmark]` in `cdc test` |

## Ecosystem

| C# | CΔ | Answer | Basis or candidates |
|---|---|---|---|
| ASP.NET Core minimal APIs | a web package | package | facil.io ([roadmap.md](roadmap.md#later)) |
| ADO.NET, Dapper, migrations | a data package | package | ODBC, SQLite, libpq ([roadmap.md](roadmap.md#later)) |
| Avalonia, GTK# | a desktop UI package | package | GTK4, libadwaita ([roadmap.md](roadmap.md#later)) |
| mobile UI | | open | Qt with QML, SDL3; needs the Android and iOS targets |
| ImageSharp, SkiaSharp | | open | stb_image, libpng, libjpeg-turbo, Skia through `libSkiaSharp` |
| fonts and text shaping | | open | FreeType, HarfBuzz |
| audio | | open | miniaudio, which takes an allocator per context |
| physics, game frameworks | | open | Box2D, Jolt through JoltC, SDL3 |
| protobuf, MessagePack, FlatBuffers | | open | nanopb, mpack, flatcc |
| Kafka, RabbitMQ, NATS, Redis clients | | open | librdkafka, rabbitmq-c, nats.c, hiredis |
| AWS SDK | | open | aws-c-*, whose allocator is per context |
| configuration (`Microsoft.Extensions.Configuration`) | | open | CΔ over `Std.Json`, the environment and the arguments |
| dependency injection | explicit passing | language | |
| NuGet | NuGet, fetched C libraries, and the package monorepo of recipes and manifests (D164) | package | [roadmap.md](roadmap.md#later) |
