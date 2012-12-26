using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    class ConcurrentMultiMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, ISet<TValue>>>, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly ConcurrentDictionary<TKey, ConcurrentSet<TValue>> _dictionary = new ConcurrentDictionary<TKey, ConcurrentSet<TValue>>();

        IEnumerator<KeyValuePair<TKey, ISet<TValue>>> IEnumerable<KeyValuePair<TKey, ISet<TValue>>>.GetEnumerator()
        {
            return _dictionary.Select(kvp => new KeyValuePair<TKey, ISet<TValue>>(kvp.Key, kvp.Value)).GetEnumerator();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.SelectMany(kvp => kvp.Value.Select(v => new KeyValuePair<TKey, TValue>(kvp.Key, v))).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return Contains(item.Key, item.Value);
        }

        public bool Contains(TKey key, TValue value)
        {
            ConcurrentSet<TValue> set;
            return _dictionary.TryGetValue(key, out set) && set.Contains(value);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key, item.Value);
        }

        public bool Remove(TKey key, TValue value)
        {
            ConcurrentSet<TValue> set;
            return _dictionary.TryGetValue(key, out set) && set.Remove(value);
        }

        public int Count { get { return _dictionary.Sum(kvp => kvp.Value.Count); } }
        public bool IsReadOnly { get { return false; } }
        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public void Add(TKey key, TValue value)
        {
            _dictionary.AddOrUpdate(key,
                                    k => new ConcurrentSet<TValue>() {value},
                                    (k, set) =>
                                        {
                                            set.Add(value);
                                            return set;
                                        });
        }

        public bool Remove(TKey key)
        {
            ConcurrentSet<TValue> set;
            return _dictionary.TryRemove(key, out set);
        }

        public bool TryGetValue(TKey key, out ISet<TValue> value)
        {
            ConcurrentSet<TValue> set;
            if (!_dictionary.TryGetValue(key, out set))
            {
                value = default(ISet<TValue>);
                return false;
            }
            else
            {
                value = set;
                return true;
            }
        }

        public ISet<TValue> this[TKey key]
        {
            get { return _dictionary[key]; }
        }

        public IEnumerable<TKey> Keys { get { return _dictionary.Keys; } }
        public IEnumerable<TValue> Values { get { return _dictionary.Values.SelectMany(kvp => kvp.Values); } }
    }
}
