using System.Drawing;
using System.Globalization;
using System.Linq;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class CdcGameInfo
    {
        private static readonly CdcGameInfo[] Instances =
            [
                new Tr2013GameInfo(),
                new RiseGameInfo(),
                new ShadowGameInfo()
            ];

        public static CdcGameInfo Get(CdcGame game)
        {
            return Instances.First(i => i.Game == game);
        }

        public abstract CdcGame Game { get; }

        public abstract string ExeName { get; }

        public abstract int PointerSize { get; }

        public abstract string ShortName { get; }

        public abstract Image Icon { get; }

        public abstract string RegistryDisplayName { get; }

        public abstract string ArchivePlatform { get; }

        public abstract ulong LocalePlatformMask { get; }

        public abstract bool UsesFourccTextureFormat { get; }

        public abstract bool UsesWwise { get; }

        public abstract Language[] Languages { get; }

        public string LocaleToLanguageCode(ulong locale)
        {
            Language lang = Languages.FirstOrDefault(l => l.Locale == locale);
            return lang?.Code ?? locale.ToString("X016");
        }

        public string LocaleToLanguageName(ulong locale)
        {
            Language lang = Languages.FirstOrDefault(l => l.Locale == locale);
            return lang?.Name;
        }

        public ulong LanguageCodeToLocale(string languageCode)
        {
            if (languageCode.Length == 16 && ulong.TryParse(languageCode, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong locale))
                return locale;

            Language lang = Languages.FirstOrDefault(l => l.Code == languageCode);
            if (lang != null)
                return lang.Locale;

            return 0xFFFFFFFFFFFFFFFF;
        }

        public record Language(ulong Locale, string Code, string Name);
    }
}
