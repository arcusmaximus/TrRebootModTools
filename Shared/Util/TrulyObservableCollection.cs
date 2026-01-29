using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace TrRebootTools.Shared.Util
{
    public class TrulyObservableCollection<T> : ObservableCollection<T>
        where T : INotifyPropertyChanged
    {
        public TrulyObservableCollection()
        {
        }

        public TrulyObservableCollection(IEnumerable<T> items)
            : base(items)
        {
            foreach (T item in this)
            {
                item.PropertyChanged += OnItemChanged;
            }
        }

        public TrulyObservableCollection(List<T> items)
            : base(items)
        {
            foreach (T item in this)
            {
                item.PropertyChanged += OnItemChanged;
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);
            if (e.OldItems != null)
            {
                foreach (T item in e.OldItems)
                {
                    item.PropertyChanged -= OnItemChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (T item in e.NewItems)
                {
                    item.PropertyChanged += OnItemChanged;
                }
            }
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (RaiseItemChangedEvents && sender is T item)
                ItemChanged?.Invoke(item, e);
        }

        public delegate void ItemChangedHandler(T item, PropertyChangedEventArgs e);

        public event ItemChangedHandler? ItemChanged;

        public bool RaiseItemChangedEvents
        {
            get;
            set;
        } = true;
    }
}
