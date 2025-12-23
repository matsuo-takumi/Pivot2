#version 450

layout(location = 0) in vec3 fragNormal;
layout(location = 1) in vec2 fragTexCoord;
layout(location = 2) in float fragDepth;
layout(location = 3) in vec3 fragWorldPos;
layout(location = 4) in vec4 fragColor;

layout(binding = 1) uniform ShadingParams {
    int mode;           // 0=WorldNormal, 1=Depth, 2=Lit, 3=UV, 4=Material, 5=VertexColor, 6=Texture, 7=Combined
    float nearPlane;
    float farPlane;
    float lightIntensity;
    vec3 lightDirection;
    float _padding1;
    vec3 lightColor;
    float _padding2;
    // PBR Material params
    vec3 materialAlbedo;
    float materialMetallic;
    float materialRoughness;
    float _padding3;
    float _padding4;
    float _padding5;
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

vec3 calculatePBR(vec3 N, vec3 V, vec3 albedo, float metallic, float roughness) {
    vec3 L = normalize(shading.lightDirection);
    vec3 H = normalize(V + L);
    
    // Fresnel reflectance at normal incidence
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);
    
    // Cook-Torrance BRDF
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
    
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;
    
    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
    vec3 specular = numerator / denominator;
    
    float NdotL = max(dot(N, L), 0.0);
    
    vec3 Lo = (kD * albedo / PI + specular) * shading.lightColor * shading.lightIntensity * NdotL;
    
    // Ambient
    vec3 ambient = vec3(0.03) * albedo;
    
    return ambient + Lo;
}

void main() {
    vec3 normal = normalize(fragNormal);
    vec3 viewDir = normalize(-fragWorldPos);
    
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
        // Lit - dynamic directional lighting
        vec3 lightDir = normalize(shading.lightDirection);
        float ambient = 0.15;
        float diffuse = max(dot(normal, lightDir), 0.0) * 0.85 * shading.lightIntensity;
        vec3 brightness = (ambient + diffuse) * shading.lightColor;
        outColor = vec4(brightness, 1.0);
    }
    else if (shading.mode == 3) {
        // UV Checkerboard
        float scale = 10.0;
        float checker = mod(floor(fragTexCoord.x * scale) + floor(fragTexCoord.y * scale), 2.0);
        vec3 color = checker > 0.5 ? vec3(0.8, 0.8, 0.8) : vec3(0.3, 0.3, 0.3);
        outColor = vec4(color, 1.0);
    }
    else if (shading.mode == 4) {
        // Material (PBR)
        vec3 result = calculatePBR(normal, viewDir, shading.materialAlbedo, shading.materialMetallic, shading.materialRoughness);
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
        // When texture sampler is added, this will use texture2D(texSampler, fragTexCoord)
        vec3 uvColor = vec3(fragTexCoord, 0.5);
        outColor = vec4(uvColor, 1.0);
    }
    else if (shading.mode == 7) {
        // Combined: Material × Vertex Color
        vec3 combinedAlbedo = shading.materialAlbedo * fragColor.rgb;
        vec3 result = calculatePBR(normal, viewDir, combinedAlbedo, shading.materialMetallic, shading.materialRoughness);
        // HDR tonemapping
        result = result / (result + vec3(1.0));
        // Gamma correction
        result = pow(result, vec3(1.0/2.2));
        outColor = vec4(result, fragColor.a);
    }
    else {
        // Fallback to normal
        outColor = vec4(normal * 0.5 + 0.5, 1.0);
    }
}
