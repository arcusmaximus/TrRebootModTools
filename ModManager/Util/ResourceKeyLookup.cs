using System;
using System.Collections;
using System.Collections.Generic;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.ModManager.Util
{
    internal class ResourceKeyLookup : ICollection<ResourceKey>
    {
        private record ResourceInfo(ResourceSubType SubType, List<ulong> Locales);

        private readonly Dictionary<(ResourceType, int), ResourceInfo> _lookup = new();

        public ResourceKeyLookup(IEnumerable<ResourceKey> resourceKeys)
        {
            foreach (ResourceKey resourceKey in resourceKeys)
            {
                Add(resourceKey);
            }
        }

        public int Count
        {
            get;
            private set;
        }

        public bool IsReadOnly => false;

        public bool Contains(ResourceKey resourceKey)
        {
            ResourceInfo? info = _lookup.GetValueOrDefault((resourceKey.Type, resourceKey.Id));
            return info != null && info.Locales.Contains(resourceKey.Locale);
        }

        public ResourceSubType? GetSubType(ResourceType type, int id)
        {
            return _lookup.GetValueOrDefault((type, id))?.SubType;
        }

        public IReadOnlyCollection<ulong>? GetLocales(ResourceType type, int id)
        {
            return _lookup.GetValueOrDefault((type, id))?.Locales;
        }

        public void Add(ResourceKey item)
        {
            ResourceInfo info = _lookup.GetOrAdd((item.Type, item.Id), () => new(item.SubType, []));
            if (!info.Locales.Contains(item.Locale))
            {
                info.Locales.Add(item.Locale);
                Count++;
            }
        }

        public void Clear()
        {
            _lookup.Clear();
            Count = 0;
        }

        public void CopyTo(ResourceKey[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(ResourceKey item)
        {
            ResourceInfo? info = _lookup.GetValueOrDefault((item.Type, item.Id));
            if (info == null || !info.Locales.Contains(item.Locale))
                return false;

            info.Locales.Remove(item.Locale);
            Count--;
            return true;
        }

        public IEnumerator<ResourceKey> GetEnumerator()
        {
            foreach (((ResourceType type, int id), ResourceInfo info) in _lookup)
            {
                foreach (ulong locale in info.Locales)
                {
                    yield return new ResourceKey(type, info.SubType, id, locale);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
