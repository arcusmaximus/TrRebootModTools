using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static TrRebootTools.Shared.SourceGenerator.GeneratorUtil;
using static TrRebootTools.Shared.SourceGenerator.ResourceStructUtil;

namespace TrRebootTools.Shared.SourceGenerator
{
    [Generator]
    public class ResourceStructSerializationGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //Debugger.Launch();
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                (node, cancellationToken) => IsSerializable(node),
                (generatorContext, cancellationToken) => generatorContext
            );
            context.RegisterSourceOutput(provider, Generate);
        }

        private static bool IsSerializable(SyntaxNode node)
        {
            TypeDeclarationSyntax composite;
            if (node is ClassDeclarationSyntax cls && !cls.Modifiers.Any(m => m.ValueText == "abstract"))
                composite = cls;
            else if (node is StructDeclarationSyntax struc)
                composite = struc;
            else
                return false;

            return IsResourceStruct(composite) || IsResourceUnion(composite);
        }

        private static void Generate(SourceProductionContext productionContext, GeneratorSyntaxContext generatorContext)
        {
            if (generatorContext.Node is not TypeDeclarationSyntax typeNode)
                return;

            if (generatorContext.SemanticModel.GetDeclaredSymbol(typeNode) is not INamedTypeSymbol type)
                return;

            StringWriter code = new();
            IndentedTextWriter writer = new(code);

            List<INamedTypeSymbol> typeNesting = GetTypeNesting(type);
            GenerateFileHeader(typeNesting, writer);

            GenerateTypeSizeProperty(type, writer, productionContext);
            if (IsResourceStruct(type))
            {
                GenerateStructReadMethod(type, writer, productionContext);
                GenerateStructWriteMethod(type, writer, productionContext);
            }
            else if (IsResourceUnion(type))
            {
                GenerateUnionReadMethod(type, writer, productionContext);
                GenerateUnionWriteMethod(type, writer, productionContext);
            }

            GenerateFileFooter(typeNesting, writer);

            productionContext.AddSource(GetGeneratedSourceFileName(typeNesting), code.ToString());
        }

        private static void GenerateTypeSizeProperty(INamedTypeSymbol type, IndentedTextWriter writer, SourceProductionContext context)
        {
            writer.WriteLine($"public static int {Names.TypeSize} => 0x{CalculateTypeSize(type, null, context):X};");
            writer.WriteLine();
        }

        private static void GenerateStructReadMethod(INamedTypeSymbol type, IndentedTextWriter writer, SourceProductionContext context)
        {
            writer.WriteLine(
                """
                public void Read(ResourceReader reader)
                {
                    int startOffset = reader.Offset;
                """
            );
            writer.Indent++;

            foreach (IFieldSymbol field in GetFields(type))
            {
                GenerateFieldReadStatements(field, writer, context);
            }
            foreach (IFieldSymbol field in GetFields(type))
            {
                ITypeSymbol? refValueType = GetResourceRefValueType(field.Type);
                if (refValueType != null)
                    GenerateRefValueReadStatements(field, refValueType, writer);
            }
            foreach (IFieldSymbol field in GetFields(type).Where(f => IsResourceUnion(f.Type)))
            {
                GenerateUnionTypedFieldReadStatements(field, writer, context);
            }

            writer.Indent--;
            writer.WriteLine(
                """
                }
                """
            );
            writer.WriteLine();
        }

        private static void GenerateUnionReadMethod(INamedTypeSymbol type, IndentedTextWriter writer, SourceProductionContext context)
        {
            writer.WriteLine(
                $$"""
                public void Read(ResourceReader reader, int fieldSelector)
                {
                    int endPos = reader.Position + {{Names.TypeSize}};
                    switch (fieldSelector)
                    {
                """
            );
            writer.Indent += 2;

            foreach (IFieldSymbol field in GetFields(type))
            {
                int fieldSelector = GetUnionMemberSelector(field);
                writer.WriteLine($"case {fieldSelector}:");
                writer.Indent++;
                GenerateFieldReadStatements(field, writer, context);
                writer.Indent--;
                writer.WriteLine("    break;");
            }

            writer.Indent -= 2;
            writer.WriteLine(
                """
                    }
                    reader.Position = endPos;
                }
                """
            );
            writer.WriteLine();
        }

        private static void GenerateFieldReadStatements(IFieldSymbol field, IndentedTextWriter writer, SourceProductionContext context)
        {
            int? paddingLength = GetPaddingLength(field);
            if (paddingLength != null)
            {
                writer.WriteLine($"reader.Skip({paddingLength.Value});");
                return;
            }

            switch (field.Type)
            {
                case IArrayTypeSymbol arrayType:
                    ITypeSymbol elemType = arrayType.ElementType;
                    int arrayLength = GetArrayLength(field, context);
                    writer.WriteLine(
                        $$"""
                        {{field.Name}} = new {{elemType}}[{{arrayLength}}];
                        for (int i = 0; i < {{arrayLength}}; i++)
                        {
                            {{field.Name}}[i] = {{MakeScalarReadExpression(elemType)}};
                        }
                        """
                    );
                    break;

                case INamedTypeSymbol fieldType:
                    if (IsResourceUnion(fieldType))
                    {
                        writer.WriteLine($"reader.Skip({fieldType}.{Names.TypeSize});");
                        break;
                    }

                    writer.WriteLine($"{field.Name} = {MakeScalarReadExpression(fieldType)};");
                    break;
            }
        }

        private static void GenerateRefValueReadStatements(IFieldSymbol field, ITypeSymbol refValueType, IndentedTextWriter writer)
        {
            writer.WriteLine(
                $$"""
                if ({{field.Name}} != null)
                {
                    using var _ = reader.Seek({{field.Name}});
                """
            );
            writer.Indent++;

            string? listCountFieldName = GetListCountField(field);
            if (listCountFieldName != null)
            {
                ITypeSymbol listElemType = ((INamedTypeSymbol)refValueType).TypeArguments[0];
                writer.WriteLine(
                    $$"""
                    {{field.Name}}.Value = new((int){{listCountFieldName}});
                    for (int i = 0; i < (int){{listCountFieldName}}; i++)
                    {
                        {{field.Name}}.Value.Add({{MakeScalarReadExpression(listElemType)}});
                    }
                    """
                );
            }
            else
            {
                writer.WriteLine($"{field.Name}.Value = {MakeScalarReadExpression(refValueType)};");
            }
            writer.Indent--;
            writer.WriteLine(
                """
                }
                """
            );
        }

        private static void GenerateUnionTypedFieldReadStatements(IFieldSymbol field, IndentedTextWriter writer, SourceProductionContext context)
        {
            string? selectorField = GetUnionSelectorField(field);
            if (selectorField == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingUnionAttribute, field.Locations.FirstOrDefault(), field.Name));
                return;
            }

            writer.WriteLine(
                $$"""
                using (reader.Seek(startOffset + {{CalculateFieldOffset(field, context)}}))
                {
                    {{field.Name}} = reader.ReadUnion<{{field.Type}}>((int){{selectorField}});
                }
                """
            );
        }

        private static string MakeScalarReadExpression(ITypeSymbol type)
        {
            if (IsPrimitive(type))
                return $"reader.Read{type.Name}()";

            INamedTypeSymbol namedType = (INamedTypeSymbol)type;
            if (namedType.EnumUnderlyingType != null)
                return $"({type.Name})reader.Read{namedType.EnumUnderlyingType.Name}()";

            if (IsResourceRef(type))
            {
                if (namedType.TypeArguments.Length > 0)
                    return $"reader.ReadRef<{namedType.TypeArguments[0]}>()";
                else
                    return "reader.ReadRef()";
            }

            return $"reader.ReadStruct<{type}>()";
        }

        private static void GenerateStructWriteMethod(INamedTypeSymbol type, IndentedTextWriter writer, SourceProductionContext context)
        {
            writer.WriteLine("public void Write(ResourceBuilder writer)");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (IFieldSymbol listRefField in GetFields(type))
            {
                string? listCountFieldName = GetListCountField(listRefField);
                if (listCountFieldName != null)
                    GenerateListPreWriteStatements(listCountFieldName, listRefField, writer);
            }
            foreach (IFieldSymbol field in GetFields(type))
            {
                GenerateFieldWriteStatements(field, writer, context);
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
        }

        private static void GenerateUnionWriteMethod(INamedTypeSymbol type, IndentedTextWriter writer, SourceProductionContext context)
        {
            writer.WriteLine(
                $$"""
                public void Write(ResourceBuilder writer, int fieldSelector)
                {
                    int endPos = writer.Position + {{Names.TypeSize}};
                    switch (fieldSelector)
                    {
                """
            );
            writer.Indent += 2;

            List<IFieldSymbol> fields = GetFields(type).ToList();
            foreach (IFieldSymbol field in fields)
            {
                int fieldSelector = GetUnionMemberSelector(field);
                writer.WriteLine($"case {fieldSelector}:");
                writer.Indent++;
                GenerateFieldWriteStatements(field, writer, context);
                writer.Indent--;
                writer.WriteLine("    break;");
            }

            writer.Indent -= 2;
            writer.WriteLine(
                """
                    }
                    writer.WritePadding(endPos - writer.Position);
                }

                """
            );
        }

        private static void GenerateListPreWriteStatements(string countFieldName, IFieldSymbol refField, IndentedTextWriter writer)
        {
            IFieldSymbol countField = GetFields(refField.ContainingType).First(f => f.Name == countFieldName);
            writer.WriteLine($"{countField.Name} = ({countField.Type})({refField.Name}?.Value.Count ?? 0);");
        }

        private static void GenerateFieldWriteStatements(IFieldSymbol field, IndentedTextWriter writer, SourceProductionContext context)
        {
            int? paddingLength = GetPaddingLength(field);
            if (paddingLength != null)
            {
                writer.WriteLine($"writer.WritePadding({paddingLength.Value});");
                return;
            }

            switch (field.Type)
            {
                case IArrayTypeSymbol arrayType:
                    GenerateArrayFieldWriteStatements(field, arrayType.ElementType, writer, context);
                    break;

                case INamedTypeSymbol fieldType:
                    if (IsResourceUnion(fieldType))
                    {
                        GenerateUnionTypedFieldWriteStatements(field, writer, context);
                        break;
                    }

                    GenerateValueWriteStatements(fieldType, field.Name, writer);

                    INamedTypeSymbol? refValueType = GetResourceRefValueType(fieldType);
                    if (refValueType != null)
                    {
                        GenerateRefValueWriteStatements(field, refValueType, writer);
                        break;
                    }
                    break;
            }
        }

        private static void GenerateArrayFieldWriteStatements(IFieldSymbol field, ITypeSymbol elemType, IndentedTextWriter writer, SourceProductionContext context)
        {
            int arrayLength = GetArrayLength(field, context);
            writer.WriteLine(
                $$"""
                if ({{field.Name}} == null)
                    throw new ArgumentNullException(nameof({{field.Name}}));

                if ({{field.Name}}.Length != {{arrayLength}})
                    throw new InvalidDataException($"Array {{field.Name}} must have {{arrayLength}} elements but has {{{field.Name}}.Length} instead");

                foreach (var elem in {{field.Name}})
                {
                """
            );
            writer.Indent++;
            GenerateValueWriteStatements(elemType, "elem", writer);
            writer.Indent--;
            writer.WriteLine(
                """
                }
                """
            );
        }

        private static void GenerateUnionTypedFieldWriteStatements(IFieldSymbol field, IndentedTextWriter writer, SourceProductionContext context)
        {
            string? selectorField = GetUnionSelectorField(field);
            if (selectorField == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingUnionAttribute, field.Locations.FirstOrDefault(), field.Name));
                return;
            }

            writer.WriteLine($"writer.Write({field.Name}, (int){selectorField});");
        }

        private static void GenerateValueWriteStatements(ITypeSymbol type, string name, IndentedTextWriter writer)
        {
            string value = name;

            ITypeSymbol? enumUnderlyingType = (type as INamedTypeSymbol)?.EnumUnderlyingType;
            if (enumUnderlyingType != null)
                value = $"({enumUnderlyingType.Name}){value}";

            writer.WriteLine($"writer.Write({value});");
        }

        private static void GenerateRefValueWriteStatements(IFieldSymbol field, ITypeSymbol refValueType, IndentedTextWriter writer)
        {
            writer.WriteLine(
                $$"""
                if ({{field.Name}} != null)
                {
                """
            );
            writer.Indent++;

            if (GetListCountField(field) != null)
            {
                writer.WriteLine(
                    $$"""
                    writer.AddPendingWrite(
                        {{field.Name}},
                        w =>
                        {
                            foreach (var item in {{field.Name}}!.Value)
                            {
                                w.Write(item);
                            }
                        }
                    );
                    """
                );
            }
            else
            {
                writer.WriteLine(
                    $$"""
                    writer.AddPendingWrite({{field.Name}}, w => w.Write({{field.Name}}.Value));
                    """
                );
            }

            writer.Indent--;
            writer.WriteLine(
                """
                }
                """
            );
        }
    }
}
