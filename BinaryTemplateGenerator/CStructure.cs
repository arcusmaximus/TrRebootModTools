namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class CStructure : CCompositeType
    {
        public CStructure(string name, string[] baseTypes, bool isCppObj)
            : base(name, baseTypes)
        {
            IsCppObj = isCppObj;
        }

        public bool IsCppObj
        {
            get;
        }
    }
}
