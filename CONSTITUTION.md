# The LFGL Constitution
**Strict Engineering Guidelines for LFGL (Luty Friendly Game Launcher)**

### 1. Performance First (Zero-Allocation Principle)
- **Span<T> Over Arrays:** Avoid creating new arrays or lists in hot paths. Use `Span<T>`, `ReadOnlySpan<T>`, or `Memory<T>` for all string and buffer manipulations.
- **Frozen Collections:** For read-only lookups (e.g., launcher paths, game metadata), ALWAYS use `.ToFrozenDictionary()` or `.ToFrozenSet()` (introduced in .NET 8, refined in .NET 10).
- **Structs over Classes:** Use `readonly record struct` for small data models (like `GameModel`) to minimize GC pressure, unless reference semantics are strictly required.

### 2. UI/UX: The "Glass" Standard
- **Mica & Acrylic:** The `MainWindow` MUST use `SystemBackdrop` (Mica or Acrylic). No solid opaque backgrounds for the main shell.
- **60fps Mandate:** Long-running operations (scanning, API calls) MUST run off the UI thread. Use `Task.Run` or proper `async/await` flows so the UI never freezes.
- **Visuals:** Animations should be "snappy" (duration < 200ms). Use `CompositionAnimation` for high-performance motion if XAML Storyboards drop types.

### 3. Architecture & Separation
- **Dependency Injection:** Use `Microsoft.Extensions.DependencyInjection`. All services (e.g., `IGameScannerService`) must be registered in the container. No static "Manager" classes for stateful logic.
- **Feature Isolation:** Group code by feature, not just type (e.g., `Features/Scanning` containing Service, Models, and Helpers), rather than giant `Models` or `Services` folders, unless shared globally.

### 4. Code Style (C# 14 Modernity)
- **File-Scoped Namespaces:** Always use `namespace LFGL;` (no indentation).
- **Primary Constructors:** Use `public class MyService(ILogger logger)` instead of defining a separate constructor body for simple assignments.
- **Global Usings:** Keep common namespaces (`System`, `System.Collections.Generic`) in a `GlobalUsings.cs` file to reduce noise.

### 5. Rider & Environment Strictness
- **Editor:** JetBrains Rider is the primary IDE.
- **Cleanup:** Configure "File Layout" to group Fields, Constructors, Properties, Methods automatically.
- **Plugins:** "XAML Styler" is recommended to keep XAML sorted and readable. "CognitiveComplexity" plugin usage is encouraged to keep methods simple.

### 6. Error Handling
- **No Silent Failures:** Scanning errors should be logged or displayed gracefully (e.g., "Scanner unavailable") but must never crash the app. Use `Result<T>` pattern or explicit `try/catch` in service boundaries, never swallow exceptions without logging.

*Signed: The LFGL Architect*
