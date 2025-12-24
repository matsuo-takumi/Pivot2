using System.Collections.Generic;
using Windows.UI;

namespace Pivot.Models
{
    /// <summary>
    /// Represents a lighting preset for 3D viewport
    /// </summary>
    public class LightingPreset
    {
        public string Name { get; set; } = "Default";
        public bool IsCustom { get; set; } = false;

        // Key Light
        public float KeyIntensity { get; set; } = 1.0f;
        public Color KeyColor { get; set; } = Color.FromArgb(255, 255, 255, 255);
        public float KeyYaw { get; set; } = 0.3f;
        public float KeyPitch { get; set; } = 1.0f;

        // Ambient Light
        public float AmbientIntensity { get; set; } = 0.1f;
        public Color AmbientColor { get; set; } = Color.FromArgb(255, 102, 102, 128);

        // Rim Light
        public float RimIntensity { get; set; } = 0.3f;
        public Color RimColor { get; set; } = Color.FromArgb(255, 204, 230, 255);

        // Back Light
        public float BackIntensity { get; set; } = 0.2f;
        public Color BackColor { get; set; } = Color.FromArgb(255, 128, 128, 153);

        public LightingPreset Clone()
        {
            return new LightingPreset
            {
                Name = Name,
                IsCustom = IsCustom,
                KeyIntensity = KeyIntensity,
                KeyColor = KeyColor,
                KeyYaw = KeyYaw,
                KeyPitch = KeyPitch,
                AmbientIntensity = AmbientIntensity,
                AmbientColor = AmbientColor,
                RimIntensity = RimIntensity,
                RimColor = RimColor,
                BackIntensity = BackIntensity,
                BackColor = BackColor
            };
        }

        public static List<LightingPreset> GetDefaultPresets()
        {
            return new List<LightingPreset>
            {
                new LightingPreset
                {
                    Name = "Studio",
                    KeyIntensity = 1.0f,
                    KeyColor = Color.FromArgb(255, 255, 255, 255),
                    KeyYaw = 0.5f,
                    KeyPitch = 1.0f,
                    AmbientIntensity = 0.15f,
                    AmbientColor = Color.FromArgb(255, 120, 120, 140),
                    RimIntensity = 0.4f,
                    RimColor = Color.FromArgb(255, 200, 220, 255),
                    BackIntensity = 0.3f,
                    BackColor = Color.FromArgb(255, 100, 100, 120)
                },
                new LightingPreset
                {
                    Name = "Outdoor",
                    KeyIntensity = 1.5f,
                    KeyColor = Color.FromArgb(255, 255, 250, 230),
                    KeyYaw = 0.8f,
                    KeyPitch = 0.6f,
                    AmbientIntensity = 0.3f,
                    AmbientColor = Color.FromArgb(255, 135, 206, 235),
                    RimIntensity = 0.2f,
                    RimColor = Color.FromArgb(255, 255, 200, 150),
                    BackIntensity = 0.1f,
                    BackColor = Color.FromArgb(255, 100, 150, 200)
                },
                new LightingPreset
                {
                    Name = "Dramatic",
                    KeyIntensity = 2.0f,
                    KeyColor = Color.FromArgb(255, 255, 240, 220),
                    KeyYaw = -0.5f,
                    KeyPitch = 0.8f,
                    AmbientIntensity = 0.05f,
                    AmbientColor = Color.FromArgb(255, 40, 40, 60),
                    RimIntensity = 0.6f,
                    RimColor = Color.FromArgb(255, 255, 200, 150),
                    BackIntensity = 0.4f,
                    BackColor = Color.FromArgb(255, 80, 60, 100)
                },
                new LightingPreset
                {
                    Name = "Soft",
                    KeyIntensity = 0.8f,
                    KeyColor = Color.FromArgb(255, 255, 255, 255),
                    KeyYaw = 0.3f,
                    KeyPitch = 1.2f,
                    AmbientIntensity = 0.4f,
                    AmbientColor = Color.FromArgb(255, 180, 180, 200),
                    RimIntensity = 0.15f,
                    RimColor = Color.FromArgb(255, 220, 220, 240),
                    BackIntensity = 0.15f,
                    BackColor = Color.FromArgb(255, 150, 150, 180)
                },
                new LightingPreset
                {
                    Name = "Night",
                    KeyIntensity = 0.5f,
                    KeyColor = Color.FromArgb(255, 200, 220, 255),
                    KeyYaw = -0.8f,
                    KeyPitch = 0.5f,
                    AmbientIntensity = 0.08f,
                    AmbientColor = Color.FromArgb(255, 30, 30, 60),
                    RimIntensity = 0.3f,
                    RimColor = Color.FromArgb(255, 100, 150, 255),
                    BackIntensity = 0.1f,
                    BackColor = Color.FromArgb(255, 50, 50, 100)
                }
            };
        }
    }
}
