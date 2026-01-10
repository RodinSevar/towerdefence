extends Node3D

var speed = 20.0
var damage = 0.0
var target: Node3D = null

func setup(start_pos: Vector3, target_enemy: Node3D, dmg: float):
	position = start_pos
	target = target_enemy
	damage = dmg
	
	# Visuals (Simple Yellow Sphere)
	var mesh_inst = MeshInstance3D.new()
	var sphere = SphereMesh.new()
	sphere.radius = 0.2
	sphere.height = 0.4
	mesh_inst.mesh = sphere
	
	var material = StandardMaterial3D.new()
	material.albedo_color = Color(1, 1, 0) # Yellow
	material.emission_enabled = true
	material.emission = Color(1, 1, 0)
	mesh_inst.material_override = material
	
	add_child(mesh_inst)

func _process(delta):
	if not is_instance_valid(target):
		queue_free() # Target died/vanished before we hit
		return
		
	var direction = (target.position - position).normalized()
	var distance = position.distance_to(target.position)
	
	# Move
	position += direction * speed * delta
	
	# Hit check
	if distance < 0.5:
		hit_target()

func hit_target():
	if target.has_method("take_damage"):
		target.take_damage(damage)
	queue_free()
