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
        public IEnumerable<IMicroSourceGenerator> Generators { get; init; }
        public IEnumerable<Diagnostic> Diagnostics { get; init; }
        public SyntaxNode SyntaxNode { get; init; }
    }
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ProductInitialCode);
        context.RegisterSourceOutput(CreateProvider(context), ProductSource);
    }

    static string Namespace => "MicroSourceGenerator";
    static string GeneratorAttributeFullName => $"{Namespace}.MicroSourceGeneratorAttribute";
    static string DependencyAttributeFullName => $"{Namespace}.MicroGeneratorDependencyAttribute";

    static void ProductInitialCode(IncrementalGeneratorPostInitializationContext context)
    {
        var builder = new StringBuilder();
        builder.Append(@"
namespace MicroSourceGenerator
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct)]
    class MicroSourceGeneratorAttribute : global::System.Attribute
    {
        
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct)]
    class MicroGeneratorDependencyAttribute : global::System.Attribute
    {
        
    }
}
");
        context.AddSource("MicroSourceGeneratorAttributes.g.cs", builder.ToString());
    }

    static (SyntaxList<UsingDirectiveSyntax> Usings, SyntaxList<ExternAliasDirectiveSyntax> Externs) GetUsingOrExtern(CompilationUnitSyntax syntax)
    {
        var usings = syntax.Usings.Concat(syntax.ChildNodes().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(n => n.Usings));
        var externs = syntax.Externs.Concat(syntax.ChildNodes().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(n => n.Externs));
        return (new(usings), new(externs));
    }

    static IncrementalValueProvider<(ImmutableArray<IMicroSourceGenerator>, IEnumerable<Diagnostic>)> CreateGeneratorProvider(IncrementalGeneratorInitializationContext context)
    {
        var usingProvider = CreateGlobalUsingProvider(context);
        return context.SyntaxProvider.CreateSyntaxProvider(
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
             .Where(pair => pair != default)
             .Select(static (pair, token) =>
             {
                 token.ThrowIfCancellationRequested();
                 var (syntax, symbol, attributes, semanticModel) = pair;
                 var generatorAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(GeneratorAttributeFullName) ?? throw new NullReferenceException("marker attribute was not found.");
                 var dependencyAttributeSymbol = semanticModel.Compilation.GetTypeByMetadataName(DependencyAttributeFullName) ?? throw new NullReferenceException("marker attribute was not found.");

                 var isGeneratorItem = attributes.Any(a =>
                 {
                     var comparer = SymbolEqualityComparer.Default;
                     return comparer.Equals(a.AttributeClass, generatorAttributeSymbol) || comparer.Equals(a.AttributeClass, dependencyAttributeSymbol);
                 });
                 if (!isGeneratorItem) return default;

                 var attributeSyntax = syntax.ChildNodes()
                    .OfType<AttributeListSyntax>().SelectMany(l => l.Attributes)
                    .First(a =>
                    {
                        var symbol = semanticModel.GetSymbolInfo(a).Symbol?.ContainingType;
                        var comparer = SymbolEqualityComparer.Default;
                        return comparer.Equals(symbol, generatorAttributeSymbol) || comparer.Equals(symbol, dependencyAttributeSymbol);
                    });

                 return new MicroGeneratorInfo
                 {
                     SyntaxTree = syntax.SyntaxTree,
                     Syntax = syntax.RemoveNode(attributeSyntax, SyntaxRemoveOptions.KeepNoTrivia) ?? throw new Exception(),
                     Symbol = symbol,
                     Attributes = attributes,
                     SemanticModel = semanticModel,
                 };
             })
             .WithComparer(MicroGeneratorInfo.Comparer)
             .Collect()
             .Combine(usingProvider)
             .Combine(context.CompilationProvider)
             .Select((pair, token) =>
             {
                 //generatorInfos contains default(MicroGeneratorInfo) so we should do null check. It seems mixed at Collect method and that might be a compiler bug.
                 token.ThrowIfCancellationRequested();
                 var ((generatorInfos, usings), sourceCompilation) = pair;
                 var compilation = CreateCompilation((sourceCompilation as CSharpCompilation)!, usings, generatorInfos);
                 var (generators, emitDiagnostics) = CreateGenerators(compilation);
                 var diagnostics = compilation.GetDiagnostics();
                 return (generators, diagnostics.Concat(emitDiagnostics));
             });
    }

    static IncrementalValueProvider<SyntaxTree> CreateGlobalUsingProvider(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider.CreateSyntaxProvider(
            (node, token) =>
            {
                token.ThrowIfCancellationRequested();
                return node is UsingDirectiveSyntax;
            },
            (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                var syntax = (context.Node as UsingDirectiveSyntax)!;
                return syntax;
            })
            .WithComparer(SyntaxNodeComparer.Default)
            .Where(syntax => !string.IsNullOrWhiteSpace(syntax.GlobalKeyword.ValueText))
            .Collect()
            .Select((usings, token) =>
            {
                token.ThrowIfCancellationRequested();
                var unit = SyntaxFactory.CompilationUnit().WithUsings(new(usings));
                return SyntaxFactory.SyntaxTree(unit);
            });
    }

    static IncrementalValuesProvider<Bundle> CreateProvider(IncrementalGeneratorInitializationContext context)
    {
        var generatorProvider = CreateGeneratorProvider(context);

        var generatorSyntaxProvider = context.SyntaxProvider.CreateSyntaxProvider(
            (node, token) => true,
            (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                return context;
            })
            .Combine(generatorProvider).Combine(context.CompilationProvider)
            .Select((pair, token) =>
            {
                token.ThrowIfCancellationRequested();
                var ((context, (generators, diagnostics)), compilation) = pair;

                var genOrDiags = generators.Select(gen =>
                {
                    try
                    {
                        gen.Initialize(context.SemanticModel);
                        return (object)gen;
                    }
                    catch (Exception ex)
                    {
                        return Diagnostic.Create(DiagnosticHelper.GeneratorInitializationError, Location.None, gen, ex.GetType(), ex.StackTrace, ex.Message);
                    }
                });

                return new Bundle
                {
                    SyntaxNode = context.Node,
                    Diagnostics = genOrDiags.OfType<Diagnostic>().Concat(diagnostics),
                    Generators = genOrDiags.OfType<IMicroSourceGenerator>(),
                };
            });

        return generatorSyntaxProvider;
    }

    static CSharpCompilation CreateCompilation(CSharpCompilation sourceCompilation, SyntaxTree usings, ImmutableArray<MicroGeneratorInfo> generatorInfos)
    {
        try
        {
            var syntaxTrees = generatorInfos.Where(info => info.Syntax is not null)
                                            .Select(info =>
                                            {
                                                var root = info.SyntaxTree.GetCompilationUnitRoot();
                                                var (usings, externs) = GetUsingOrExtern(root);
                                                var compilationUnit = SyntaxFactory.CompilationUnit(externs, usings, root.AttributeLists, new(info.Syntax));
                                                return SyntaxFactory.SyntaxTree(compilationUnit);
                                            })
                                            .Concat(Enumerable.Repeat(usings, 1));

            var compilation = CSharpCompilation.Create(
                $"MicroGenerators.g.dll",
                syntaxTrees,
                AppDomain.CurrentDomain.GetAssemblies().Select(asm => MetadataReference.CreateFromFile(asm.Location)),
                sourceCompilation.Options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary)
            );

            return compilation;
        }
        catch (Exception ex)
        {
            throw new Exception($"{ex.GetType().Name} was thrown in source generation. StackTrace : {ex.StackTrace} Message : {ex.Message}", ex);
        }
    }

    static (ImmutableArray<IMicroSourceGenerator> Generators, ImmutableArray<Diagnostic> Diagnostics) CreateGenerators(CSharpCompilation compilation)
    {
        try
        {
            var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            if (!result.Success)
            {
                return (ImmutableArray.Create<IMicroSourceGenerator>(), result.Diagnostics);
            }
            stream.Seek(0, SeekOrigin.Begin);
            var asm = Assembly.Load(stream.ToArray());
            var generators = asm.GetTypes().Where(type => typeof(IMicroSourceGenerator).IsAssignableFrom(type)).Select(Activator.CreateInstance).OfType<IMicroSourceGenerator>();

            return (ImmutableArray.CreateRange(generators), result.Diagnostics);
        }
        catch (Exception ex)
        {
            throw new Exception($"{ex.GetType().Name} was thrown in source generation. Message : {ex.Message}", ex);
        }
    }

    static void ProductSource(SourceProductionContext context, Bundle bundle)
    {
        foreach (var diganostic in bundle.Diagnostics)
        {
            context.ReportDiagnostic(diganostic);
        }
        var syntaxNode = bundle.SyntaxNode;

        foreach (var generator in bundle.Generators)
        {
            try
            {
                if (!generator.Accept(syntaxNode)) continue;
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelper.SyntaxFilteringError, syntaxNode.GetLocation(), generator, ex.GetType(), ex.StackTrace, ex.Message));
                continue;
            }

            try
            {
                generator.ProductSource(context, syntaxNode);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticHelper.SourceProductionError, syntaxNode.GetLocation(), generator, ex.GetType(), ex.StackTrace, ex.Message));
                continue;
            }
        }
    }
}