using Microsoft.CodeAnalysis;

namespace MicroSourceGenerator.MetaGenerator;

internal static class SyntaxNodeComparer
{
    private class DefaultComparer : IEqualityComparer<SyntaxNode>
    {
        public bool Equals(SyntaxNode x, SyntaxNode y) => x.IsEquivalentTo(y);
        public int GetHashCode(SyntaxNode obj) => obj.GetHashCode();
    }

    public static IEqualityComparer<SyntaxNode> Default { get; } = new DefaultComparer();
}