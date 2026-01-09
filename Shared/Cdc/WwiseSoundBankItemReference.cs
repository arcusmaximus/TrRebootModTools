namespace TrRebootTools.Shared.Cdc
{
    public enum WwiseSoundBankItemReferenceType
    {
        DataIndex,
        Event
    }

    public record WwiseSoundBankItemReference(int BankResourceId, ulong BankResourceLocale, WwiseSoundBankItemReferenceType Type, int Index);
}
