using System;
using System.Collections.Generic;
using Windows.UI;

namespace Pivot.Models
{
    /// <summary>
    /// Represents a PBR material preset for 3D rendering
    /// </summary>
    public class MaterialPreset
    {
        /// <summary>
        /// Display name of the preset
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Base color (albedo)
        /// </summary>
        public Color Albedo { get; set; } = Color.FromArgb(255, 180, 180, 180);

        /// <summary>
        /// Metallic value (0.0 = dielectric, 1.0 = metal)
        /// </summary>
        public float Metallic { get; set; } = 0.0f;

        /// <summary>
        /// Roughness value (0.0 = smooth/mirror, 1.0 = rough/matte)
        /// </summary>
        public float Roughness { get; set; } = 0.5f;

        /// <summary>
        /// Whether this is a user-created custom preset
        /// </summary>
        public bool IsCustom { get; set; } = false;

        /// <summary>
        /// Clone this preset
        /// </summary>
        public MaterialPreset Clone()
        {
            return new MaterialPreset
            {
                Name = Name,
                Albedo = Albedo,
                Metallic = Metallic,
                Roughness = Roughness,
                IsCustom = IsCustom
            };
        }

        /// <summary>
        /// Get default built-in presets
        /// </summary>
        public static List<MaterialPreset> GetDefaultPresets()
        {
            return new List<MaterialPreset>
            {
                new MaterialPreset
                {
                    Name = "Clay",
                    Albedo = Color.FromArgb(255, 200, 180, 160),
                    Metallic = 0.0f,
                    Roughness = 0.8f
                },
                new MaterialPreset
                {
                    Name = "Plastic",
                    Albedo = Color.FromArgb(255, 220, 220, 220),
                    Metallic = 0.0f,
                    Roughness = 0.4f
                },
                new MaterialPreset
                {
                    Name = "Metal",
                    Albedo = Color.FromArgb(255, 192, 192, 192),
                    Metallic = 1.0f,
                    Roughness = 0.3f
                },
                new MaterialPreset
                {
                    Name = "Gold",
                    Albedo = Color.FromArgb(255, 255, 215, 0),
                    Metallic = 1.0f,
                    Roughness = 0.2f
                },
                new MaterialPreset
                {
                    Name = "Copper",
                    Albedo = Color.FromArgb(255, 184, 115, 51),
                    Metallic = 1.0f,
                    Roughness = 0.25f
                },
                new MaterialPreset
                {
                    Name = "Rubber",
                    Albedo = Color.FromArgb(255, 50, 50, 50),
                    Metallic = 0.0f,
                    Roughness = 0.9f
                },
                new MaterialPreset
                {
                    Name = "Glass",
                    Albedo = Color.FromArgb(255, 240, 240, 255),
                    Metallic = 0.0f,
                    Roughness = 0.1f
                },
                new MaterialPreset
                {
                    Name = "Chrome",
                    Albedo = Color.FromArgb(255, 230, 230, 230),
                    Metallic = 1.0f,
                    Roughness = 0.05f
                }
            };
        }
    }
}
