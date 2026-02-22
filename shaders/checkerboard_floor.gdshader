shader_type spatial;
render_mode world_vertex_coords;

uniform float grid_size = 2.0;

void fragment() {
	vec2 xz = VERTEX.xz;
	float cx = floor(xz.x / grid_size);
	float cz = floor(xz.y / grid_size);
	float check = mod(cx + cz, 2.0);
	ALBEDO = check > 0.5 ? vec3(1.0, 1.0, 1.0) : vec3(0.12, 0.12, 0.12);
	ROUGHNESS = 0.9;
	METALLIC = 0.0;
}

