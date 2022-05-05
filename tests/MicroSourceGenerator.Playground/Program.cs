using Microsoft.CodeAnalysis;
using MicroSourceGenerator;

Console.WriteLine();

[MicroSourceGenerator]
class CommentGenerator : IMicroSourceGenerator
{
    public bool Accept(MicroSourceGenerationArg arg)
    {
        throw new NotImplementedException();
    }
    public void ProductSource(SourceProductionContext context, MicroSourceGenerationArg arg) => throw new NotImplementedException();
}