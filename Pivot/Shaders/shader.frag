#version 450

layout(location = 0) in vec3 fragNormal;
layout(location = 1) in vec2 fragTexCoord;
layout(location = 2) in float fragDepth;
layout(location = 3) in vec3 fragWorldPos;
layout(location = 4) in vec4 fragColor;

layout(binding = 1) uniform ShadingParams {
    int mode;           // 0=WorldNormal, 1=Depth, 2=Lit, 3=UV, 4=Material, 5=VertexColor, 6=Texture, 7=Blend
    float nearPlane;
    float farPlane;
    float _padding0;
    
    // Key Light (Main directional light)
    vec3 keyLightDirection;
    float keyLightIntensity;
    vec3 keyLightColor;
    float _padding1;
    
    // Ambient Light
    vec3 ambientColor;
    float ambientIntensity;
    
    // Rim Light
    vec3 rimLightColor;
    float rimLightIntensity;
    
    // Back Light
    vec3 backLightColor;
    float backLightIntensity;
    
    // PBR Material params
    vec3 materialAlbedo;
    float materialMetallic;
    float materialRoughness;
    float _padding2;
    float _padding3;
    float _padding4;
    
    // Camera position for correct view direction
    vec3 cameraPosition;
    float _padding5;
    
    // Toggle flags
    int useTexture;
    int useVertexColor;
    int useUVChecker;
    int useMaterial;
} shading;

layout(location = 0) out vec4 outColor;

// Constants
const float PI = 3.14159265359;

// PBR Functions
float DistributionGGX(vec3 N, vec3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    
    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    
    return num / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    
    return num / denom;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    
    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// Calculate contribution from a single directional light
vec3 calculateDirectionalLight(vec3 N, vec3 V, vec3 L, vec3 lightColor, float intensity, vec3 albedo, float metallic, float roughness, vec3 F0) {
    // Early exit for surfaces facing away from light
    float NdotL = max(dot(N, L), 0.0);
    if (NdotL <= 0.0) return vec3(0.0);
    
    float NdotV = max(dot(N, V), 0.001); // Prevent division issues
    
    // Check for degenerate half vector (when V and L are nearly opposite)
    vec3 H_unnorm = V + L;
    float H_len = length(H_unnorm);
    if (H_len < 0.001) {
        // V and L are nearly opposite, skip specular, only diffuse
        vec3 kD = vec3(1.0) * (1.0 - metallic);
        return kD * albedo / PI * lightColor * intensity * NdotL;
    }
    vec3 H = H_unnorm / H_len;
    
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
    
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;
    
    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * NdotV * NdotL + 0.0001;
    vec3 specular = numerator / denominator;
    
    return (kD * albedo / PI + specular) * lightColor * intensity * NdotL;
}

vec3 calculateMultiLightPBR(vec3 N, vec3 V, vec3 albedo, float metallic, float roughness) {
    // Fresnel reflectance at normal incidence
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);
    
    vec3 Lo = vec3(0.0);
    
    // Key Light (main directional light)
    vec3 keyDir = normalize(shading.keyLightDirection);
    Lo += calculateDirectionalLight(N, V, keyDir, shading.keyLightColor, shading.keyLightIntensity, albedo, metallic, roughness, F0);
    
    // Back Light (opposite to key light)
    vec3 backDir = -keyDir;
    Lo += calculateDirectionalLight(N, V, backDir, shading.backLightColor, shading.backLightIntensity, albedo, metallic, roughness, F0);
    
    // Rim Light (based on view direction - Fresnel-like effect)
    // Only apply rim when the surface is lit (not facing away from camera)
    float NdotV_rim = max(dot(N, V), 0.0);
    float rimFactor = 1.0 - NdotV_rim;
    rimFactor = pow(rimFactor, 4.0) * NdotV_rim; // Multiply by NdotV to prevent artifacts at grazing angles
    Lo += shading.rimLightColor * shading.rimLightIntensity * rimFactor;
    
    // Ambient Light
    vec3 ambient = shading.ambientColor * shading.ambientIntensity * albedo;
    
    return ambient + Lo;
}

