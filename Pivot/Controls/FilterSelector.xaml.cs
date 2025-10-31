using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using Pivot.ViewModels;
using System.Collections.ObjectModel;

namespace Pivot.Controls
{
    public sealed partial class FilterSelector : UserControl
    {
        public FilterSelector()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty FiltersProperty = DependencyProperty.Register(
            "Filters", typeof(ObservableCollection<FilterViewModel>), typeof(FilterSelector), new PropertyMetadata(null));

        public ObservableCollection<FilterViewModel> Filters
        {
            get => (ObservableCollection<FilterViewModel>)GetValue(FiltersProperty);
            set => SetValue(FiltersProperty, value);
        }

        public static readonly DependencyProperty SelectedFilterProperty = DependencyProperty.Register(
            "SelectedFilter", typeof(FilterViewModel), typeof(FilterSelector), new PropertyMetadata(null));

        public FilterViewModel SelectedFilter
        {
            get => (FilterViewModel)GetValue(SelectedFilterProperty);
            set => SetValue(SelectedFilterProperty, value);
        }
    }
}
