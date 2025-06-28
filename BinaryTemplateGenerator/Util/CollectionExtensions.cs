using System.Collections.Generic;

namespace TrRebootTools.BinaryTemplateGenerator.Util
{
    internal static class CollectionExtensions
    {
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            dict.TryGetValue(key, out TValue value);
            return value;
        }

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}
