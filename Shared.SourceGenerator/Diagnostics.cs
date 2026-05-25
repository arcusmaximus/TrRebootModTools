using Microsoft.CodeAnalysis;

namespace TrRebootTools.Shared.SourceGenerator
{
    internal static class Diagnostics
    {
        public static readonly DiagnosticDescriptor MissingArrayAttribute = new(
            "CDC001",
            "Array field has no [Array] attribute",
            "The array field {0} has no [Array] attribute",
            "ResourceStruct",
            DiagnosticSeverity.Error,
            true
        );

        public static readonly DiagnosticDescriptor MissingUnionAttribute = new(
            "CDC002",
            "Union-typed field has no [Union] attribute",
            "The union-typed field {0} has no [Union] attribute",
            "ResourceStruct",
            DiagnosticSeverity.Error,
            true
        );

        public static readonly DiagnosticDescriptor UnsupportedFieldType = new(
            "CDC003",
            "Field type not supported",
            "The type {0} is not supported in IResourceStruct/IResourceUnion",
            "ResourceStruct",
            DiagnosticSeverity.Error,
            true
        );

        public static readonly DiagnosticDescriptor NoBitnessAvailable = new(
            "CDC004",
            "No bitness set",
            "ResourceRef fields must be in a type that implements IResourceStruct32/64 (or Union)",
            "ResourceStruct",
            DiagnosticSeverity.Error,
            true
        );
    }
}
