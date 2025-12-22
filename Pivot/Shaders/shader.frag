#version 450

layout(location = 0) in vec3 fragNormal;
layout(location = 1) in vec2 fragTexCoord;
layout(location = 2) in float fragDepth;
layout(location = 3) in vec3 fragWorldPos;

layout(binding = 1) uniform ShadingParams {
    int mode;           // 0=WorldNormal, 1=Depth, 2=Lit, 3=UV
    float nearPlane;
    float farPlane;
    float lightIntensity;
    vec3 lightDirection;
    float _padding1;
    vec3 lightColor;
    float _padding2;
} shading;

layout(location = 0) out vec4 outColor;

void main() {
    vec3 normal = normalize(fragNormal);
    
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
    else {
        // Fallback to normal
        outColor = vec4(normal * 0.5 + 0.5, 1.0);
    }
}
