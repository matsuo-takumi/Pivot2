using Microsoft.UI.Xaml.Controls;
using System;
using System.Numerics;
using Pivot.Utilities;

namespace Pivot.Controls
{
    public sealed partial class AxisGizmo : UserControl
    {
        private const double AxisLength = 25;
        private const double CenterX = 40;
        private const double CenterY = 40;

        public AxisGizmo()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Update gizmo to match camera orientation
        /// </summary>
        public void UpdateFromCamera(OrbitCamera camera)
        {
            if (camera == null) return;
            
            // Get camera's view matrix and extract rotation
            var viewMatrix = camera.GetViewMatrix();
            
            // Transform axis directions by view rotation
            var xDir = Vector3.TransformNormal(Vector3.UnitX, viewMatrix);
            var yDir = Vector3.TransformNormal(Vector3.UnitY, viewMatrix);
            var zDir = Vector3.TransformNormal(Vector3.UnitZ, viewMatrix);
            
            // Project to 2D screen space (simple orthographic projection)
            UpdateAxis(XAxis, XLabel, xDir, "#E53935");
            UpdateAxis(YAxis, YLabel, yDir, "#43A047");
            UpdateAxis(ZAxis, ZLabel, zDir, "#1E88E5");
        }

        private void UpdateAxis(Microsoft.UI.Xaml.Shapes.Line line, TextBlock label, Vector3 dir, string color)
        {
            // Project 3D direction to 2D
            double x2 = CenterX + dir.X * AxisLength;
            double y2 = CenterY - dir.Y * AxisLength; // Flip Y for screen coords
            
            line.X2 = x2;
            line.Y2 = y2;
            
            // Position label at end of axis
            Canvas.SetLeft(label, x2 - 4);
            Canvas.SetTop(label, y2 - 6);
            
            // Fade if pointing away from camera (Z < 0)
            var opacity = dir.Z > 0 ? 1.0 : 0.4;
            line.Opacity = opacity;
            label.Opacity = opacity;
        }
    }
}
