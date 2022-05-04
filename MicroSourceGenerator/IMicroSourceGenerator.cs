using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace MicroSourceGenerator;

public interface IMicroSourceGenerator
{
    public bool Accept(MicroSourceGenerationArg arg);
    public void ProductSource(SourceProductionContext context, MicroSourceGenerationArg arg);
}

public struct MicroSourceGenerationArg
{
    public SemanticModel SemanticModel { get; init; }
    public Compilation Compilation { get; init; }
    public SyntaxNode Node { get; init; }
}
