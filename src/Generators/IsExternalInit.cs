#pragma warning disable IDE0130 // Namespace must be System.Runtime.CompilerServices for the compiler to recognize this polyfill
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130

/// <summary>
/// This class is required by the C# compiler to support the 'record' and 'init' keywords
/// when targeting older frameworks like .NET Standard 2.0.
/// It must be defined in the 'System.Runtime.CompilerServices' namespace.
/// </summary>
internal static class IsExternalInit { }
