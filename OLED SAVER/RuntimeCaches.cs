using System;
using System.Collections.Generic;

#nullable disable

namespace OLEDSaver
{
    internal sealed class CachedSnapshot<T>
    {
        private readonly object _syncRoot = new object();
        private readonly Func<T[]> _snapshotProvider;
        private T[] _snapshot;

        public CachedSnapshot(Func<T[]> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public T[] Get()
        {
            lock (_syncRoot)
            {
                if (_snapshot == null)
                {
                    _snapshot = _snapshotProvider();
                }

                return _snapshot;
            }
        }

        public void Invalidate()
        {
            lock (_syncRoot)
            {
                _snapshot = null;
            }
        }
    }

    internal sealed class ExpiringValueCache<TKey, TValue>
    {
        private readonly TimeSpan _lifetime;
        private readonly Func<DateTime> _nowProvider;
        private readonly Dictionary<TKey, Entry> _entries = new Dictionary<TKey, Entry>();

        public ExpiringValueCache(TimeSpan lifetime, Func<DateTime> nowProvider)
        {
            _lifetime = lifetime;
            _nowProvider = nowProvider;
        }

        public TValue GetOrAdd(TKey key, Func<TValue> valueFactory)
        {
            DateTime now = _nowProvider();
            if (_entries.TryGetValue(key, out Entry entry) &&
                now - entry.CreatedAt < _lifetime)
            {
                return entry.Value;
            }

            TValue value = valueFactory();
            _entries[key] = new Entry(value, now);
            return value;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        private readonly struct Entry
        {
            public Entry(TValue value, DateTime createdAt)
            {
                Value = value;
                CreatedAt = createdAt;
            }

            public TValue Value { get; }
            public DateTime CreatedAt { get; }
        }
    }

    internal sealed class CachedWindowHandles
    {
        private readonly List<IntPtr> _handles = new List<IntPtr>();

        public bool TryGetValid(Func<IntPtr, bool> isValid, out IReadOnlyList<IntPtr> handles)
        {
            if (_handles.Count == 0)
            {
                handles = Array.Empty<IntPtr>();
                return false;
            }

            for (int i = 0; i < _handles.Count; i++)
            {
                if (!isValid(_handles[i]))
                {
                    _handles.Clear();
                    handles = Array.Empty<IntPtr>();
                    return false;
                }
            }

            handles = _handles.ToArray();
            return true;
        }

        public void Replace(IEnumerable<IntPtr> handles)
        {
            _handles.Clear();
            _handles.AddRange(handles);
        }

        public void Clear()
        {
            _handles.Clear();
        }
    }
}
