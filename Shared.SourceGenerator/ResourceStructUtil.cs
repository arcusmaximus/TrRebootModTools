using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;

namespace TrRebootTools.Shared.SourceGenerator
{
    internal static class ResourceStructUtil
    {
        public static bool IsResourceStruct(TypeDeclarationSyntax type)
        {
            return GeneratorUtil.HasAnyBaseType(type, [Names.IResourceStruct, Names.IResourceStruct32, Names.IResourceStruct64]);
        }

        public static bool IsResourceStruct(ITypeSymbol type)
        {
            return GeneratorUtil.HasAnyBaseType(type, [Names.IResourceStruct, Names.IResourceStruct32, Names.IResourceStruct64]);
        }

        public static bool IsResourceUnion(TypeDeclarationSyntax type)
        {
            return GeneratorUtil.HasAnyBaseType(type, [Names.IResourceUnion, Names.IResourceUnion32, Names.IResourceUnion64]);
        }

        public static bool IsResourceUnion(ITypeSymbol type)
        {
            return GeneratorUtil.HasAnyBaseType(type, [Names.IResourceUnion, Names.IResourceUnion32, Names.IResourceUnion64]);
        }

        public static bool IsResourceRef(ITypeSymbol type)
        {
            return type.Name == Names.ResourceRef;
        }

        public static INamedTypeSymbol? GetResourceRefValueType(ITypeSymbol refType)
        {
            if (!IsResourceRef(refType))
                return null;

            var typeArgs = ((INamedTypeSymbol)refType).TypeArguments;
            if (typeArgs.Length == 0)
                return null;

            return (INamedTypeSymbol)typeArgs[0];
        }

        public static int CalculateTypeSize(ITypeSymbol type, int? pointerSize, SourceProductionContext context)
        {
            if (type is INamedTypeSymbol namedType && namedType.EnumUnderlyingType != null)
                type = namedType.EnumUnderlyingType;

            switch (type.Name)
            {
                case nameof(Boolean):
                case nameof(SByte):
                case nameof(Byte):
                    return 1;

                case nameof(Int16):
                case nameof(UInt16):
                    return 2;

                case nameof(Int32):
                case nameof(UInt32):
                    return 4;

                case nameof(Int64):
                case nameof(UInt64):
                    return 8;

                case nameof(Single):
                    return 4;

                case Names.ResourceRef:
                    if (pointerSize == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NoBitnessAvailable, type.Locations.FirstOrDefault()));
                        return 0;
                    }
                    return pointerSize.Value;
            }

            if (IsResourceStruct(type))
                return CalculateStructSize(type, context);

            if (IsResourceUnion(type))
                return CalculateUnionSize(type, context);

            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnsupportedFieldType, type.Locations.FirstOrDefault(), type.Name));
            return 0;
        }

        public static int CalculateStructSize(ITypeSymbol type, SourceProductionContext context)
        {
            int? pointerSize = GetPointerSize(type);
            return GeneratorUtil.GetFields(type).Sum(f => CalculateFieldSize(f, pointerSize, context));
        }

        public static int CalculateUnionSize(ITypeSymbol type, SourceProductionContext context)
        {
            int? pointerSize = GetPointerSize(type);
            return GeneratorUtil.GetFields(type).Max(f => CalculateFieldSize(f, pointerSize, context));
        }

        public static int CalculateFieldSize(IFieldSymbol field, int? pointerSize, SourceProductionContext context)
        {
            int? paddingSize = GetPaddingLength(field);
            if (paddingSize != null)
                return paddingSize.Value;

            ITypeSymbol type = field.Type;
            int arrayLength = 1;
            if (type is IArrayTypeSymbol arrayType)
            {
                arrayLength = GetArrayLength(field, context);
                type = arrayType.ElementType;
            }
            return CalculateTypeSize(type, pointerSize, context) * arrayLength;
        }

        public static int CalculateFieldOffset(IFieldSymbol field, SourceProductionContext context)
        {
            ITypeSymbol type = field.ContainingType;
            int? pointerSize = GetPointerSize(type);
            int offset = 0;
            foreach (IFieldSymbol f in GeneratorUtil.GetFields(type))
            {
                if (f.Equals(field, SymbolEqualityComparer.Default))
                    return offset;

                offset += CalculateFieldSize(f, pointerSize, context);
            }
            throw new Exception($"Field {field.Name} not found in type {type.Name}");
        }

        public static int? GetPointerSize(ITypeSymbol type)
        {
            if (GeneratorUtil.HasAnyBaseType(type, [Names.IResourceStruct32, Names.IResourceUnion32]))
                return 4;

            if (GeneratorUtil.HasAnyBaseType(type, [Names.IResourceStruct64, Names.IResourceUnion64]))
                return 8;

            return null;
        }

        public static int? GetPaddingLength(IFieldSymbol field)
        {
            return GeneratorUtil.GetAttributeValue<int?>(field, Names.PaddingAttribute);
        }

        public static int GetArrayLength(IFieldSymbol field, SourceProductionContext context)
        {
            int? arrayLength = GeneratorUtil.GetAttributeValue<int?>(field, Names.ArrayAttribute);
            if (arrayLength == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingArrayAttribute, field.Locations.FirstOrDefault(), field.Name));
                return 0;
            }
            return arrayLength.Value;
        }

        public static string? GetListCountField(IFieldSymbol refField)
        {
            return GeneratorUtil.GetAttributeValue<string>(refField, Names.ListAttribute);
        }

        public static string? GetUnionSelectorField(IFieldSymbol field)
        {
            return GeneratorUtil.GetAttributeValue<string>(field, Names.UnionAttribute);
        }

        public static int GetUnionMemberSelector(IFieldSymbol field)
        {
            return GeneratorUtil.GetAttributeValue<int>(field, Names.UnionMemberAttribute)!;
        }
    }
}
