using System;

namespace TrRebootTools.Shared.Controls
{
    public interface ICheckListBoxEntry
    {
        bool Checked
        {
            get;
            set;
        }

        public event EventHandler CheckedChanged;

        string Name
        {
            get;
        }
    }
}
