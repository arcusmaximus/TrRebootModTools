namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013Hash : CdcHash
    {
        protected override string ListFileName => "TR2013_PC_Release.list";

        public override ulong Calculate(string str)
        {
            return Calculate32(str);
        }
    }
}
