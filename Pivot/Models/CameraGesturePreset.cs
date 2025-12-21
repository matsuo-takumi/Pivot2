using System;

namespace Pivot.Models
{
    /// <summary>
    /// Camera gesture preset for 3D viewport navigation
    /// </summary>
    public enum CameraGesturePreset
    {
        /// <summary>
        /// Maya style: Alt+LMB=Rotate, Alt+MMB=Pan, Alt+RMB=Zoom, Scroll=Zoom
        /// </summary>
        Maya,
        
        /// <summary>
        /// Houdini style: LMB=Rotate, MMB=Pan, RMB=Zoom, Scroll=Zoom
        /// (Space+LMB=Rotate, Space+MMB=Pan, Space+RMB=Zoom in viewport)
        /// </summary>
        Houdini,
        
        /// <summary>
        /// Blender style: MMB=Rotate, Shift+MMB=Pan, Ctrl+MMB=Zoom, Scroll=Zoom
        /// </summary>
        Blender
    }

    /// <summary>
    /// Configuration for camera gesture behavior
    /// </summary>
    public class CameraGestureConfig
    {
        public bool RotateRequiresAlt { get; set; }
        public bool RotateRequiresShift { get; set; }
        public bool RotateRequiresCtrl { get; set; }
        public MouseButton RotateButton { get; set; }
        
        public bool PanRequiresAlt { get; set; }
        public bool PanRequiresShift { get; set; }
        public bool PanRequiresCtrl { get; set; }
        public MouseButton PanButton { get; set; }
        
        public bool ZoomRequiresAlt { get; set; }
        public bool ZoomRequiresShift { get; set; }
        public bool ZoomRequiresCtrl { get; set; }
        public MouseButton ZoomButton { get; set; }
        
        public static CameraGestureConfig FromPreset(CameraGesturePreset preset)
        {
            return preset switch
            {
                CameraGesturePreset.Maya => new CameraGestureConfig
                {
                    // Alt + LMB = Rotate
                    RotateButton = MouseButton.Left,
                    RotateRequiresAlt = true,
                    
                    // Alt + MMB = Pan
                    PanButton = MouseButton.Middle,
                    PanRequiresAlt = true,
                    
                    // Alt + RMB = Zoom
                    ZoomButton = MouseButton.Right,
                    ZoomRequiresAlt = true
                },
                
                CameraGesturePreset.Houdini => new CameraGestureConfig
                {
                    // LMB = Rotate (no modifier needed)
                    RotateButton = MouseButton.Left,
                    RotateRequiresAlt = false,
                    
                    // MMB = Pan
                    PanButton = MouseButton.Middle,
                    PanRequiresAlt = false,
                    
                    // RMB = Zoom
                    ZoomButton = MouseButton.Right,
                    ZoomRequiresAlt = false
                },
                
                CameraGesturePreset.Blender => new CameraGestureConfig
                {
                    // MMB = Rotate
                    RotateButton = MouseButton.Middle,
                    RotateRequiresAlt = false,
                    
                    // Shift + MMB = Pan
                    PanButton = MouseButton.Middle,
                    PanRequiresShift = true,
                    
                    // Ctrl + MMB = Zoom (or scroll)
                    ZoomButton = MouseButton.Middle,
                    ZoomRequiresCtrl = true
                },
                
                _ => FromPreset(CameraGesturePreset.Maya)
            };
        }
    }

    public enum MouseButton
    {
        Left,
        Middle,
        Right
    }
}
