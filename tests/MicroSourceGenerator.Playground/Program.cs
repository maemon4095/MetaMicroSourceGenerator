using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MicroSourceGenerator;
using System.Text;

Console.WriteLine();


[GenerateComment]
partial class A
{

}

//これにもマーカーをくっつける必要がある．
[MicroGeneratorDependency]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
class GenerateCommentAttribute : Attribute
{

}

[MicroSourceGenerator]
class CommentGenerator : IMicroSourceGenerator
{
    SemanticModel SemanticModel { get; set; }
    INamedTypeSymbol AttributeSymbol { get; set; }

    public void Initialize(SemanticModel semanticModel)
    {
        this.SemanticModel = semanticModel;
        this.AttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(typeof(GenerateCommentAttribute).FullName ?? throw new NullReferenceException("fullname is null")) ?? throw new NullReferenceException("cannot find generator comment");
    }
    public bool Accept(SyntaxNode node)
    {
        if (node is not TypeDeclarationSyntax decl) return false;
        if (this.SemanticModel.GetDeclaredSymbol(decl) is not INamedTypeSymbol symbol) return false;
        return symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(this.AttributeSymbol, a.AttributeClass));
    }
    public void ProductSource(SourceProductionContext context, SyntaxNode node)
    {
        var syntax = (node as TypeDeclarationSyntax)!;
        var symbol = this.SemanticModel.GetDeclaredSymbol(syntax) as INamedTypeSymbol ?? throw new NullReferenceException();
        var builder = new StringBuilder();

        if (!symbol.ContainingNamespace.IsGlobalNamespace)
        {
            builder.Append("namespace ").Append(symbol.ContainingNamespace.ToString()).AppendLine(";");
        }

        builder.Append("partial ").Append(symbol.IsReferenceType ? "class " : "struct ").AppendLine(symbol.Name)
               .AppendLine("{")
               .AppendLine("// Hello From Micro Source Generator")
               .AppendLine("}");


        context.AddSource($"{symbol.Name}.g.cs", builder.ToString());
    }
}