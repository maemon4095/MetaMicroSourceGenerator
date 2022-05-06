using Microsoft.CodeAnalysis;

namespace MicroSourceGenerator.MetaGenerator;

static class DiagnosticHelper
{
    public static DiagnosticDescriptor SourceProductionError { get; } = new(
        id: "MSG001",
        title: "Source Production Failure",
        messageFormat: "Generator '{0}' failed to product source and {1} was thrown. StackTrace : {2}. Message : {3}.",
        category: "MicroSourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor SyntaxFilteringError { get; } = new(
        id: "MSG002",
        title: "Syntax Filtering Failure",
        messageFormat: "Generator '{0}' failed to filter syntax and {1} was thrown. StackTrace : {2}. Message : {3}.",
        category: "MicroSourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GeneratorInitializationError { get; } = new(
        id: "MSG003",
        title: "Generator Initialization Failure",
        messageFormat: "Generator '{0}' failed to initialize and {1} was thrown. StackTrace : {2}. Message : {3}.",
        category: "MicroSourceGenerator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}