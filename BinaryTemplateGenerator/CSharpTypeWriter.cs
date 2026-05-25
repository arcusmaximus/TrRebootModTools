namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class CSharpTypeWriter : TypeWriterBase
    {
        protected readonly string _structInterface;
        protected readonly string _unionInterface;
        protected readonly List<string> _containingTypes = [];

        private int _paddingIndex;
        private int _bitfieldIndex;
        private int _bitfieldMemberOffset;
        private int _bitfieldSize;

        public CSharpTypeWriter(TypeLibrary lib, TextWriter writer, CdcGame game)
            : base(lib, writer, game)
        {
            string bitness = game == CdcGame.Tr2013 ? "32" : "64";
            _structInterface = $"IResourceStruct{bitness}";
            _unionInterface = $"IResourceUnion{bitness}";
        }

        protected override void WriteFileHeader(CType rootType)
        {
            _writer.WriteLine(
                $"""
                using System;
                using System.Collections.Generic;
                using System.ComponentModel;
                using TrRebootTools.Shared.Cdc;
                using TrRebootTools.Shared.Serialization;
                using TrRebootTools.ActionGraphEditor.Common;
            
                namespace TrRebootTools.ActionGraphEditor.{_game}.Nodes;

                """
            );
        }

        protected override void WriteFileFooter(CType rootType)
        {
            LeaveContainingTypes();
        }

        protected override void WriteStructureHeader(CStructure structure)
        {
            WriteTypeHeader("partial class", structure, _structInterface, "INotifyPropertyChanged");
            _paddingIndex = -1;
            _bitfieldIndex = -1;
        }

        protected override void WriteStructureFooter(CStructure structure)
        {
            _writer.WriteLine("}");
        }

        protected override void WritePadding(int size)
        {
            _writer.WriteLine($"[Padding({size})] private byte _padding{++_paddingIndex};");
        }

        protected override void WriteField(CField field, bool isRef, bool followRef, ListFieldPair? list, bool isUnionMember)
        {
            if (field.BitLength != null)
            {
                WriteBitfieldMember(field);
                return;
            }

            _bitfieldMemberOffset = 0;
            _bitfieldSize = 0;

            WriteFieldAttributes(field, list, isUnionMember);
            _writer.Write("private ");
            WriteFieldType(field, isRef, followRef, list);
            _writer.Write($" {CleanFieldName(field)}");
            if (isRef)
                _writer.Write("Ref");

            _writer.WriteLine(";");
        }

        private void WriteBitfieldMember(CField field)
        {
            if (field.Name.StartsWith("__bitfieldpadding"))
            {
                _bitfieldMemberOffset = 0;
                _bitfieldSize = 0;
                return;
            }

            if (_bitfieldMemberOffset == _bitfieldSize)
            {
                _bitfieldIndex++;
                _bitfieldMemberOffset = 0;
                _bitfieldSize = field.Size * 8;
            }

            string bitfieldType = CleanTypeName(field.Type);
            string bitfieldName = $"_bitfield{_bitfieldIndex}";
            string bitfieldMemberName = CleanFieldName(field);
            if (_bitfieldMemberOffset == 0)
                _writer.WriteLine($"private {bitfieldType} {bitfieldName};");

            int bitfieldMemberSize = field.BitLength!.Value;
            if (bitfieldMemberSize == 1)
            {
                _writer.WriteLine(
                    $$"""
                    public bool {{bitfieldMemberName}}
                    {
                        get => ({{bitfieldName}} & (1 << {{_bitfieldMemberOffset}})) != 0;
                        set
                        {
                            if (value)
                                {{bitfieldName}} = ({{bitfieldType}})({{bitfieldName}} | (1 << {{_bitfieldMemberOffset}}));
                            else
                                {{bitfieldName}} = ({{bitfieldType}})({{bitfieldName}} & ~(1 << {{_bitfieldMemberOffset}}));
                        }
                    }
                    """
                );
            }
            else
            {
                _writer.WriteLine(
                    $$"""
                    public {{bitfieldType}} {{bitfieldMemberName}}
                    {
                        get => ({{bitfieldType}})(({{bitfieldName}} >> {{_bitfieldMemberOffset}}) & ((1 << {{bitfieldMemberSize}}) - 1));
                        set
                        {
                            {{bitfieldName}} = ({{bitfieldType}})({{bitfieldName}} & ~(((1 << {{bitfieldMemberSize}}) - 1) << {{_bitfieldMemberOffset}}));
                            {{bitfieldName}} = ({{bitfieldType}})({{bitfieldName}} | (value << {{_bitfieldMemberOffset}}));
                        }
                    }
                    """
                );
            }

            _bitfieldMemberOffset += bitfieldMemberSize;
        }

        private void WriteFieldAttributes(CField field, ListFieldPair? list, bool isUnionMember)
        {
            if (field.ArrayDimensions.Length > 0)
            {
                if (field.ArrayDimensions.Length > 1)
                    throw new NotSupportedException();

                _writer.Write($"[Array({field.ArrayDimensions[0]})] ");
            }

            if (list != null && field == list.RefField)
                _writer.Write($"[List(nameof({CleanFieldName(list.CountField)}))] ");

            if (isUnionMember)
                _writer.Write("[UnionMember(-1)] ");
            else if (_lib.Types.GetValueOrDefault(field.Type) is CUnion)
                _writer.Write("[Union(nameof(Object))] ");
        }

        private void WriteFieldType(CField field, bool isRef, bool followRef, ListFieldPair? list)
        {
            if (isRef)
            {
                _writer.Write("ResourceRef");
                if (followRef)
                {
                    string valueTypeName = field.Type.TrimEnd('*').Trim();
                    if (valueTypeName is "char" or "const char")
                        valueTypeName = "string";

                    valueTypeName = CleanTypeName(valueTypeName);
                    if (list != null)
                        _writer.Write($"<List<{valueTypeName}>>");
                    else
                        _writer.Write($"<{valueTypeName}>");
                }
                _writer.Write("?");
            }
            else
            {
                _writer.Write(CleanTypeName(field.Type));
            }

            if (field.ArrayDimensions.Length > 0)
                _writer.Write("[]");
        }

        protected override void WriteList(ListFieldPair list)
        {
        }

        protected override void WriteUnionHeader(CUnion union)
        {
            WriteTypeHeader("partial class", union, _unionInterface);
        }

        protected override void WriteUnionFooter(CUnion union)
        {
            _writer.WriteLine($"}}");
        }

        protected override void WriteEnumHeader(CEnum enumeration)
        {
            string? baseType = enumeration.BaseTypes.Length > 0 ? CleanTypeName(enumeration.BaseTypes[0]) : null;
            if (baseType != null)
                WriteTypeHeader("enum", enumeration, baseType);
            else
                WriteTypeHeader("enum", enumeration);
        }

        private void WriteTypeHeader(string keyword, CType type, params string[] baseTypes)
        {
            string name = ApplyTypeNameOverride(type.Name);
            string[] nameParts = RemoveTypeNamespace(name).Split("::");
            SwitchToContainingType(nameParts[..^1]);
            _writer.Write($"internal {keyword} {(nameParts.Length > 1 ? "_" : "")}{CleanTypeName(nameParts.Last())}");
            if (baseTypes != null)
                _writer.Write(" : " + string.Join(", ", baseTypes));

            _writer.WriteLine();
            _writer.WriteLine("{");
        }

        protected override void WriteEnumFooter(CEnum enumeration)
        {
            _writer.WriteLine($"}}");
        }

        protected void SwitchToContainingType(Span<string> nameParts)
        {
            LeaveContainingTypes(nameParts.Length);

            for (int i = 0; i < nameParts.Length; i++)
            {
                if (i < _containingTypes.Count && _containingTypes[i] != nameParts[i])
                {
                    LeaveContainingTypes(i);
                }
                if (i == _containingTypes.Count)
                {
                    _writer.WriteLine($"partial class {(i > 0 ? "_" : "")}{CleanTypeName(nameParts[i])}");
                    _writer.WriteLine($"{{");
                    _writer.Indent++;
                    _containingTypes.Add(nameParts[i]);
                }
            }
        }

        protected void LeaveContainingTypes(int upToCount = 0)
        {
            if (_containingTypes.Count <= upToCount)
                return;

            while (_containingTypes.Count > upToCount)
            {
                _writer.Indent--;
                _writer.WriteLine("}");
                _containingTypes.RemoveAt(_containingTypes.Count - 1);
            }

            if (_containingTypes.Count == 0)
                _writer.WriteLine();
        }

        protected override string CleanFieldName(CField field)
        {
            return "_" + base.CleanFieldName(field);
        }

        protected override string NestedTypeSeparator => "._";

        protected override string VoidTypeName => "void";

        protected override string BoolTypeName => "bool";

        protected override string Int8TypeName => "sbyte";
        protected override string UInt8TypeName => "byte";

        protected override string Int16TypeName => "short";
        protected override string UInt16TypeName => "ushort";

        protected override string Int32TypeName => "int";
        protected override string UInt32TypeName => "uint";

        protected override string Int64TypeName => "long";
        protected override string UInt64TypeName => "ulong";
    }
}
