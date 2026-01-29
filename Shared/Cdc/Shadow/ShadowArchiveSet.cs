namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowArchiveSet : ArchiveSet
    {
        public ShadowArchiveSet(string folderPath, bool includeGame, bool includeMods)
            : base(folderPath, includeGame, includeMods)
        {
        }

        public override CdcGame Game => CdcGame.Shadow;

        protected override bool SupportsMetaData => true;

        protected override bool RequiresSpecMaskFiles => true;

        protected override string MakeLocaleSuffix(ulong locale)
        {
            if (locale == 0xFFFFFFFFFFFFFFFF)
                return "";

            uint textFlags = (uint)(locale & 0xFFFFFFF);
            string textLanguage;
            if (textFlags == 0xFFFFFFF)
                textLanguage = "alltxt";
            else
                textLanguage = CdcGameInfo.Get(Game).LocaleToLanguageName(0xFFFFFFFFF0000000 | textFlags)?.ToLower() ?? "alltxt";

            uint voiceFlags = (uint)((locale >> 28) & 0xFFFFFFF);
            string voiceLanguage;
            if (voiceFlags == 0xFFFFFFF)
                voiceLanguage = "allvo";
            else
                voiceLanguage = CdcGameInfo.Get(Game).LocaleToLanguageName(0xFFFFFFFFF0000000 | voiceFlags)?.ToLower() ?? "allvo";

            int platformFlags = (int)(locale >> 56);
            string platform = platformFlags switch
            {
                0xFF => "allplt",
                0x20 => "neutral",
                _ => platformFlags.ToString("X02")
            };

            return $"_{textLanguage}_{voiceLanguage}_{platform}";
        }
    }
}
