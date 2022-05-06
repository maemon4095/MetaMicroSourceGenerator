using Microsoft.CodeAnalysis;

namespace MicroSourceGenerator;

public interface IMicroSourceGenerator
{
    public void Initialize(SemanticModel semanticModel);
    public bool Accept(SyntaxNode node);
    public void ProductSource(SourceProductionContext context, SyntaxNode node);
}
