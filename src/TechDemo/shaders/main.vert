#version 460

layout(set = 0, binding = 0) uniform UBO {
    mat4 projection;
    mat4 view;
} ubo;

layout(location = 0) in vec3 VertPosition;

layout(location = 1) in vec2 VertUV;
layout(location = 0) out vec2 FragUV;

void main(){
    FragUV = VertUV;
    gl_Position = ubo.projection * ubo.view * vec4(VertPosition.x, VertPosition.y, VertPosition.z, 1.0);
}