using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TrRebootTools.Shared.Util
{
    public static class CollectionExtensions
    {
        public static int IndexOf<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            int index = 0;
            foreach (T item in items)
            {
                if (predicate(item))
                    return index;

                index++;
            }
            return -1;
        }

        public static int LastIndexOf<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            int lastIndex = -1;
            int index = 0;
            foreach (T item in items)
            {
                if (predicate(item))
                    lastIndex = index;

                index++;
            }
            return lastIndex;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TValue> createValue)
        {
            if (!dict.TryGetValue(key, out TValue? value))
            {
                value = createValue();
                dict.Add(key, value);
            }
            return value;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> createValue)
        {
            if (!dict.TryGetValue(key, out TValue? value))
            {
                value = createValue(key);
                dict.Add(key, value);
            }
            return value;
        }

        public static void TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (!dict.ContainsKey(key))
                dict.Add(key, value);
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }

        public static void AddRange<T>(this ObservableCollection<T> list, IEnumerable<T> items)
        {
            foreach (T item in items)
            {
                list.Add(item);
            }
        }

        public static void RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dict, Func<KeyValuePair<TKey, TValue>, bool> predicate)
        {
            foreach (TKey key in dict.Where(predicate).Select(p => p.Key).ToList())
            {
                dict.Remove(key);
            }
        }
    }
}
