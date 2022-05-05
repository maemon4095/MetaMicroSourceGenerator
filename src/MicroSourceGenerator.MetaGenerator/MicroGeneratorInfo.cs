using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace MicroSourceGenerator.MetaGenerator;

readonly struct MicroGeneratorInfo
{
    class EqualityComparer : EqualityComparer<MicroGeneratorInfo>
    {
        public override bool Equals(MicroGeneratorInfo x, MicroGeneratorInfo y) => x.Syntax.IsEquivalentTo(y.Syntax);
        public override int GetHashCode(MicroGeneratorInfo obj) => HashCode.Combine(obj.Syntax);
    }

    public static IEqualityComparer<MicroGeneratorInfo> Comparer { get; } = new EqualityComparer();

    public CSharpCompilation Compilation => (this.SemanticModel.Compilation as CSharpCompilation)!;
    public SemanticModel SemanticModel { get; init; }
    public TypeDeclarationSyntax Syntax { get; init; }
    public INamedTypeSymbol Symbol { get; init; }
    public ImmutableArray<AttributeData> Attributes { get; init; }
    public SyntaxTree SyntaxTree { get; init; }
}
