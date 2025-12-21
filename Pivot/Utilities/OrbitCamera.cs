using System;
using System.Numerics;

namespace Pivot.Utilities
{
    /// <summary>
    /// Orbit camera that rotates around a target point
    /// </summary>
    public class OrbitCamera
    {
        private float _yaw = 0.0f;      // Horizontal rotation (radians)
        private float _pitch = 0.3f;    // Vertical rotation (radians)
        private float _distance = 3.0f; // Distance from target
        
        public Vector3 Target { get; set; } = Vector3.Zero;
        
        /// <summary>
        /// Field of view in radians (default 45 degrees)
        /// </summary>
        public float Fov { get; set; } = MathF.PI / 4f;
        
        public float Distance
        {
            get => _distance;
            set => _distance = Math.Clamp(value, 0.1f, 10000f);
        }
        
        public float Yaw
        {
            get => _yaw;
            set => _yaw = value;
        }
        
        public float Pitch
        {
            get => _pitch;
            set => _pitch = value; // No clamp - allow full 360 degree rotation
        }
        
        /// <summary>
        /// Get the current camera position based on spherical coordinates (Y-up coordinate system)
        /// </summary>
        public Vector3 Position
        {
            get
            {
                // Y-up coordinate system
                float x = _distance * MathF.Cos(_pitch) * MathF.Sin(_yaw);
                float y = _distance * MathF.Sin(_pitch);
                float z = _distance * MathF.Cos(_pitch) * MathF.Cos(_yaw);
                return Target + new Vector3(x, y, z);
            }
        }
        
        /// <summary>
        /// Get the view matrix for this camera
        /// </summary>
        public Matrix4x4 GetViewMatrix()
        {
            // Y-up coordinate system
            var up = Vector3.UnitY;
            
            // If we're looking from below (pitch outside normal range), flip up
            if (MathF.Cos(_pitch) < 0)
            {
                up = -up;
            }
            
            return Matrix4x4.CreateLookAt(Position, Target, up);
        }
        
        /// <summary>
        /// Rotate the camera around the target
        /// </summary>
        public void Rotate(float deltaYaw, float deltaPitch)
        {
            Yaw += deltaYaw;
            Pitch += deltaPitch;
        }
        
        /// <summary>
        /// Pan the camera (move target)
        /// </summary>
        public void Pan(float deltaX, float deltaY)
        {
            // Calculate right and up vectors in camera space (Y-up)
            var forward = Vector3.Normalize(Target - Position);
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var up = Vector3.Cross(right, forward);
            
            // Move target based on pan input
            Target += right * deltaX * _distance * 0.01f;
            Target += up * deltaY * _distance * 0.01f;
        }
        
        /// <summary>
        /// Zoom in/out
        /// </summary>
        public void Zoom(float delta)
        {
            Distance -= delta * _distance * 0.1f;
        }
        
        /// <summary>
        /// Fit camera to bounding box - ensures the entire model is visible
        /// </summary>
        public void FitToBounds(BoundingBox bounds, float padding = 1.2f)
        {
            Target = bounds.Center;
            
            // Calculate distance needed to fit the bounding sphere in view
            // Using: distance = radius / sin(fov/2)
            float radius = bounds.Radius;
            if (radius < 0.001f) radius = 1.0f; // Prevent zero radius
            
            float halfFov = Fov / 2f;
            float distanceNeeded = (radius * padding) / MathF.Sin(halfFov);
            
            // Ensure minimum distance
            Distance = MathF.Max(distanceNeeded, radius * 2f);
            
            // Set a nice viewing angle (45° yaw, 30° pitch)
            _yaw = MathF.PI / 4f;
            _pitch = MathF.PI / 6f;
        }
        
        /// <summary>
        /// Reset camera to default position
        /// </summary>
        public void Reset()
        {
            Target = Vector3.Zero;
            Distance = 3.0f;
            _yaw = 0.0f;
            _pitch = 0.3f;
        }
    }
}

