// This polyfill enables C# 9+ init-only setters on netstandard2.0.
// It is a standard pattern used by many Oss libraries targeting netstandard2.0.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit { }
