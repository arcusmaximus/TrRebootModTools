namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowHash : CdcHash
    {
        protected override string ListFileName => "SOTR_PC_Release.list";

        public override ulong Calculate(string str)
        {
            return Calculate64(str);
        }
    }
}
