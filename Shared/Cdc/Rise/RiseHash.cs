namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseHash : CdcHash
    {
        protected override string ListFileName => "ROTR_PC_Release.list";

        public override ulong Calculate(string str)
        {
            return Calculate32(str);
        }
    }
}
