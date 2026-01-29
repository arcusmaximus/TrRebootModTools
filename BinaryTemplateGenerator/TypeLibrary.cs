namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class TypeLibrary
    {
        private readonly int _pointerSize;

        public TypeLibrary(int pointerSize)
        {
            _pointerSize = pointerSize;

            AddPrimitive("void", 0);

            AddPrimitive("bool", 1);
            AddPrimitive("char", 1);
            AddPrimitive("_BYTE", 1);
            AddPrimitive("__int8", 1);
            AddPrimitive("unsigned __int8", 1);

            AddPrimitive("__int16", 2);
            AddPrimitive("unsigned __int16", 2);

            AddPrimitive("int", 4);
            AddPrimitive("__int32", 4);
            AddPrimitive("unsigned int", 4);
            AddPrimitive("unsigned __int32", 4);

            AddPrimitive("__int64", 8);
            AddPrimitive("unsigned __int64", 8);

            AddPrimitive("__int128", 16);
            AddPrimitive("unsigned __int128", 16);

            AddPrimitive("float", 4);
            
            AddPrimitive("double", 8);
            AddPrimitive("long double", 8);
        }

        public Dictionary<string, CType> Types { get; } = new();

        public void Add(CType type)
        {
            Types[type.Name] = type;
        }

        private void AddPrimitive(string name, int size)
        {
            Add(new CPrimitive(name, size));
        }

        public void CalculateAlignmentsAndSizes(string typeName)
        {
            CalculateAlignmentsAndSizes(GetCType(typeName));
        }

        public void CalculateAlignmentsAndSizes(CType type)
        {
            CalculateAlignmentsAndSizes(type, new());
        }

        private void CalculateAlignmentsAndSizes(CDeclaration declaration, HashSet<CType> visitedTypes)
        {
            if (declaration.Size > 0 || declaration is CPrimitive)
                return;

            int alignment = 0;
            int size = 0;
            if (declaration is CType type)
            {
                if (!visitedTypes.Add(type))
                    return;

                foreach (CType baseType in type.BaseTypes.Select(GetCType))
                {
                    CalculateAlignmentsAndSizes(baseType, visitedTypes);
                    if (alignment == 0)
                        alignment = baseType.Alignment;

                    size = SeekForward(size, baseType.Alignment, baseType.Size);
                }
            }

            switch (declaration)
            {
                case CEnum enumType:
                    enumType.Size = size > 0 ? size : 4;
                    enumType.Alignment = enumType.Size;
                    break;

                case CField field:
                    if (field.Type.EndsWith("*"))
                    {
                        CType fieldType = TryGetCType(field.Type.TrimEnd('*').Trim()) ?? GetCType("void");
                        CalculateAlignmentsAndSizes(fieldType, visitedTypes);
                        field.Alignment = _pointerSize;
                        field.Size = _pointerSize * field.TotalItemCount;
                    }
                    else
                    {
                        CType fieldType = GetCType(field.Type);
                        CalculateAlignmentsAndSizes(fieldType, visitedTypes);
                        field.Alignment = fieldType.Alignment;
                        field.Size = fieldType.Size * field.TotalItemCount;
                    }
                    break;

                case CStructure structure:
                    foreach (CField field in structure.Fields)
                    {
                        CalculateAlignmentsAndSizes(field, visitedTypes);
                        if (alignment == 0)
                            alignment = field.Alignment;

                        size = SeekForward(size, field.Alignment, field.Size);
                    }
                    structure.Alignment = alignment;
                    structure.Size = size;
                    break;

                case CUnion union:
                {
                    foreach (CField field in union.Fields)
                    {
                        CalculateAlignmentsAndSizes(field, visitedTypes);
                        alignment = Math.Max(field.Alignment, alignment);
                        size = Math.Max(field.Size, size);
                    }
                    union.Alignment = alignment;
                    union.Size = size;
                    break;
                }
            }
        }

        private CType GetCType(string name)
        {
            CType? type = TryGetCType(name);
            if (type == null)
                throw new Exception($"Type {name} not found in header file.");

            return type;
        }

        private CType? TryGetCType(string name)
        {
            Types.TryGetValue(name, out CType? type);
            return type;
        }

        private static int SeekForward(int offset, int alignment, int size)
        {
            offset = (offset + alignment - 1) & ~(alignment - 1);
            offset += size;
            return offset;
        }
    }
}
