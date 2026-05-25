using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using static TrRebootTools.Shared.SourceGenerator.GeneratorUtil;
using static TrRebootTools.Shared.SourceGenerator.ResourceStructUtil;

namespace TrRebootTools.Shared.SourceGenerator
{
    [Generator]
    internal class ResourceStructPropertyChangedGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                (node, _) => node is TypeDeclarationSyntax typeNode &&
                             (IsResourceStruct(typeNode) || IsResourceUnion(typeNode)) &&
                             HasAnyBaseType(typeNode, [nameof(INotifyPropertyChanged)]),
                (context, _) => context
            );
            context.RegisterSourceOutput(provider, Generate);
        }

        private void Generate(SourceProductionContext productionContext, GeneratorSyntaxContext generatorContext)
        {
            if (generatorContext.Node is not TypeDeclarationSyntax typeNode)
                return;

            if (generatorContext.SemanticModel.GetDeclaredSymbol(typeNode) is not INamedTypeSymbol type)
                return;

            List<INamedTypeSymbol> typeNesting = GetTypeNesting(type);

            StringWriter code = new();
            IndentedTextWriter writer = new(code);

            GenerateFileHeader(typeNesting, writer);
            GeneratePropertyChangedEvent(writer);
            GenerateProperties(type, writer, productionContext);
            GenerateFileFooter(typeNesting, writer);

            productionContext.AddSource(GetGeneratedSourceFileName(typeNesting), code.ToString());
        }

        private void GeneratePropertyChangedEvent(IndentedTextWriter writer)
        {
            writer.WriteLine(
                """
                public event PropertyChangedEventHandler? PropertyChanged;

                private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
                {
                    PropertyChanged?.Invoke(this, new(propertyName));
                }

                """
            );
        }

        private void GenerateProperties(INamedTypeSymbol type, IndentedTextWriter writer, SourceProductionContext context)
        {
            List<string> listCountFieldNames = [];
            foreach (IFieldSymbol field in GetFields(type))
            {
                string? listCountFieldName = GetListCountField(field);
                if (listCountFieldName != null)
                    listCountFieldNames.Add(listCountFieldName);
            }

            foreach (IFieldSymbol field in GetFields(type).Where(f => GetPaddingLength(f) == null &&
                                                                      f.DeclaredAccessibility == Accessibility.Private &&
                                                                      f.Name.StartsWith("_") &&
                                                                      !listCountFieldNames.Contains(f.Name)))
            {
                string propertyName = field.Name.Substring(1, 1).ToUpper() + field.Name.Substring(2);
                INamedTypeSymbol? refValueType = GetResourceRefValueType(field.Type);
                if (refValueType != null)
                    GenerateResouceRefValueProperty(propertyName, refValueType, field, writer);
                else if (field.Type is IArrayTypeSymbol)
                    GenerateArrayProperty(propertyName, field, writer, context);
                else
                    GenerateRegularProperty(propertyName, field, writer);
            }
        }

        private void GenerateResouceRefValueProperty(string propertyName, INamedTypeSymbol refValueType, IFieldSymbol field, IndentedTextWriter writer)
        {
            if (propertyName.EndsWith("Ref"))
                propertyName = propertyName.Substring(0, propertyName.Length - 3);

            writer.WriteLine(
                $$"""
                public {{refValueType}}? {{propertyName}}
                {
                    get => {{field.Name}}?.Value;
                    set
                    {
                        if (value == {{field.Name}}?.Value)
                            return;
                        
                        if (value != null)
                            ({{field.Name}} ??= new()).Value = value;
                        else
                            {{field.Name}} = null;

                        OnPropertyChanged();
                    }
                }

                """
            );
        }

        private void GenerateArrayProperty(string propertyName, IFieldSymbol field, IndentedTextWriter writer, SourceProductionContext context)
        {
            ITypeSymbol elemType = ((IArrayTypeSymbol)field.Type).ElementType;
            int arrayLength = GetArrayLength(field, context);
            writer.WriteLine(
                $$"""
                public {{field.Type}} {{propertyName}} => {{field.Name}} ??= new {{elemType}}[{{arrayLength}}];

                """
            );
        }

        private void GenerateRegularProperty(string propertyName, IFieldSymbol field, IndentedTextWriter writer)
        {
            writer.WriteLine(
                $$"""
                public {{field.Type}} {{propertyName}}
                {
                    get => {{field.Name}};
                    set
                    {
                        if (value == {{field.Name}})
                            return;

                        {{field.Name}} = value;
                        OnPropertyChanged();
                    }
                }

                """
            );
        }
    }
}
