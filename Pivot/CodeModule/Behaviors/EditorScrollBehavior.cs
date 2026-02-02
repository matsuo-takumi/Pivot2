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

        private static double _velocityY = 0; // Renamed from _wheelVelocityY
        private static double _velocityX = 0; // Added horizontal velocity
        private static double _accumulatorY = 0; // Renamed from _wheelAccumulatorY
        private static double _accumulatorX = 0; // Added horizontal accumulator
        private static DispatcherTimer? _inertiaTimer;
        private static CodeEditorControl? _activeEditor; 
        private const double Friction = 0.92; // Slightly lower friction for glide feel

        private static void Editor_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (sender is CodeEditorControl editor)
            {
                var properties = e.GetCurrentPoint(editor).Properties;
                var delta = properties.MouseWheelDelta;
                
                if (delta != 0)
                {
                    // Add impulse to existing velocity
                    // 120 delta -> ~4 lines of impulse
                    double impulse = (delta / 120.0) * -4.0; 
                    _velocityY += impulse;
                    
                    // Clamp max velocity to prevent insane scrolling
                    _velocityY = Math.Clamp(_velocityY, -50, 50);

                    _activeEditor = editor;
                    StartInertiaTimer();
                    
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

            // --- Y Axis ---
            _accumulatorY += _velocityY;
            int linesToScroll = (int)_accumulatorY;
            if (linesToScroll != 0)
            {
                _activeEditor.Editor.LineScroll(0, linesToScroll);
                _accumulatorY -= linesToScroll;
            }
            _velocityY *= Friction;

            // --- X Axis ---
            _accumulatorX += _velocityX;
            int charsToScroll = (int)_accumulatorX;
            if (charsToScroll != 0)
            {
                _activeEditor.Editor.LineScroll(charsToScroll, 0);
                _accumulatorX -= charsToScroll;
            }
            _velocityX *= Friction;

            // Stop if velocity is negligible
            if (Math.Abs(_velocityY) < 0.1 && Math.Abs(_velocityX) < 0.1 &&
                Math.Abs(_accumulatorY) < 1.0 && Math.Abs(_accumulatorX) < 1.0)
            {
                _velocityY = 0;
                _velocityX = 0;
                _accumulatorY = 0;
                _accumulatorX = 0;
                _inertiaTimer?.Stop();
            }
        }
        #endregion

        #region EnableRightClickScroll
        public static readonly DependencyProperty EnableRightClickScrollProperty =
            DependencyProperty.RegisterAttached("EnableRightClickScroll", typeof(bool), typeof(EditorScrollBehavior), new PropertyMetadata(false, OnEnableRightClickScrollChanged));

        public static bool GetEnableRightClickScroll(DependencyObject obj) => (bool)obj.GetValue(EnableRightClickScrollProperty);
        public static void SetEnableRightClickScroll(DependencyObject obj, bool value) => obj.SetValue(EnableRightClickScrollProperty, value);

        private static void OnEnableRightClickScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
        private static DateTime _lastDragTime;
        private static Windows.Foundation.Point _previousDragPoint; // for velocity calculation
        private const double PixelsPerLine = 20.0; // Estimate
        private const double PixelsPerChar = 10.0; // Estimate

        private static void Editor_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var ptr = e.GetCurrentPoint(sender as UIElement);
            if (ptr.Properties.IsRightButtonPressed)
            {
                if (sender is CodeEditorControl editor)
                {
                    _isGrabScrolling = true;
                    _hasDragged = false;
                    _lastMousePoint = ptr.Position;
                    _isGrabScrolling = true;
                    _hasDragged = false;
                    _lastMousePoint = ptr.Position;
                    _lastDragTime = DateTime.Now;
                    _activeEditor = editor;
                    
                    // Reset inertia on grab start
                    _velocityY = 0;
                    _velocityX = 0;
                    _accumulatorX = 0;
                    _accumulatorY = 0;
                    
                    editor.CapturePointer(e.Pointer);
                    e.Handled = true;
                    System.Diagnostics.Debug.WriteLine("Grab Scroll: START");
                }
            }
        }

        // Helper to detect right click without drag (for context menu if needed later)
        private static bool _hasDragged = false;

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
                
                // --- Release Inertia ---
                // If velocity is significant, start the timer
                if (Math.Abs(_velocityX) > 0.5 || Math.Abs(_velocityY) > 0.5)
                {
                     _activeEditor = sender as CodeEditorControl;
                     StartInertiaTimer();
                }

                System.Diagnostics.Debug.WriteLine($"Grab Scroll: END (VelX={_velocityX:F2}, VelY={_velocityY:F2})");
            }
        }

        private static void Editor_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isGrabScrolling && _activeEditor != null)
            {
                var currentPoint = e.GetCurrentPoint(_activeEditor).Position;
                var now = DateTime.Now;
                var dt = (now - _lastDragTime).TotalSeconds;

                var dx = currentPoint.X - _lastMousePoint.X;
                var dy = currentPoint.Y - _lastMousePoint.Y;

                if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2) _hasDragged = true;

                // --- Calculate Instantaneous Velocity ---
                // Velocity = DeltaLines / Tick(16ms)
                // We want compatibility with the Timer's decay logic
                // If we moved N lines in T seconds, velocity per tick (16ms) is...
                // But simplified: Velocity ~ DeltaLines (if events come at ~60hz)
                // For smoother release, we use a simple moving average or just current frame
                
                // Scroll immediately
                // Vertical
                if (dy != 0)
                {
                    double lines = dy / PixelsPerLine;
                    int linesInt = (int)(_accumulatorY + lines); 
                    // Wait, we are adding to Accumulator for direct scroll?
                    // Direct scroll logic needs its own accumulator if we want sub-pixel precision on drag?
                    // Actually, let's keep it simple: direct scroll what we can, keep remainder in accumulator
                    // AND update _velocityY for release.
                    
                    _activeEditor.Editor.LineScroll(0, -(int)lines); // Direct move
                    
                    // Update Velocity for Release (Inverted direction: Drag Down = Scroll Up, so Velocity is Negative of movement?)
                    // Wait, if I drag down, content moves down. 
                    // If I release while dragging down, it should continue down.
                    // Drag Down -> dy > 0. Scroll -> negative lines (up) to see content above?
                    // Wait, standard pan: Drag Down = View moves UP (content moves down).
                    // So LineScroll should be negative.
                    // Velocity should also be negative (scrolling up).
                    
                    if (dt > 0.001)
                    {
                        // Normalize to "lines per tick (16ms)"
                        double velocityLines = (lines / dt) * 0.016; 
                        
                        // Simple low-pass filter for smooth release
                        _velocityY = (_velocityY * 0.5) + (-velocityLines * 0.5); 
                    }
                }

                // Horizontal
                if (dx != 0)
                {
                    double chars = dx / PixelsPerChar;
                    _activeEditor.Editor.LineScroll(-(int)chars, 0);

                    if (dt > 0.001)
                    {
                        double velocityChars = (chars / dt) * 0.016;
                        _velocityX = (_velocityX * 0.5) + (-velocityChars * 0.5);
                    }
                }

                _lastMousePoint = currentPoint;
                _lastDragTime = now;
                
                e.Handled = true;
            }
        }
        #endregion
    }
}
