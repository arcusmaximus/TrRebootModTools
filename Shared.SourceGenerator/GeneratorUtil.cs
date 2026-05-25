using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TrRebootTools.Shared.SourceGenerator
{
    internal static class GeneratorUtil
    {
        public static IEnumerable<IFieldSymbol> GetFields(ITypeSymbol type)
        {
            return type.GetMembers().OfType<IFieldSymbol>().Where(f => f.AssociatedSymbol == null);
        }

        public static IEnumerable<IPropertySymbol> GetProperties(ITypeSymbol type)
        {
            return type.GetMembers().OfType<IPropertySymbol>();
        }

        public static bool IsPrimitive(ITypeSymbol type)
        {
            switch (type.Name)
            {
                case nameof(Boolean):
                case nameof(SByte):
                case nameof(Byte):
                case nameof(Int16):
                case nameof(UInt16):
                case nameof(Int32):
                case nameof(UInt32):
                case nameof(Int64):
                case nameof(UInt64):
                case nameof(Single):
                case nameof(String):
                    return true;

                default:
                    return false;
            }
        }

        public static bool HasAnyBaseType(TypeDeclarationSyntax type, string[] baseTypeNames)
        {
            return type.BaseList?.Types.Any(
                b => b.Type is IdentifierNameSyntax name && baseTypeNames.Contains(name.Identifier.Text)
            ) ?? false;
        }

        public static bool HasAnyBaseType(ITypeSymbol type, string[] baseTypeNames)
        {
            return type.Interfaces.Concat([type.BaseType]).Any(i => baseTypeNames.Contains(i?.Name));
        }

        public static List<INamedTypeSymbol> GetTypeNesting(INamedTypeSymbol type)
        {
            List<INamedTypeSymbol> types = [];
            while (type != null)
            {
                types.Insert(0, type);
                type = type.ContainingType;
            }
            return types;
        }

        public static bool HasAttribute(MemberDeclarationSyntax node, string attrClassName)
        {
            string attrName = attrClassName.Substring(0, attrClassName.Length - "Attribute".Length);
            return node.AttributeLists.SelectMany(l => l.Attributes).Any(
                a => a.Name is IdentifierNameSyntax name && (name.Identifier.Text == attrClassName || name.Identifier.Text == attrName)
            );
        }

        public static bool HasAttribute(ISymbol symbol, string attrClassName)
        {
            return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attrClassName);
        }

        public static T? GetAttributeValue<T>(ISymbol symbol, string attrClassName)
        {
            AttributeData? attr = GetAttribute(symbol, attrClassName);
            return (T?)attr?.ConstructorArguments[0].Value;
        }

        public static (T1?, T2?) GetAttributeValues<T1, T2>(ISymbol symbol, string attrClassName)
        {
            AttributeData? attr = GetAttribute(symbol, attrClassName);
            return (
                (T1?)attr?.ConstructorArguments[0].Value,
                (T2?)attr?.ConstructorArguments[1].Value
            );
        }

        private static AttributeData? GetAttribute(ISymbol symbol, string attrClassName)
        {
            return symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == attrClassName);
        }

        public static void GenerateFileHeader(List<INamedTypeSymbol> typeNesting, IndentedTextWriter writer)
        {
            writer.WriteLine(
                $$"""
                using System;
                using System.ComponentModel;
                using System.IO;
                using System.Runtime.CompilerServices;
                using TrRebootTools.Shared.Cdc;
                using TrRebootTools.Shared.Serialization;
                
                namespace {{typeNesting[0].ContainingNamespace}};

                """
            );

            foreach (INamedTypeSymbol type in typeNesting)
            {
                var typeNode = (TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax();
                writer.WriteLine(
                    $$"""
                    partial {{typeNode.Keyword}} {{type.Name}}
                    {
                    """
                );
                writer.Indent++;
            }
        }

        public static void GenerateFileFooter(List<INamedTypeSymbol> typeNesting, IndentedTextWriter writer)
        {
            foreach (var _ in typeNesting)
            {
                writer.Indent--;
                writer.WriteLine("}");
            }
        }

        public static string GetGeneratedSourceFileName(List<INamedTypeSymbol> typeNesting)
        {
            string ns = typeNesting[0].ContainingNamespace.ToDisplayString();
            string typeChain = string.Join(".", typeNesting.Select(t => t.Name));
            return $"{ns}.{typeChain}.Generated.cs";
        }
    }
}
