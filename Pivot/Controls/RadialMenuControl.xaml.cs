using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Windows.Input;

namespace Pivot.Controls;

public sealed partial class RadialMenuControl : UserControl
{
    public event EventHandler<string>? MenuItemSelected;

    public ICommand? LayoutCommand
    {
        get => (ICommand?)GetValue(LayoutCommandProperty);
        set => SetValue(LayoutCommandProperty, value);
    }
    public static readonly DependencyProperty LayoutCommandProperty =
        DependencyProperty.Register(nameof(LayoutCommand), typeof(ICommand), typeof(RadialMenuControl), new PropertyMetadata(null));

    public ICommand? ToggleSortOrderCommand
    {
        get => (ICommand?)GetValue(ToggleSortOrderCommandProperty);
        set => SetValue(ToggleSortOrderCommandProperty, value);
    }
    public static readonly DependencyProperty ToggleSortOrderCommandProperty =
        DependencyProperty.Register(nameof(ToggleSortOrderCommand), typeof(ICommand), typeof(RadialMenuControl), new PropertyMetadata(null));

    public event EventHandler? SortRequested;

    private Button? _hoveredButton;

    public RadialMenuControl()
    {
        InitializeComponent();
        RootCanvas.PointerMoved += RootCanvas_PointerMoved;
        RootCanvas.PointerReleased += RootCanvas_PointerReleased;
    }

    private void RootCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(RootCanvas).Position;
        
        Button? targetButton = null;
        
        // Horizontal detection based on X position
        if (position.Y >= 70 && position.Y <= 130) // Within button height range
        {
            if (position.X >= 10 && position.X < 118)
                targetButton = LayoutButton;
            else if (position.X >= 118 && position.X < 226)
                targetButton = SortButton;
            else if (position.X >= 226 && position.X < 326)
                targetButton = OrderButton;
        }
        
        if (targetButton != _hoveredButton)
        {
            if (_hoveredButton != null)
                _hoveredButton.BorderThickness = new Thickness(1);
            
            _hoveredButton = targetButton;
            
            if (_hoveredButton != null)
                _hoveredButton.BorderThickness = new Thickness(3);
        }
    }

    private void RootCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_hoveredButton == LayoutButton)
            LayoutCommand?.Execute(null);
        else if (_hoveredButton == OrderButton)
            ToggleSortOrderCommand?.Execute(null);
        else if (_hoveredButton == SortButton)
            SortRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MenuButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            _hoveredButton = button;
            button.BorderThickness = new Thickness(3);
        }
    }

    private void MenuButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button && _hoveredButton == button)
        {
            _hoveredButton = null;
            button.BorderThickness = new Thickness(1);
        }
    }

    private void LayoutButton_Click(object sender, RoutedEventArgs e)
    {
        LayoutCommand?.Execute(null);
        MenuItemSelected?.Invoke(this, "Layout");
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        SortRequested?.Invoke(this, EventArgs.Empty);
        MenuItemSelected?.Invoke(this, "Sort");
    }

    private void OrderButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSortOrderCommand?.Execute(null);
        MenuItemSelected?.Invoke(this, "Order");
    }

    public void UpdateIcons(string layoutIcon, string sortIcon, string orderIcon)
    {
        LayoutIcon.Glyph = layoutIcon;
        SortIcon.Glyph = sortIcon;
        OrderIcon.Glyph = orderIcon;
    }
}
