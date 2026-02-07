namespace TrRebootTools.Shared.Cdc.Avengers
{
    internal class AvengersArchiveSet : ArchiveSet
    {
        public AvengersArchiveSet(string folderPath, bool includeGame, bool includeMods)
            : base(folderPath, includeGame, includeMods)
        {
        }

        public override CdcGame Game => CdcGame.Avengers;
        protected override bool SupportsMetaData => true;
        protected override bool RequiresSpecMaskFiles => false;

        protected override string MakeLocaleSuffix(ulong locale)
        {
            string? language = CdcGameInfo.Get(Game).LocaleToLanguageName(locale);
            return language != null ? "_" + language : "";
        }
    }
}
