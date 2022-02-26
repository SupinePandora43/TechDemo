#version 460

layout(set = 1, binding = 0) uniform sampler2D Texture;

layout(location = 0) in vec2 FragUV;

layout(location = 0) out vec4 Color;

void main(){
    Color = texture(Texture, FragUV);
}