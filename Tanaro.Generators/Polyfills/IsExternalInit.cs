// Polyfill required to declare init-only properties (used by positional records) when targeting netstandard2.0.
// ReSharper disable UnusedMember.Global
#pragma warning disable IDE0130
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;
