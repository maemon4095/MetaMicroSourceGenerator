using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace MicroSourceGenerator;

public interface IMicroSourceGenerator
{
    public bool Accept(SemanticModel semanticModel, SyntaxNode node);
    public void ProductSource(SourceProductionContext context, SemanticModel semanticModel, SyntaxNode node);
}
