using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Rummy.Util;

// Adapted from implementation by Brian Lagunas https://brianlagunas.com/write-a-sortable-observablecollection-for-wpf/

public class SortableObservableCollection<T> : ObservableCollection<T>
{
   public SortableObservableCollection(List<T> list) : base(list) {}

   public SortableObservableCollection(IEnumerable<T> collection) : base(collection) {}

   public void Replace(IEnumerable<T> newItems) {
      Clear();
      foreach (var item in newItems) {
         Add(item);
      }
   }

   public void Sort<TKey>(Func<T, TKey> keySelector) {
      ApplySort(Items.OrderBy(keySelector));
   }

   public void Sort<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer) {
      ApplySort(Items.OrderBy(keySelector, comparer));
   }

   private void ApplySort(IEnumerable<T> sortedItems) {
      var sortedItemsList = sortedItems.ToList();

      foreach (var item in sortedItemsList) {
         Move(IndexOf(item), sortedItemsList.IndexOf(item));
      }
   }
}