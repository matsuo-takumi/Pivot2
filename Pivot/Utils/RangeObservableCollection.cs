using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Pivot.Utils
{
    /// <summary>
    /// ObservableCollection with batch update support to avoid flickering
    /// </summary>
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        public RangeObservableCollection() : base() { }
        
        public RangeObservableCollection(IEnumerable<T> collection) : base(collection) { }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }

        /// <summary>
        /// Replace all items with new collection in single notification
        /// </summary>
        public void ReplaceRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            _suppressNotification = true;
            try
            {
                Clear();
                foreach (var item in collection)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Add multiple items with single notification
        /// </summary>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            _suppressNotification = true;
            try
            {
                foreach (var item in collection)
                {
                    Add(item);
                }
            }
            finally
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
