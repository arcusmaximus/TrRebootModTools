using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ArchiveSet : ArchiveSet
    {
        private static readonly Dictionary<CultureInfo, ArchiveIdentity> FlattedModArchiveIdentities =
            new()
            {
                { CultureInfo.GetCultureInfo(1041), new(66, "jcontent.000.tiger") },
                { CultureInfo.InvariantCulture, new(67, "patch2.000.tiger") }
            };

        public Tr2013ArchiveSet(string folderPath, bool includeGame, bool includeMods)
            : base(folderPath, includeGame, includeMods)
        {
        }

        public override CdcGame Game => CdcGame.Tr2013;

        protected override bool SupportsMetaData => false;

        protected override bool RequiresSpecMaskFiles => false;

        public override List<Archive> GetSortedArchives()
        {
            int lastArchiveId = FlattedModArchiveIdentities.Max(p => p.Value.Id);
            return Archives.Where(a => a.Id <= lastArchiveId)
                           .OrderBy(a => a.Id)
                           .Concat(base.GetSortedArchives())
                           .ToList();
        }

        public override ICollection<ArchiveIdentity> GetAllFlattenedModArchiveIdentities()
        {
            return FlattedModArchiveIdentities.Values;
        }

        public override ArchiveIdentity GetActiveFlattenedModArchiveIdentity()
        {
            ArchiveIdentity langSpecificArchive = FlattedModArchiveIdentities.GetOrDefault(CultureInfo.InstalledUICulture);
            if (langSpecificArchive != null && File.Exists(Path.Combine(FolderPath, langSpecificArchive.FileName)))
                return langSpecificArchive;

            return FlattedModArchiveIdentities.GetOrDefault(CultureInfo.InvariantCulture);
        }

        protected override string MakeLocaleSuffix(ulong locale)
        {
            string language = CdcGameInfo.Get(Game).LocaleToLanguageName(locale);
            return language != null ? "_" + language : "";
        }
    }
}
