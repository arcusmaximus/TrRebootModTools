namespace TrRebootTools.Shared.Cdc.Avengers
{
    internal class AvengersHash : CdcHash
    {
        protected override string ListFileName => "MA_PC_Release.list";

        public override ulong Calculate(string str)
        {
            return Calculate64(str);
        }
    }
}
