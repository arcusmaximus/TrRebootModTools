using System.Text.RegularExpressions;
using TrRebootTools.BinaryTemplateGenerator.Util;

namespace TrRebootTools.BinaryTemplateGenerator
{
    internal abstract class TypeWriterBase
    {
        protected static readonly string[] TypesToSkip = [
            "Instance",
            "cdc::CubeLut",
            "cdc::IMaterial",
            "cdc::RenderMesh",
            "cdc::ResolveObject",
            "cdc::TextureMap"
        ];

        protected static readonly string[] ReservedNames = [
            "bool",
            "byte",
            "int",
            "uint32",
            "float",
            "string",
            "struct",
            "union",

            "if",
            "while",
            "continue",
            "break",
            "switch",
            "case",
            "default"
        ];

        protected readonly TypeLibrary _lib;
        protected IndentedTextWriter _writer;
        protected readonly CdcGame _game;
        protected readonly HashSet<string> _writtenTypes = new();

        protected readonly Dictionary<string, string> _typeNameOverrides = new();

        protected TypeWriterBase(TypeLibrary lib, TextWriter writer, CdcGame game)
        {
            _lib = lib;
            _writer = new(writer);
            _game = game;
        }

        public void AddTypeNameOverride(CType type, string name)
        {
            _typeNameOverrides[type.Name] = name;
        }

        public void SetWriter(TextWriter writer)
        {
            _writer = new(writer);
        }

        public virtual void WriteRootType(CType type)
        {
            WriteFileHeader(type);
            WriteType(type);
            WriteFileFooter(type);
        }

        public void WriteType(CType type)
        {
            if (type is CPrimitive || !_writtenTypes.Add(ApplyTypeNameOverride(type.Name)))
                return;

            foreach (CType referencedType in GetReferencedTypes(type))
            {
                if (TypesToSkip.Contains(referencedType.Name))
                    continue;

                WriteType(referencedType);
            }

            switch (type)
            {
                case CStructure structure:
                    WriteStructure(structure);
                    break;

                case CUnion union:
                    WriteUnion(union);
                    break;

                case CEnum enumeration:
                    WriteEnum(enumeration);
                    break;
            }
        }

        private void WriteStructure(CStructure structure)
        {
            WriteStructureHeader(structure);
            _writer.Indent++;

            int byteOffset = 0;
            int bitOffset = 0;
            int baseTypeIdx = 1;
            foreach (CType baseType in structure.BaseTypes.Select(n => _lib.Types[n]))
            {
                CField field = new(baseType.Name, "__parent" + (baseTypeIdx > 1 ? baseTypeIdx : ""), null, [])
                {
                    Alignment = baseType.Alignment,
                    Size = baseType.Size
                };
                WriteField(field, ref byteOffset, ref bitOffset, null);
                baseTypeIdx++;
            }

            SortedList<int, ListFieldPair> listFields = FindListFields(structure);
            for (int i = 0; i < structure.Fields.Count; i++)
            {
                CField field = structure.Fields[i];
                if (field.Name == "__vftable")
                    continue;

                listFields.TryGetValue(i, out ListFieldPair? list);
                WriteField(field, ref byteOffset, ref bitOffset, list);
                if (list != null && i == Math.Max(list.RefFieldIndex, list.CountFieldIndex))
                    WriteList(list);
            }

            WriteAlignment(ref byteOffset, ref bitOffset, structure.Alignment, null);

            _writer.Indent--;
            WriteStructureFooter(structure);
            _writer.WriteLine();
        }

        private void WriteUnion(CUnion union)
        {
            WriteUnionHeader(union);
            _writer.Indent++;

            foreach (CField field in union.Fields)
            {
                WriteField(field, false, null, true);
            }

            _writer.Indent--;
            WriteUnionFooter(union);
            _writer.WriteLine();
        }

