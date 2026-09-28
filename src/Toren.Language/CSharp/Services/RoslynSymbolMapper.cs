using Microsoft.CodeAnalysis;
using Toren.Language.CSharp.Models;

namespace Toren.Language.CSharp.Services;

internal static class RoslynSymbolMapper
{
    public static CSharpSymbolKind MapKind(SymbolKind kind) => kind switch
    {
        SymbolKind.Namespace => CSharpSymbolKind.Namespace,
        SymbolKind.NamedType or SymbolKind.TypeParameter => CSharpSymbolKind.Type,
        SymbolKind.Method => CSharpSymbolKind.Method,
        SymbolKind.Property => CSharpSymbolKind.Property,
        SymbolKind.Field => CSharpSymbolKind.Field,
        SymbolKind.Event => CSharpSymbolKind.Event,
        SymbolKind.Parameter => CSharpSymbolKind.Parameter,
        SymbolKind.Local => CSharpSymbolKind.Local,
        _ => CSharpSymbolKind.Other,
    };
}
