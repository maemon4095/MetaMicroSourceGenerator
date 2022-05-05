using Microsoft.CodeAnalysis;

namespace MicroSourceGenerator;

public interface IMicroSourceGenerator
{
    public bool Accept(SemanticModel semanticModel, SyntaxNode node);
    public void ProductSource(SourceProductionContext context, SemanticModel semanticModel, SyntaxNode node);
}
