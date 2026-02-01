using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using WinUIEditor;

namespace Pivot.CodeModule.Behaviors
{
    public static class EditorScrollBehavior
    {
        #region EnableInertia
        public static readonly DependencyProperty EnableInertiaProperty =
            DependencyProperty.RegisterAttached("EnableInertia", typeof(bool), typeof(EditorScrollBehavior), new PropertyMetadata(false, OnEnableInertiaChanged));

        public static bool GetEnableInertia(DependencyObject obj) => (bool)obj.GetValue(EnableInertiaProperty);
        public static void SetEnableInertia(DependencyObject obj, bool value) => obj.SetValue(EnableInertiaProperty, value);

        private static void OnEnableInertiaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditorControl editor)
            {
                if ((bool)e.NewValue)
                {
                    editor.PointerWheelChanged += Editor_PointerWheelChanged;
                }
                else
                {
                    editor.PointerWheelChanged -= Editor_PointerWheelChanged;
                }
            }
        }

        private static double _wheelVelocityY = 0;
        private static double _wheelAccumulatorY = 0;
        private static DispatcherTimer? _inertiaTimer;
        private const double Friction = 0.90; // Decay factor per tick

        private static void Editor_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (sender is CodeEditorControl editor)
            {
                var properties = e.GetCurrentPoint(editor).Properties;
                var delta = properties.MouseWheelDelta;
                
                if (delta != 0)
                {
                    // Map delta (usually multiples of 120) to line velocity
                    // 120 delta -> ~3-5 lines of impulse
                    double impulse = (delta / 120.0) * -5.0; 
                    _wheelVelocityY += impulse;
                    
                    _activeEditor = editor;
                    StartInertiaTimer();
                    
                    // Mark as handled to prevent default jumpy scroll
                    e.Handled = true;
                }
            }
        }

        private static void StartInertiaTimer()
        {
            if (_inertiaTimer == null)
            {
                _inertiaTimer = new DispatcherTimer();
                _inertiaTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
                _inertiaTimer.Tick += InertiaTimer_Tick;
            }
            _inertiaTimer.Start();
        }

        private static void InertiaTimer_Tick(object? sender, object e)
        {
            if (_activeEditor == null) 
            {
                _inertiaTimer?.Stop();
                return;
            }

            // Apply velocity to accumulator
            _wheelAccumulatorY += _wheelVelocityY;
            
            // Calculate integer lines to scroll
            int linesToScroll = (int)_wheelAccumulatorY;
            if (linesToScroll != 0)
            {
                _activeEditor.Editor.LineScroll(0, linesToScroll);
                _wheelAccumulatorY -= linesToScroll;
            }

            // Apply friction
            _wheelVelocityY *= Friction;

            // Stop if velocity is negligible
            if (Math.Abs(_wheelVelocityY) < 0.05 && Math.Abs(_wheelAccumulatorY) < 0.05)
            {
                _wheelVelocityY = 0;
                _wheelAccumulatorY = 0;
                _inertiaTimer?.Stop();
            }
        }
        #endregion

        #region EnableMiddleClickScroll
        public static readonly DependencyProperty EnableMiddleClickScrollProperty =
            DependencyProperty.RegisterAttached("EnableMiddleClickScroll", typeof(bool), typeof(EditorScrollBehavior), new PropertyMetadata(false, OnEnableMiddleClickScrollChanged));

        public static bool GetEnableMiddleClickScroll(DependencyObject obj) => (bool)obj.GetValue(EnableMiddleClickScrollProperty);
        public static void SetEnableMiddleClickScroll(DependencyObject obj, bool value) => obj.SetValue(EnableMiddleClickScrollProperty, value);

        private static void OnEnableMiddleClickScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeEditorControl editor)
            {
                if ((bool)e.NewValue)
                {
                    editor.PointerPressed += Editor_PointerPressed;
                    editor.PointerReleased += Editor_PointerReleased;
                    editor.PointerMoved += Editor_PointerMoved;
                }
                else
                {
                    editor.PointerPressed -= Editor_PointerPressed;
                    editor.PointerReleased -= Editor_PointerReleased;
                    editor.PointerMoved -= Editor_PointerMoved;
                }
            }
        }

        private static bool _isGrabScrolling = false;
        private static Windows.Foundation.Point _lastMousePoint;
        private static double _grabAccumulatorY = 0;
        private static double _grabAccumulatorX = 0;
        private const double PixelsPerLine = 20.0; // Estimate
        private const double PixelsPerChar = 10.0; // Estimate

        private static void Editor_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var ptr = e.GetCurrentPoint(sender as UIElement);
            if (ptr.Properties.IsMiddleButtonPressed)
            {
                if (sender is CodeEditorControl editor)
                {
                    _isGrabScrolling = true;
                    _lastMousePoint = ptr.Position;
                    _activeEditor = editor;
                    _grabAccumulatorX = 0;
                    _grabAccumulatorY = 0;
                    
                    editor.CapturePointer(e.Pointer);
                    e.Handled = true;
                    System.Diagnostics.Debug.WriteLine("Grab Scroll: START");
                }
            }
        }

        private static void Editor_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isGrabScrolling)
            {
                if (sender is CodeEditorControl editor)
                {
                    editor.ReleasePointerCapture(e.Pointer);
                }
                _isGrabScrolling = false;
                _activeEditor = null;
                System.Diagnostics.Debug.WriteLine("Grab Scroll: END");
            }
        }

        private static void Editor_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isGrabScrolling && _activeEditor != null)
            {
                var currentPoint = e.GetCurrentPoint(_activeEditor).Position;
                var dx = currentPoint.X - _lastMousePoint.X;
                var dy = currentPoint.Y - _lastMousePoint.Y;

                _lastMousePoint = currentPoint;

                // Vertical scroll (lines)
                _grabAccumulatorY += dy; // dragging down moves text down
                int lines = (int)(_grabAccumulatorY / PixelsPerLine);
                if (lines != 0)
                {
                    _activeEditor.Editor.LineScroll(0, -lines); // Negative scrolls text up? Wait.
                    _grabAccumulatorY -= lines * PixelsPerLine;
                }

                // Horizontal scroll
                _grabAccumulatorX += dx;
                int chars = (int)(_grabAccumulatorX / PixelsPerChar);
                if (chars != 0)
                {
                    _activeEditor.Editor.LineScroll(-chars, 0);
                    _grabAccumulatorX -= chars * PixelsPerChar;
                }
                
                e.Handled = true;
            }
        }
        #endregion
    }
}
