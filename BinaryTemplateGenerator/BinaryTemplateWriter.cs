namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class BinaryTemplateWriter : TypeWriterBase
    {
        public BinaryTemplateWriter(TypeLibrary lib, TextWriter writer, CdcGame game)
            : base(lib, writer, game)
        {
        }

        protected override void WriteFileHeader(CType rootType)
        {
            _writer.WriteLine($"#define TR_VERSION {(int)_game}");
            _writer.WriteLine("#include \"../trcommon.bt\"");
            _writer.WriteLine();
        }

        protected override void WriteFileFooter(CType rootType)
        {
            _writer.WriteLine("RefDefinitions refDefinitions;");
            _writer.Write($"{CleanTypeName(rootType.Name)} root <open=true>;");
        }

        protected override void WriteStructureHeader(CStructure structure)
        {
            _writer.WriteLine("typedef struct");
            _writer.WriteLine("{");
        }

        protected override void WriteStructureFooter(CStructure structure)
        {
            _writer.WriteLine("} " + CleanTypeName(structure.Name) + " <optimize=false>;");
        }

        protected override void WritePadding(int size)
        {
            _writer.WriteLine($"FSkip({size});");
        }

        protected override void WriteField(CField field, bool isRef, bool followRef, ListFieldPair? list, bool isUnionMember)
        {
            string name = CleanFieldName(field);
            string pointerlessTypeName = field.Type;
            if (isRef)
            {
                _writer.WriteLine($"Ref {name}Ref;");
                if (!followRef)
                    return;

                _writer.WriteLine($"if (CanSeekTo({name}Ref))");
                _writer.WriteLine($"{{");
                _writer.WriteLine($"    SeekToRef({name}Ref);");
                _writer.Indent++;
                pointerlessTypeName = field.Type.TrimEnd('*').Trim();
            }

            _writer.Write($"{(field.Type is "char *" or "const char *" ? "string" : CleanTypeName(pointerlessTypeName))} {name}");
            if (field.BitLength != null)
                _writer.Write($" : {field.BitLength}");

            _writer.Write(string.Join("", field.ArrayDimensions.Select(d => $"[{Math.Max(d, 1)}]")));
            _writer.WriteLine(";");

            if (isRef)
            {
                _writer.Indent--;
                _writer.WriteLine($"    ReturnFromRef();");
                _writer.WriteLine($"}}");
            }
        }

        protected override void WriteList(ListFieldPair list)
        {
            string pointerFieldName = CleanFieldName(list.RefField);
            string referencedTypeName = CleanTypeName(list.RefField.Type.TrimEnd('*').Trim());
            string sizeFieldName = CleanFieldName(list.CountField);
            _writer.WriteLine(
                $$"""
                if (CanSeekTo({{pointerFieldName}}Ref))
                {
                    SeekToRef({{pointerFieldName}}Ref);
                    {{referencedTypeName}} {{pointerFieldName}}[{{sizeFieldName}}];
                    ReturnFromRef();
                }
                """
            );
        }

        protected override void WriteUnionHeader(CUnion union)
        {
            _writer.WriteLine("typedef union");
            _writer.WriteLine("{");
        }

        protected override void WriteUnionFooter(CUnion union)
        {
            _writer.WriteLine("} " + CleanTypeName(union.Name) + ";");
        }

        protected override void WriteEnumHeader(CEnum enumeration)
        {
            _writer.WriteLine(
                enumeration.BaseTypes.Length > 0 ? $"enum <{CleanTypeName(enumeration.BaseTypes[0])}> {CleanTypeName(enumeration.Name)}"
                                                 : $"enum {CleanTypeName(enumeration.Name)}"
            );
            _writer.WriteLine("{");
        }

        protected override void WriteEnumFooter(CEnum enumeration)
        {
            _writer.WriteLine("};");
        }

        protected override string NestedTypeSeparator => "_";

        protected override string VoidTypeName => "byte";
        protected override string BoolTypeName => "byte";

        protected override string Int8TypeName => "byte";
        protected override string UInt8TypeName => "ubyte";

        protected override string Int16TypeName => "short";
        protected override string UInt16TypeName => "ushort";

        protected override string Int32TypeName => "int";
        protected override string UInt32TypeName => "uint";

        protected override string Int64TypeName => "quad";
        protected override string UInt64TypeName => "uquad";
    }
}
