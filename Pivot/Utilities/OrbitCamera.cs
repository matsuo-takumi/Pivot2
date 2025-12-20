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
        public Vector3 Up { get; } = Vector3.UnitZ;
        
        public float Distance
        {
            get => _distance;
            set => _distance = Math.Clamp(value, 0.5f, 100f);
        }
        
        public float Yaw
        {
            get => _yaw;
            set => _yaw = value;
        }
        
        public float Pitch
        {
            get => _pitch;
            set => _pitch = Math.Clamp(value, -MathF.PI / 2f + 0.1f, MathF.PI / 2f - 0.1f);
        }
        
        /// <summary>
        /// Get the current camera position based on spherical coordinates
        /// </summary>
        public Vector3 Position
        {
            get
            {
                float x = _distance * MathF.Cos(_pitch) * MathF.Cos(_yaw);
                float y = _distance * MathF.Cos(_pitch) * MathF.Sin(_yaw);
                float z = _distance * MathF.Sin(_pitch);
                return Target + new Vector3(x, y, z);
            }
        }
        
        /// <summary>
        /// Get the view matrix for this camera
        /// </summary>
        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Target, Up);
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
            // Calculate right and up vectors in camera space
            var forward = Vector3.Normalize(Target - Position);
            var right = Vector3.Normalize(Vector3.Cross(forward, Up));
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
    }
}
