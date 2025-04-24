using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class BinaryTemplateWriter
    {
        private readonly TextWriter _writer;
        private readonly string[] TypesToSkip = new[] { "Instance" };

        public BinaryTemplateWriter(TextWriter writer, int trVersion)
        {
            _writer = writer;
            _writer.WriteLine($"#define TR_VERSION {trVersion}");
            _writer.WriteLine("#include \"../trcommon.bt\"");
            _writer.WriteLine();
        }

        public void WriteType(string typeName, TypeLibrary lib)
        {
            WriteType(lib.Types[typeName], lib, new HashSet<CType>());
            _writer.WriteLine("RefDefinitions refDefinitions;");
            _writer.Write($"{CleanTypeName(typeName)} root <open=true>;");
        }

        private void WriteType(CType type, TypeLibrary lib, HashSet<CType> visitedTypes)
        {
            if (type is CPrimitive || !visitedTypes.Add(type))
                return;

            foreach (CType referencedType in GetReferencedTypes(type, lib))
            {
                if (TypesToSkip.Contains(referencedType.Name))
                    continue;

                if (referencedType is CStructure struc && struc.Fields.Any(f => f.Name == "__vftable"))
                    continue;

                WriteType(referencedType, lib, visitedTypes);
            }

            switch (type)
            {
                case CStructure structure:
                    WriteStructure(structure, lib);
                    break;

                case CUnion union:
                    WriteUnion(union);
                    break;

                case CEnum enumeration:
                    WriteEnum(enumeration);
                    break;
            }
        }

        private void WriteStructure(CStructure structure, TypeLibrary lib)
        {
            _writer.WriteLine("typedef struct");
            _writer.WriteLine("{");

            int byteOffset = 0;
            int bitOffset = 0;
            int baseTypeIdx = 1;
            foreach (CType baseType in structure.BaseTypes.Select(n => lib.Types[n]))
            {
                CField field = new CField(baseType.Name, "__parent" + (baseTypeIdx > 1 ? baseTypeIdx : ""), null, Array.Empty<int>())
                               {
                                   Alignment = baseType.Alignment,
                                   Size = baseType.Size
                               };
                WriteField(field, ref byteOffset, ref bitOffset, false);
                baseTypeIdx++;
            }

            FindArrays(structure, out SortedList<int, ArrayFieldPair> arrays, out bool[] isArrayRelatedField);
            for (int i = 0; i < structure.Fields.Count; i++)
            {
                CField field = structure.Fields[i];
                if (field.Name == "__vftable")
                    continue;

                WriteField(field, ref byteOffset, ref bitOffset, !isArrayRelatedField[i]);
                if (arrays.TryGetValue(i, out ArrayFieldPair array))
                    WriteArray(array);
            }

            _writer.WriteLine("} " + CleanTypeName(structure.Name) + " <optimize=false>;");
            _writer.WriteLine();
        }

        private record ArrayFieldPair(CField PointerField, CField CountField);

        private void FindArrays(CStructure structure, out SortedList<int, ArrayFieldPair> arrays, out bool[] isArrayRelatedField)
        {
            arrays = new();
            isArrayRelatedField = new bool[structure.Fields.Count];

            Dictionary<string, int> pointerFields = new();
            for (int i = 0; i < structure.Fields.Count; i++)
            {
                CField pointerField = structure.Fields[i];
                if (!pointerField.Type.EndsWith("*"))
                    continue;

                string arrayName = CleanFieldName(pointerField).ToLower();
                arrayName = Regex.Replace(arrayName, @"list$", "");
                arrayName = Regex.Replace(arrayName, @"s$", "");
                pointerFields[arrayName] = i;
            }

            for (int i = 0; i < structure.Fields.Count; i++)
            {
                CField countField = structure.Fields[i];
                if (countField.Type.EndsWith("*"))
                    continue;

                string arrayName = CleanFieldName(countField).ToLower();
                arrayName = Regex.Replace(arrayName, @"(count|size)$", "");
                arrayName = Regex.Replace(arrayName, @"(list|s)$", "");
                arrayName = Regex.Replace(arrayName, @"^num", "");
                if (!pointerFields.TryGetValue(arrayName, out int pointerFieldIdx) || Math.Abs(pointerFieldIdx - i) != 1)
                    continue;

                arrays.Add(Math.Max(i, pointerFieldIdx), new ArrayFieldPair(structure.Fields[pointerFieldIdx], countField));
                isArrayRelatedField[pointerFieldIdx] = true;
                isArrayRelatedField[i] = true;
            }
        }

        private void WriteArray(ArrayFieldPair array)
        {
            string pointerFieldName = CleanFieldName(array.PointerField);
            string referencedTypeName = CleanTypeName(array.PointerField.Type.TrimEnd('*').Trim());
            string sizeFieldName = CleanFieldName(array.CountField);
            _writer.WriteLine($"    if (CanSeekTo({pointerFieldName}Ref))");
            _writer.WriteLine($"    {{");
            _writer.WriteLine($"        SeekToRef({pointerFieldName}Ref);");
            _writer.WriteLine($"        {referencedTypeName} {pointerFieldName}[{sizeFieldName}];");
            _writer.WriteLine($"        ReturnFromRef();");
            _writer.WriteLine($"    }}");
        }

        private void WriteUnion(CUnion union)
        {
            _writer.WriteLine("typedef union");
            _writer.WriteLine("{");
            foreach (CField field in union.Fields)
            {
                WriteField(field);
            }
            _writer.WriteLine("} " + CleanTypeName(union.Name) + ";");
            _writer.WriteLine();
        }

        private void WriteField(CField field, ref int byteOffset, ref int bitOffset, bool followRef)
        {
            WriteFieldAlignment(ref byteOffset, ref bitOffset, field);
            WriteField(field, followRef);
            AdvanceOffset(ref byteOffset, ref bitOffset, field);
        }

        private void WriteFieldAlignment(ref int byteOffset, ref int bitOffset, CField field)
        {
            if (bitOffset > 0 && field.BitLength == null)
                throw new InvalidDataException();

            if (bitOffset == 0)
            {
                int alignedByteOffset = (byteOffset + field.Alignment - 1) & ~(field.Alignment - 1);
                if (byteOffset != alignedByteOffset)
                {
                    _writer.WriteLine($"    FSkip({alignedByteOffset - byteOffset});");
                    byteOffset = alignedByteOffset;
                }
            }
        }

        private void WriteField(CField field, bool followRef = true)
        {
            string name = CleanFieldName(field);
            bool makeRef = field.Type.EndsWith("*");

            if (makeRef)
            {
                _writer.WriteLine($"    Ref {name}Ref;");
                if (!followRef)
                    return;

                _writer.WriteLine($"    if (CanSeekTo({name}Ref))");
                _writer.WriteLine($"    {{");
                _writer.WriteLine($"        SeekToRef({name}Ref);");
                _writer.Write("    ");
            }

            _writer.Write($"    {(field.Type == "char *" ? "string" : CleanTypeName(field.Type.TrimEnd('*').Trim()))} {name}");
            if (field.BitLength != null)
                _writer.Write($" : {field.BitLength}");

            _writer.Write(string.Join("", field.ArrayDimensions.Select(d => $"[{Math.Max(d, 1)}]")));
            _writer.WriteLine(";");

            if (makeRef)
            {
                _writer.WriteLine($"        ReturnFromRef();");
                _writer.WriteLine($"    }}");
            }
        }

        private void WriteEnum(CEnum enumeration)
        {
            _writer.WriteLine(
                enumeration.BaseTypes.Length > 0 ? $"enum <{CleanTypeName(enumeration.BaseTypes[0])}> {CleanTypeName(enumeration.Name)}"
                                                 : $"enum {CleanTypeName(enumeration.Name)}"
            );
            _writer.WriteLine("{");
            int idx = 0;
            foreach (KeyValuePair<string, int> pair in enumeration.Values)
            {
                _writer.Write($"    {pair.Key} = {pair.Value}");
                if (idx < enumeration.Values.Count - 1)
                    _writer.Write(",");

                _writer.WriteLine();
                idx++;
            }
            _writer.WriteLine("};");
            _writer.WriteLine();
        }

        private void AdvanceOffset(ref int byteOffset, ref int bitOffset, CField field)
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

        private IEnumerable<CType> GetReferencedTypes(CType type, TypeLibrary lib)
        {
            foreach (string baseTypeName in type.BaseTypes)
            {
                yield return lib.Types[baseTypeName];
            }

            if (type is CCompositeType compositeType)
            {
                IEnumerable<CField> fields = compositeType.Fields.Where(f => f.Name != "__vftable");
                foreach (CField field in fields)
                {
                    if (lib.Types.TryGetValue(field.Type.TrimEnd('*').Trim(), out CType fieldType))
                        yield return fieldType;
                }
            }
        }

        private static string CleanTypeName(string name)
        {
            name = name.Replace("cdc::", "").Replace("dtp::", "");
            name = Regex.Replace(name, @"(?! )[\W]", "_");
            name = name switch
            {
                "void" => "byte",
                "bool" => "byte",
                "__int8" => "byte",
                "unsigned __int8" => "ubyte",
                "__int16" => "short",
                "unsigned __int16" => "ushort",
                "__int32" => "int",
                "unsigned __int32" => "uint",
                "__int64" => "quad",
                "unsigned __int64" => "uquad",
                _ => name
            };
            return name;
        }

        private static string CleanFieldName(CField field)
        {
            string name = field.Name;

            if (name.StartsWith("m_"))
                name = name.Substring(2);

            if (name.Length >= 2 && name[0] == 'p' && char.IsUpper(name[1]))
                name = name.Substring(1);

            if (char.IsUpper(name[0]))
                name = name.Substring(0, 1).ToLower() + name.Substring(1);

            name = Regex.Replace(name, @"_([a-zA-Z])", m => m.Groups[1].Value.ToUpper());

            return name;
        }
    }
}