        private void WriteField(CField field, ref int byteOffset, ref int bitOffset, ListFieldPair? list)
        {
            WriteAlignment(ref byteOffset, ref bitOffset, field.Alignment, field.BitLength);
            WriteField(field, true, list, false);
            AdvanceOffset(ref byteOffset, ref bitOffset, field);
        }

        private void WriteAlignment(ref int byteOffset, ref int bitOffset, int alignment, int? bitLength)
        {
            if (alignment == 0)
                return;

            if (bitOffset > 0 && bitLength == null)
                throw new InvalidDataException();

            if (bitOffset == 0)
            {
                int alignedByteOffset = (byteOffset + alignment - 1) & ~(alignment - 1);
                if (byteOffset != alignedByteOffset)
                {
                    WritePadding(alignedByteOffset - byteOffset);
                    byteOffset = alignedByteOffset;
                }
            }
        }

        private void WriteField(CField field, bool followRef, ListFieldPair? list, bool isUnionMember)
        {
            bool isRef = field.Type.EndsWith('*');
            if (isRef && followRef)
            {
                string pointerlessTypeName = field.Type.TrimEnd('*').Trim();
                CType? fieldType = _lib.Types.GetValueOrDefault(pointerlessTypeName);
                if (fieldType == null || fieldType.Name == "void" || (fieldType is not CPrimitive && !_writtenTypes.Contains(ApplyTypeNameOverride(pointerlessTypeName))))
                    followRef = false;
            }

            WriteField(field, isRef, followRef, list, isUnionMember);
        }

        private void WriteEnum(CEnum enumeration)
        {
            WriteEnumHeader(enumeration);
            _writer.Indent++;

            int idx = 0;
            foreach ((string key, int value) in enumeration.Values)
            {
                _writer.Write($"{key} = {value}");
                if (idx < enumeration.Values.Count - 1)
                    _writer.Write(",");

                _writer.WriteLine();
                idx++;
            }

            _writer.Indent--;
            WriteEnumFooter(enumeration);
            _writer.WriteLine();
        }

        protected abstract void WriteFileHeader(CType rootType);
        protected abstract void WriteFileFooter(CType rootType);

        protected abstract void WriteStructureHeader(CStructure structure);
        protected abstract void WritePadding(int size);
        protected abstract void WriteField(CField field, bool isRef, bool followRef, ListFieldPair? list, bool isUnionMember);
        protected abstract void WriteList(ListFieldPair array);
        protected abstract void WriteStructureFooter(CStructure structure);

        protected abstract void WriteUnionHeader(CUnion union);
        protected abstract void WriteUnionFooter(CUnion union);

        protected abstract void WriteEnumHeader(CEnum enumeration);
        protected abstract void WriteEnumFooter(CEnum enumeration);

        protected IEnumerable<CType> GetReferencedTypes(CType type)
        {
            foreach (string baseTypeName in type.BaseTypes)
            {
                yield return _lib.Types[baseTypeName];
            }

            if (type is CCompositeType compositeType)
            {
                IEnumerable<CField> fields = compositeType.Fields.Where(f => f.Name != "__vftable");
                foreach (CField field in fields)
                {
                    if (_lib.Types.TryGetValue(field.Type.TrimEnd('*').Trim(), out CType? fieldType))
                        yield return fieldType;
                }
            }
        }

        protected record ListFieldPair(int RefFieldIndex, CField RefField, int CountFieldIndex, CField CountField);

