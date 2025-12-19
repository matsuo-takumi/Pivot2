using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace Pivot.Controls
{
    public sealed partial class ModelViewerControl : UserControl
    {
        public static readonly DependencyProperty ViewerBackgroundProperty =
            DependencyProperty.Register(nameof(ViewerBackground), typeof(Microsoft.UI.Xaml.Media.Brush),
                typeof(ModelViewerControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ModelPathProperty =
            DependencyProperty.Register(nameof(ModelPath), typeof(string),
                typeof(ModelViewerControl),
                new PropertyMetadata(null, OnModelPathChanged));

        public Microsoft.UI.Xaml.Media.Brush ViewerBackground
        {
            get => (Microsoft.UI.Xaml.Media.Brush)GetValue(ViewerBackgroundProperty);
            set => SetValue(ViewerBackgroundProperty, value);
        }

        public string? ModelPath
        {
            get => (string?)GetValue(ModelPathProperty);
            set => SetValue(ModelPathProperty, value);
        }

        public ModelViewerControl()
        {
            this.InitializeComponent();
        }

        private void UpAxisComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: Implement Up Axis change in Phase 3
        }

        private static void OnModelPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // TODO: Implement Model Loading in Phase 2
        }
    }
}