// UV Checker pattern
vec3 getUVChecker() {
    float scale = 10.0;
    float checker = mod(floor(fragTexCoord.x * scale) + floor(fragTexCoord.y * scale), 2.0);
    return checker > 0.5 ? vec3(0.8, 0.8, 0.8) : vec3(0.3, 0.3, 0.3);
}

// Texture color (placeholder - using UV gradient for now)
vec3 getTextureColor() {
    return vec3(fragTexCoord, 0.5);
}

void main() {
    vec3 normal = normalize(fragNormal);
    // Correct view direction from camera position to fragment
    vec3 viewDir = normalize(shading.cameraPosition - fragWorldPos);
    
    if (shading.mode == 0) {
        // World Normal - map [-1,1] to [0,1]
        outColor = vec4(normal * 0.5 + 0.5, 1.0);
    }
    else if (shading.mode == 1) {
        // Depth - linear depth visualization
        float depth = clamp((fragDepth - shading.nearPlane) / (shading.farPlane - shading.nearPlane), 0.0, 1.0);
        outColor = vec4(vec3(1.0 - depth), 1.0);
    }
    else if (shading.mode == 2) {
        // Lit - dynamic directional lighting (simple version)
        vec3 lightDir = normalize(shading.keyLightDirection);
        float ambient = shading.ambientIntensity;
        float diffuse = max(dot(normal, lightDir), 0.0) * shading.keyLightIntensity;
        vec3 brightness = (ambient * shading.ambientColor + diffuse * shading.keyLightColor);
        outColor = vec4(brightness, 1.0);
    }
    else if (shading.mode == 3) {
        // UV Checkerboard
        outColor = vec4(getUVChecker(), 1.0);
    }
    else if (shading.mode == 4) {
        // Material (PBR) with multi-light
        vec3 result = calculateMultiLightPBR(normal, viewDir, shading.materialAlbedo, shading.materialMetallic, shading.materialRoughness);
        // HDR tonemapping
        result = result / (result + vec3(1.0));
        // Gamma correction
        result = pow(result, vec3(1.0/2.2));
        outColor = vec4(result, 1.0);
    }
    else if (shading.mode == 5) {
        // Vertex Color only
        outColor = fragColor;
    }
    else if (shading.mode == 6) {
        // Texture mode - using UV as color for now (placeholder for actual texture)
        outColor = vec4(getTextureColor(), 1.0);
    }
    else if (shading.mode == 7) {
        // Blend mode: average blend of enabled toggles with multi-light
        vec3 blendedAlbedo = vec3(0.0);
        float alpha = 1.0;
        int blendCount = 0;
        
        if (shading.useMaterial == 1) {
            blendedAlbedo += shading.materialAlbedo;
            blendCount++;
        }
        
        if (shading.useVertexColor == 1) {
            blendedAlbedo += fragColor.rgb;
            alpha *= fragColor.a;
            blendCount++;
        }
        
        if (shading.useTexture == 1) {
            blendedAlbedo += getTextureColor();
            blendCount++;
        }
        
        if (shading.useUVChecker == 1) {
            blendedAlbedo += getUVChecker();
            blendCount++;
        }
        
        if (blendCount > 0) {
            blendedAlbedo /= float(blendCount);
        } else {
            blendedAlbedo = shading.materialAlbedo;
        }
        
        // Apply multi-light PBR
        vec3 result = calculateMultiLightPBR(normal, viewDir, blendedAlbedo, shading.materialMetallic, shading.materialRoughness);
        
        // HDR tonemapping
        result = result / (result + vec3(1.0));
        // Gamma correction
        result = pow(result, vec3(1.0/2.2));
        
        outColor = vec4(result, alpha);
    }
    else {
        // Fallback to normal
        outColor = vec4(normal * 0.5 + 0.5, 1.0);
    }
}
