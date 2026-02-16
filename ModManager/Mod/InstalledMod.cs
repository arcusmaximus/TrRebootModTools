using System;
using TrRebootTools.Shared.Controls;

namespace TrRebootTools.ModManager.Mod
{
    public class InstalledMod : ICheckListBoxEntry
    {
        private bool _enabled;

        public InstalledMod(int archiveId, string name, bool enabled)
        {
            ArchiveId = archiveId;
            Name = name;
            Enabled = enabled;
        }

        public int ArchiveId
        {
            get;
        }

        public string Name
        {
            get;
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (value == _enabled)
                    return;

                _enabled = value;
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        bool ICheckListBoxEntry.Checked
        {
            get => Enabled;
            set => Enabled = value;
        }

        public event EventHandler? CheckedChanged;

        public override string ToString()
        {
            return Name;
        }
    }
}
