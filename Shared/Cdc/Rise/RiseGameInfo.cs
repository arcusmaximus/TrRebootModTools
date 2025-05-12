using System.Drawing;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseGameInfo : CdcGameInfo
    {
        public override CdcGame Game => CdcGame.Rise;

        public override string ExeName => "ROTTR.exe";

        public override int PointerSize => 8;

        public override string ShortName => "ROTTR";

        public override Image Icon => Properties.Resources.Rise;

        public override string RegistryDisplayName => "Rise of the Tomb Raider";

        public override string ArchivePlatform => "pcx64-w";

        public override ulong LocalePlatformMask => 0;

        public override bool UsesFourccTextureFormat => false;

        public override bool UsesWwise => false;

        public override Language[] Languages { get; } =
        [
            new(0xFFFFFFFFFFFF0000 | 1,         "en", "ENGLISH"),
            new(0xFFFFFFFFFFFF0000 | 2,         "fr", "FRENCH"),
            new(0xFFFFFFFFFFFF0000 | 4,         "de", "GERMAN"),
            new(0xFFFFFFFFFFFF0000 | 8,         "it", "ITALIAN"),
            new(0xFFFFFFFFFFFF0000 | 0x10,      "es419", "LATAMSPANISH"),
            new(0xFFFFFFFFFFFF0000 | 0x20,      "es", "IBERSPANISH"),
            new(0xFFFFFFFFFFFF0000 | 0x40,      "ja", "JAPANESE"),
            new(0xFFFFFFFFFFFF0000 | 0x80,      "pt", "PORTUGUESE"),
            new(0xFFFFFFFFFFFF0000 | 0x100,     "pl", "POLISH"),
            new(0xFFFFFFFFFFFF0000 | 0x200,     "ru", "RUSSIAN"),
            new(0xFFFFFFFFFFFF0000 | 0x400,     "nl", "DUTCH"),
            new(0xFFFFFFFFFFFF0000 | 0x800,     "ko", "KOREAN"),
            new(0xFFFFFFFFFFFF0000 | 0x1000,    "zhhant", "CHINESE"),
            new(0xFFFFFFFFFFFF0000 | 0x2000,    "zhhans", "SIMPLECHINESE"),
            new(0xFFFFFFFFFFFF0000 | 0x8000,    "ar", "ARABIC")
        ];
    }
}
