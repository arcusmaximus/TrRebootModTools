using System.Drawing;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013GameInfo : CdcGameInfo
    {
        public override CdcGame Game => CdcGame.Tr2013;

        public override string ExeName => "TombRaider.exe";

        public override int PointerSize => 4;

        public override string ShortName => "TR2013";

        public override Image Icon => Properties.Resources.Tr2013;

        public override string RegistryDisplayName => "Tomb Raider";

        public override string ArchivePlatform => "pc-w";

        public override ulong LocalePlatformMask => 0x80000000;

        public override bool UsesFourccTextureFormat => true;

        public override bool UsesWwise => false;

        public override Language[] Languages { get; } =
        [
            new(0xFFFFFFFFFFFF1100 | 1,         "en", "ENGLISH"),
            new(0xFFFFFFFFFFFF1100 | 2,         "fr", "FRENCH"),
            new(0xFFFFFFFFFFFF1100 | 4,         "de", "GERMAN"),
            new(0xFFFFFFFFFFFF1100 | 8,         "it", "ITALIAN"),
            new(0xFFFFFFFFFFFF1100 | 0x10,      "es", "IBERSPANISH"),
            new(0xFFFFFFFFFFFF1100 | 0x40,      "pt", "PORTUGUESE"),
            new(0xFFFFFFFFFFFF1100 | 0x80,      "pl", "POLISH"),
            new(0xFFFFFFFFFFFF1100 | 0x200,     "ru", "RUSSIAN"),
            new(0xFFFFFFFFFFFF1100 | 0x400,     "cs", "CZECH"),
            new(0xFFFFFFFFFFFF1100 | 0x800,     "nl", "DUTCH"),
            new(0xFFFFFFFFFFFF1100 | 0x2000,    "ar", "ARABIC"),
            new(0xFFFFFFFFFFFF1100 | 0x4000,    "ko", "KOREAN"),
            new(0xFFFFFFFFFFFF1100 | 0x8000,    "zhhant", "CHINESE")
        ];
    }
}
