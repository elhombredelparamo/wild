shader_type spatial;

uniform sampler2D tex_oceano : source_color;
uniform sampler2D tex_costa : source_color;
uniform sampler2D tex_pradera : source_color;
uniform sampler2D tex_bosque : source_color;
uniform sampler2D tex_montana : source_color;

varying vec3 v_vertex;
varying vec4 v_color;

void vertex() {
	v_vertex = VERTEX;
	v_color = COLOR;
}

void fragment() {
	// UVs basadas en el mundo para evitar estiramientos y costuras
	// Usamos un factor de escala para que la textura se repita adecuadamente
	vec2 world_uv = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz * 0.5;
	
	vec3 c_oceano = texture(tex_oceano, world_uv).rgb;
	vec3 c_costa = texture(tex_costa, world_uv).rgb;
	vec3 c_pradera = texture(tex_pradera, world_uv).rgb;
	vec3 c_bosque = texture(tex_bosque, world_uv).rgb;
	vec3 c_montana = texture(tex_montana, world_uv).rgb;
	
	// Mezclamos basándonos en los canales de color del vértice (Splat Map)
	// Base es Océano
	vec3 final_color = c_oceano;
	
	// Mezclamos Costa (R)
	final_color = mix(final_color, c_costa, v_color.r);
	// Mezclamos Pradera (G)
	final_color = mix(final_color, c_pradera, v_color.g);
	// Mezclamos Bosque (B)
	final_color = mix(final_color, c_bosque, v_color.b);
	// Mezclamos Montana (A)
	final_color = mix(final_color, c_montana, v_color.a);
	
	ALBEDO = final_color;
	ROUGHNESS = 0.8;
}
