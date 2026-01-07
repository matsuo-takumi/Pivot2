using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pivot.Models;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace Pivot.Controls
{
    public class MasonryLayout : VirtualizingLayout
    {
        private readonly List<Rect> _layoutCache = new();
        private double _lastAvailableWidth = -1;
        private int _lastItemCount = -1;

        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(MasonryLayout), new PropertyMetadata(200.0, OnPropertyChanged));

        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        public static readonly DependencyProperty ColumnSpacingProperty =
            DependencyProperty.Register(nameof(ColumnSpacing), typeof(double), typeof(MasonryLayout), new PropertyMetadata(8.0, OnPropertyChanged));

        public double ColumnSpacing
        {
            get => (double)GetValue(ColumnSpacingProperty);
            set => SetValue(ColumnSpacingProperty, value);
        }

        public static readonly DependencyProperty RowSpacingProperty =
            DependencyProperty.Register(nameof(RowSpacing), typeof(double), typeof(MasonryLayout), new PropertyMetadata(8.0, OnPropertyChanged));

        public double RowSpacing
        {
            get => (double)GetValue(RowSpacingProperty);
            set => SetValue(RowSpacingProperty, value);
        }

        public static readonly DependencyProperty FooterHeightProperty =
            DependencyProperty.Register(nameof(FooterHeight), typeof(double), typeof(MasonryLayout), new PropertyMetadata(32.0, OnPropertyChanged));

        public double FooterHeight
        {
            get => (double)GetValue(FooterHeightProperty);
            set => SetValue(FooterHeightProperty, value);
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var layout = (MasonryLayout)d;
            layout.InvalidateMeasure();
        }

        protected override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
        {
            if (context.ItemCount == 0)
            {
                _layoutCache.Clear();
                return new Size(0, 0);
            }

            // Re-calculate layout if needed (cache invalidation)
            // Invalid conditions: ItemCount changed, Width changed significantly, or cache mismatch
            if (_layoutCache.Count != context.ItemCount || 
                Math.Abs(_lastAvailableWidth - availableSize.Width) > 1.0 || 
                _lastAvailableWidth < 0)
            {
                CalculateLayout(context, availableSize.Width);
                _lastAvailableWidth = availableSize.Width;
                _lastItemCount = context.ItemCount;
            }

            var realizationRect = context.RealizationRect;
            
            // Loop through ALL items to determine visible range and realizing them
            // Optimization: Could use binary search if sorted by Y, but Masonry isn't strictly sorted by Y.
            // Linear scan O(N) is fast enough for 10k items (calculating intersection).
            int itemsToProcess = Math.Min(context.ItemCount, _layoutCache.Count);
            for (int i = 0; i < itemsToProcess; i++)
            {
                var rect = _layoutCache[i];
                
                // Check intersection with RealizationRect (Visible area + buffer)
                if (rect.Bottom >= realizationRect.Top && rect.Top <= realizationRect.Bottom)
                {
                    // Item is within realization range
                    var element = context.GetOrCreateElementAt(i); 
                    element.Measure(new Size(rect.Width, rect.Height));
                }
                else
                {
                    // Item is outside. VirtualizingLayout automatically recycles elements 
                    // that are not requested via GetOrCreateElementAt in this pass.
                }
            }

            // Calculate total extent height
            double maxY = 0;
            if (_layoutCache.Count > 0)
            {
                 // To avoid O(N) scan every time, track maxY during CalculateLayout?
                 // But calculating here ensures accuracy.
                 // For 10k items loop is fine.
                 foreach(var r in _layoutCache)
                 {
                     if (r.Bottom > maxY) maxY = r.Bottom;
                 }
            }
            
            return new Size(availableSize.Width, maxY);
        }

        protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
        {
            var realizationRect = context.RealizationRect;
            
            int itemsToProcess = Math.Min(context.ItemCount, _layoutCache.Count);
            for (int i = 0; i < itemsToProcess; i++)
            {
                var rect = _layoutCache[i];
                 // Check intersection again to arrange only realized items
                if (rect.Bottom >= realizationRect.Top && rect.Top <= realizationRect.Bottom)
                {
                    var element = context.GetOrCreateElementAt(i);
                    element.Arrange(rect);
                }
            }
            return finalSize;
        }

        private void CalculateLayout(VirtualizingLayoutContext context, double availableWidth)
        {
            _layoutCache.Clear();
            
            // Determine column count fitting in available width
            // Logic: Fill width with columns of at least ColumnWidth
            double totalSpacing = ColumnSpacing; // Spacing between columns? 
            // If N columns, (N-1) spaces? Or outer padding? 
            // Let's assume N columns have N-1 gaps of ColumnSpacing.
            
            // Width = N * W + (N-1) * S
            // Width + S = N * (W + S)
            // N = (Width + S) / (W + S)
            
            int columnCount = (int)Math.Max(1, Math.Floor((availableWidth + ColumnSpacing) / (ColumnWidth + ColumnSpacing)));
            
            // Calculate exact column width to fill the space
            // W_actual = (Width - (N-1)*S) / N
            double actualColumnWidth = (availableWidth - (columnCount - 1) * ColumnSpacing) / columnCount;
            if (actualColumnWidth < 1) actualColumnWidth = 1;

            double[] columnHeights = new double[columnCount];

            for (int i = 0; i < context.ItemCount; i++)
            {
                // 1. Find shortest column
                int minCol = 0;
                double minH = columnHeights[0];
                for(int c = 1; c < columnCount; c++)
                {
                    if (columnHeights[c] < minH)
                    {
                        minH = columnHeights[c];
                        minCol = c;
                    }
                }

                // 2. Calculate Item Height
                double aspect = 1.0;
                var item = context.GetItemAt(i);
                if (item is TemplateItem templateItem && templateItem.AspectRatio > 0)
                {
                    aspect = templateItem.AspectRatio;
                }
                else if (item is AssetEntity assetEntity && assetEntity.AspectRatio.HasValue && assetEntity.AspectRatio.Value > 0)
                {
                    aspect = assetEntity.AspectRatio.Value;
                }
                
                double h = (actualColumnWidth / aspect) + FooterHeight;
                
                // 3. Set Position
                double x = minCol * (actualColumnWidth + ColumnSpacing);
                double y = minH;
                
                _layoutCache.Add(new Rect(x, y, actualColumnWidth, h));
                
                // 4. Update Column Height
                columnHeights[minCol] += h + RowSpacing;
            }
        }


        public void Invalidate()
        {
            // Clear layout cache to force full recalculation
            // This is critical when the collection changes (e.g., directory filter)
            // even if the item count happens to be the same
            _layoutCache.Clear();
            _lastAvailableWidth = -1;
            _lastItemCount = -1;
            InvalidateMeasure();
        }
    }
}
