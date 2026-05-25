using System.Collections;
using System.Collections.Generic;

namespace TrRebootTools.Shared.Util
{
    public class ListAdapter<TOuter, TInner> : IList<TOuter>
        where TInner : TOuter
    {
        private readonly IList<TInner> _inner;

        public ListAdapter(IList<TInner> inner)
        {
            _inner = inner;
        }

        public TOuter this[int index]
        {
            get => _inner[index];
            set => _inner[index] = (TInner)value;
        }

        public int Count => _inner.Count;

        public bool IsReadOnly => _inner.IsReadOnly;

        public void Add(TOuter item)
        {
            _inner.Add((TInner)item);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public bool Contains(TOuter item)
        {
            return _inner.Contains((TInner)item);
        }

        public void CopyTo(TOuter[] array, int arrayIndex)
        {
            for (int i = 0; i < _inner.Count; i++)
            {
                array[arrayIndex + i] = _inner[i];
            }
        }

        public IEnumerator<TOuter> GetEnumerator()
        {
            return ((IEnumerable<TOuter>)_inner).GetEnumerator();
        }

        public int IndexOf(TOuter item)
        {
            return _inner.IndexOf((TInner)item);
        }

        public void Insert(int index, TOuter item)
        {
            _inner.Insert(index, (TInner)item);
        }

        public bool Remove(TOuter item)
        {
            return _inner.Remove((TInner)item);
        }

        public void RemoveAt(int index)
        {
            _inner.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
