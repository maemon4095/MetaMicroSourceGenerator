using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace MetaMicroSourceGenerator;

[Generator]
public partial class MetaMicroSourceGenerator : IIncrementalGenerator
{
    struct Bundle
    {

    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ProductInitialCode);
        context.RegisterSourceOutput(CreateValuesProvider(context), ProductSource);
    }

    static IncrementalValuesProvider<Bundle> CreateValuesProvider(IncrementalGeneratorInitializationContext context)
    {

    }

    static void ProductInitialCode(IncrementalGeneratorPostInitializationContext context)
    {
        var builder = new StringBuilder();
        builder.Append(@"
namespace MetaMicroSourceGenerator.Attributes
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

    }
}
