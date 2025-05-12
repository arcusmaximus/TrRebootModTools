namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class CPrimitive : CType
    {
        public CPrimitive(string name, int size)
            : base(name, [])
        {
            Alignment = size;
            Size = size;
        }
    }
}
