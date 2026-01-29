namespace TrRebootTools.BinaryTemplateGenerator
{
    internal abstract class CCompositeType : CType
    {
        public CCompositeType(string name, string[] baseTypes)
            : base(name, baseTypes)
        {
        }

        public List<CField> Fields { get; } = new();
    }
}
