using System.Collections.Generic;

namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class CEnum : CType
    {
        public CEnum(string name, string? baseType)
            : base(name, baseType != null ? [baseType] : [])
        {
        }

        public Dictionary<string, int> Values { get; } = new();
    }
}
