using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace TrRebootTools.ModManager.Mod
{
    public class InstalledMod : INotifyPropertyChanged
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

        public Color NameColor
        {
            get
            {
                if (!Enabled)
                    return Color.Gray;

                return Color.Empty;
            }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (value == _enabled)
                    return;

                _enabled = value;
                OnPropertyChanged();
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
