namespace TrRebootTools.Shared.Controls
{
    public interface IArrayItemControl
    {
        public int Index
        {
            get;
            set;
        }

        public object DataSource
        {
            get;
            set;
        }
    }
}