        protected SortedList<int, ListFieldPair> FindListFields(CStructure structure)
        {
            SortedList<int, ListFieldPair> lists = new();

            Dictionary<string, int> refFields = new();
            for (int i = 0; i < structure.Fields.Count; i++)
            {
                CField refField = structure.Fields[i];
                if (!refField.Type.EndsWith("*"))
                    continue;

                string listName = CleanFieldName(refField).ToLower();
                listName = Regex.Replace(listName, @"list$", "");
                listName = Regex.Replace(listName, @"s$", "");
                refFields[listName] = i;
            }

            for (int countFieldIdx = 0; countFieldIdx < structure.Fields.Count; countFieldIdx++)
            {
                CField countField = structure.Fields[countFieldIdx];
                if (countField.Type.EndsWith('*'))
                    continue;

                string listName = CleanFieldName(countField).ToLower();
                listName = Regex.Replace(listName, @"(count|size)$", "");
                listName = Regex.Replace(listName, @"(list|s)$", "");
                listName = Regex.Replace(listName, @"^num", "");
                if (!refFields.TryGetValue(listName, out int refFieldIdx) || Math.Abs(refFieldIdx - countFieldIdx) != 1)
                    continue;

                CField refField = structure.Fields[refFieldIdx];
                ListFieldPair pair = new(refFieldIdx, refField, countFieldIdx, countField);
                lists.Add(refFieldIdx, pair);
                lists.Add(countFieldIdx, pair);
            }

            return lists;
        }

        protected string ApplyTypeNameOverride(string name)
        {
            foreach ((string from, string to) in _typeNameOverrides)
            {
                if (name == from)
                    return to;

                if (name.StartsWith(from + "::"))
                    return to + "::" + name[(from.Length + 2)..];
            }
            return name;
        }

        protected static string RemoveTypeNamespace(string name)
        {
            string[] namespaces = ["cdc::", "dtp::"];
            foreach (string ns in namespaces)
            {
                if (name.StartsWith(ns))
                    name = name[ns.Length..];
            }
            return name;
        }

        protected string CleanTypeName(string name)
        {
            name = ApplyTypeNameOverride(name);
            name = RemoveTypeNamespace(name);
            name = Regex.Replace(name, @"(?![ :])\W", "_");
            name = name.Replace("::", NestedTypeSeparator);
            name = name switch
            {
                "void" => VoidTypeName,
                
                "bool" => BoolTypeName,

                "_BYTE" => UInt8TypeName,
                "char" => Int8TypeName,
                "unsigned char" => UInt8TypeName,
                "__int8" => Int8TypeName,
                "unsigned __int8" => UInt8TypeName,

                "__int16" => Int16TypeName,
                "unsigned __int16" => UInt16TypeName,

                "__int32" => Int32TypeName,
                "unsigned __int32" => UInt32TypeName,
                "unsigned int" => UInt32TypeName,

                "__int64" => Int64TypeName,
                "unsigned __int64" => UInt64TypeName,
                _ => name
            };
            return name;
        }

        protected abstract string NestedTypeSeparator { get; }

        protected abstract string VoidTypeName { get; }
        protected abstract string BoolTypeName { get; }
        protected abstract string Int8TypeName { get; }
        protected abstract string UInt8TypeName { get; }

        protected abstract string Int16TypeName { get; }
        protected abstract string UInt16TypeName { get; }

        protected abstract string Int32TypeName { get; }
        protected abstract string UInt32TypeName { get; }

        protected abstract string Int64TypeName { get; }
        protected abstract string UInt64TypeName { get; }

        protected virtual string CleanFieldName(CField field)
        {
            string name = field.Name;

            if (name.StartsWith("m_"))
                name = name[2..];

            if (name.Length >= 2 && name[0] == 'p' && char.IsUpper(name[1]))
                name = name[1..];

            if (char.IsUpper(name[0]))
                name = name.Substring(0, 1).ToLower() + name.Substring(1);

            name = Regex.Replace(name, @"_([a-zA-Z])", m => m.Groups[1].Value.ToUpper());

            if (ReservedNames.Contains(name) || Regex.IsMatch(name, @"^\d"))
                name = "_" + name;

            return name;
        }

        private static void AdvanceOffset(ref int byteOffset, ref int bitOffset, CField field)
        {
            if (field.BitLength != null)
            {
                bitOffset += field.BitLength.Value;
                while (bitOffset >= field.Size * 8)
                {
                    byteOffset += field.Size;
                    bitOffset -= field.Size * 8;
                }
            }
            else
            {
                byteOffset += field.Size;
            }
        }
    }
}
