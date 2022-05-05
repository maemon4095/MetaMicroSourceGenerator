using Microsoft.CodeAnalysis;
using MicroSourceGenerator;

Console.WriteLine();

[MicroSourceGenerator]
class CommentGenerator : IMicroSourceGenerator
{
    public bool Accept(SemanticModel semanticModel, SyntaxNode node)
    {
        throw new NotImplementedException();
    }

    public void ProductInitialSource(SourceProductionContext context) { }

    public void ProductSource(SourceProductionContext context, SemanticModel semanticModel, SyntaxNode node) => throw new NotImplementedException();
}