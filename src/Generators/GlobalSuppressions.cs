// Centralized code-analysis suppressions for the Generators project.
using System.Diagnostics.CodeAnalysis;

// IsExternalInit polyfill — the namespace must be System.Runtime.CompilerServices for the
// compiler to recognize it, which deliberately violates the folder-structure rule.
[assembly: SuppressMessage(
    "Style",
    "IDE0130:Namespace does not match folder structure",
    Scope = "namespace",
    Target = "~N:System.Runtime.CompilerServices",
    Justification = "Namespace must be System.Runtime.CompilerServices for the compiler to recognize this polyfill.")]
