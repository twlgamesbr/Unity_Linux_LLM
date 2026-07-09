using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using UnityEngine;

namespace Unity.PlatformToolkit.Editor
{
    [Serializable]
    internal class ObservableSerializableList<T> : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged
    {
        [SerializeField]
        private List<T> items;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObservableSerializableList()
        {
            items = new List<T>();
        }

        public ObservableSerializableList(ICollection<T> collection)
        {
            items = collection.ToList();
        }

        protected void InvokeCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            items.Add(item);
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, items.Count  - 1));
        }

        public void Clear()
        {
            items.Clear();
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(T item)
        {
            return items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            items.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            var index = IndexOf(item);
            var removed = items.Remove(item);
            if (removed)
            {
                InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            }
            return removed;
        }

        public int Count => items.Count;

        public bool IsReadOnly => false;

        public int IndexOf(T item)
        {
            return items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            items.Insert(index, item);
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public void RemoveAt(int index)
        {
            var removedItem = items[index];
            items.RemoveAt(index);
            InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
        }

        public T this[int index]
        {
            get => items[index];
            set
            {
                var oldValue = items[index];
                if (!EqualityComparer<T>.Default.Equals(oldValue, value))
                {
                    items[index] = value;
                    InvokeCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, oldValue, index));
                }
            }
        }
    }
}
