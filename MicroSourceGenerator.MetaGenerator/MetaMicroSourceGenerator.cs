using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace MicroSourceGenerator.MetaGenerator;

[Generator]
public partial class MetaMicroSourceGenerator : IIncrementalGenerator
{
    struct Bundle
    {
        public IMicroSourceGenerator Generator { get; init; }
        public MicroSourceGenerationArg Data { get; init; }
    }
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ProductInitialCode);
        context.RegisterSourceOutput(CreateProvider(context), ProductSource);
    }

    static string AttributeFullName => "MicroSourceGenerator.Attributes.MicroSourceGeneratorAttribute";
    static (SyntaxList<UsingDirectiveSyntax> Usings, SyntaxList<ExternAliasDirectiveSyntax> Externs) GetUsingOrExtern(CompilationUnitSyntax syntax)
    {
        var usings = syntax.Usings.Concat(syntax.ChildNodes().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(n => n.Usings));
        var externs = syntax.Externs.Concat(syntax.ChildNodes().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(n => n.Externs));
        return (new(usings), new(externs));
    }
    static IncrementalValuesProvider<Bundle> CreateProvider(IncrementalGeneratorInitializationContext context)
    {
        var generatorProvider = context.SyntaxProvider.CreateSyntaxProvider(
             static (node, token) =>
             {
                 token.ThrowIfCancellationRequested();
                 return node is TypeDeclarationSyntax { AttributeLists.Count: > 0 };
             },
             static (context, token) =>
             {
                 token.ThrowIfCancellationRequested();
                 var syntax = (context.Node as TypeDeclarationSyntax)!;
                 if (context.SemanticModel.GetDeclaredSymbol(syntax, token) is not INamedTypeSymbol symbol) return default;
                 var attributes = symbol.GetAttributes();
                 return (syntax, symbol, attributes, context.SemanticModel);
             })
             .Select(static (pair, token) =>
             {
                 token.ThrowIfCancellationRequested();
                 var (syntax, symbol, attributes, semanticModel) = pair;
                 var attributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(AttributeFullName) ?? throw new NullReferenceException("maker attribute was not found.");

                 if (!attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol))) return default;

                 return new MicroGeneratorInfo
                 {
                     Syntax = syntax,
                     Symbol = symbol,
                     Attributes = attributes,
                     SemanticModel = semanticModel,
                 };
             })
             .WithComparer(MicroGeneratorInfo.Comparer)
             .Collect()
             .Combine(context.CompilationProvider)
             .Select((pair, token) =>
             {
                 token.ThrowIfCancellationRequested();
                 var (generatorInfos, sourceCompilation) = pair;
                 var compilation = CreateCompilation((sourceCompilation as CSharpCompilation)!, generatorInfos);
                 var generators = CreateGenerators(compilation);
                 var diagnostics = compilation.GetDiagnostics();
                 return (generators, diagnostics);
             });

        var generatorSyntaxProvider = context.SyntaxProvider.CreateSyntaxProvider(
            (node, token) => true,
            (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                return context;
            })
            .Combine(generatorProvider).Combine(context.CompilationProvider)
            .SelectMany((pair, token) =>
            {
                token.ThrowIfCancellationRequested();
                var ((context, (generators, diagnostics)), compilation) = pair;
                var data = new MicroSourceGenerationArg()
                {
                    SemanticModel = context.SemanticModel,
                    Compilation = compilation,
                    Node = context.Node,
                };
                return generators.Where(g => g.Accept(data)).Select(g => new Bundle { Data = data, Generator = g });
            });

        return generatorSyntaxProvider;
    }
    static CSharpCompilation CreateCompilation(CSharpCompilation sourceCompilation, ImmutableArray<MicroGeneratorInfo> generatorInfos)
    {
        var syntaxTrees = generatorInfos.Select(info =>
        {
            var root = info.Syntax.SyntaxTree.GetCompilationUnitRoot();
            var (usings, externs) = GetUsingOrExtern(root);
            var compilationUnit = SyntaxFactory.CompilationUnit(externs, usings, root.AttributeLists, new(info.Syntax));
            return SyntaxFactory.SyntaxTree(compilationUnit);
        });
        var compilation = CSharpCompilation.Create(
            $"MicroGenerators.g.dll",
            syntaxTrees,
            sourceCompilation.References,
            sourceCompilation.Options
        );
        return compilation;
    }
    static ImmutableArray<IMicroSourceGenerator> CreateGenerators(CSharpCompilation compilation)
    {
        var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) return ImmutableArray.Create<IMicroSourceGenerator>();
        stream.Seek(0, SeekOrigin.Begin);
        var asm = Assembly.Load(stream.ToArray());
        var generators = asm.GetTypes()
                            .Select(type => Activator.CreateInstance(type) as IMicroSourceGenerator)
                            .OfType<IMicroSourceGenerator>();
        return ImmutableArray.CreateRange(generators);
    }
    static void ProductInitialCode(IncrementalGeneratorPostInitializationContext context)
    {
        var builder = new StringBuilder();
        builder.Append(@"
namespace MicroSourceGenerator.Attributes
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct)]
    class MicroSourceGeneratorAttribute : global::System.Attribute
    {
        
    }
}
");
        context.AddSource("MetaMicroSourceGeneratorAttributes.g.cs", builder.ToString());
    }
    static void ProductSource(SourceProductionContext context, Bundle bundle)
    {
        bundle.Generator.ProductSource(context, bundle.Data);
    }
}

interface IMicroSourceGenerator
{
    public bool Accept(MicroSourceGenerationArg arg);
    public void ProductSource(SourceProductionContext context, MicroSourceGenerationArg arg);
}

struct MicroSourceGenerationArg
{
    public SemanticModel SemanticModel { get; init; }
    public Compilation Compilation { get; init; }
    public SyntaxNode Node { get; init; }
}