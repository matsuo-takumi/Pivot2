using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Pivot.ViewModels
{
    /// <summary>
    /// 再利用可能な選択管理ViewModel（エクスプローラー風の動作）
    /// </summary>
    public partial class SelectionManagerViewModel<T> : ObservableObject where T : class
    {
        private readonly HashSet<T> _selectedItems = new();
        private T? _lastSelectedItem;
        public T? LastSelectedItem => _lastSelectedItem;

        public ObservableCollection<T> SelectedItems { get; } = new();

        public int SelectedCount => _selectedItems.Count;

        public bool IsSelected(T item) => _selectedItems.Contains(item);

        /// <summary>
        /// アイテムを選択/選択解除（エクスプローラー風の動作）
        /// </summary>
        public void SelectItem(T item, bool isCtrlPressed, bool isShiftPressed)
        {
            if (item == null) return;

            if (isShiftPressed && _lastSelectedItem != null)
            {
                // Shift+クリック: 範囲選択
                // 実装は呼び出し側でアイテムの順序を提供する必要がある
                // ここでは基本実装のみ
                if (!_selectedItems.Contains(item))
                {
                    _selectedItems.Add(item);
                    SelectedItems.Add(item);
                }
            }
            else if (isCtrlPressed)
            {
                // Ctrl+クリック: 追加/削除
                if (_selectedItems.Contains(item))
                {
                    _selectedItems.Remove(item);
                    SelectedItems.Remove(item);
                }
                else
                {
                    _selectedItems.Add(item);
                    SelectedItems.Add(item);
                }
            }
            else
            {
                // 通常クリック: 単一選択
                ClearSelection();
                _selectedItems.Add(item);
                SelectedItems.Add(item);
            }

            _lastSelectedItem = item;
            OnPropertyChanged(nameof(SelectedCount));
        }

        /// <summary>
        /// 範囲選択（Shift+クリック用）
        /// </summary>
        public void SelectRange(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                if (!_selectedItems.Contains(item))
                {
                    _selectedItems.Add(item);
                    SelectedItems.Add(item);
                }
            }
            OnPropertyChanged(nameof(SelectedCount));
        }

        /// <summary>
        /// すべての選択を解除
        /// </summary>
        public void ClearSelection()
        {
            _selectedItems.Clear();
            SelectedItems.Clear();
            _lastSelectedItem = null;
            OnPropertyChanged(nameof(SelectedCount));
        }

        /// <summary>
        /// すべて選択
        /// </summary>
        public void SelectAll(IEnumerable<T> allItems)
        {
            ClearSelection();
            foreach (var item in allItems)
            {
                _selectedItems.Add(item);
                SelectedItems.Add(item);
            }
            OnPropertyChanged(nameof(SelectedCount));
        }

        /// <summary>
        /// 選択状態を更新（外部から呼び出し用）
        /// </summary>
        public void SetSelected(T item, bool isSelected)
        {
            if (item == null) return;

            if (isSelected)
            {
                if (!_selectedItems.Contains(item))
                {
                    _selectedItems.Add(item);
                    SelectedItems.Add(item);
                }
            }
            else
            {
                if (_selectedItems.Contains(item))
                {
                    _selectedItems.Remove(item);
                    SelectedItems.Remove(item);
                }
            }
            OnPropertyChanged(nameof(SelectedCount));
        }
    }
}

