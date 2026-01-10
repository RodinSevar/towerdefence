extends Node3D

var speed = 10.0
var current_path = []
var target_position: Vector3
var max_health = 100.0
var current_health = 100.0
var gold_reward = 5

func _ready():
	# Simple visual for the enemy (Red Sphere)
	var mesh_inst = MeshInstance3D.new()
	var sphere = SphereMesh.new()
	sphere.radius = 0.5
	sphere.height = 1.0
	mesh_inst.mesh = sphere
	
	var material = StandardMaterial3D.new()
	material.albedo_color = Color(1, 0, 0) # Red
	mesh_inst.material_override = material
	
	add_child(mesh_inst)

func take_damage(amount: float):
	current_health -= amount
	# Visual feedback: Flash white or shrink
	scale *= 0.9 
	
	if current_health <= 0:
		die()

func die():
	# Reward the player
	# Assuming the parent is the GameMap/GameWorld node that has the add_gold function
	var game_map = get_parent()
	if game_map.has_method("add_gold"):
		game_map.add_gold(gold_reward)
		
	queue_free() # Remove from game

func set_path(path_points: Array):
	# Path points are already in World Coordinates
	current_path = []
	for point in path_points:
		# Map 2D (x,y) path to 3D (x, 0.5, z) world
		var world_pos = Vector3(point.x, 0.5, point.y)
		current_path.append(world_pos)
	
	# Fix: The path includes the starting cell (where we are now).
	# We should skip it so we don't walk backwards to the center of our current tile.
	if current_path.size() > 1:
		current_path.pop_front()
	
	if current_path.size() > 0:
		target_position = current_path[0]

func _process(delta):
	if current_path.is_empty():
		return

	var direction = (target_position - position).normalized()
	var distance = position.distance_to(target_position)

	# Move towards target
	position += direction * speed * delta

	# Check if reached the point
	if distance < 0.2:
		current_path.pop_front() # Remove reached point
		if current_path.size() > 0:
			target_position = current_path[0]
		else:
			# Reached the end!
			var game_map = get_parent()
			if game_map.has_method("lose_life"):
				game_map.lose_life()
			
			queue_free() # Destroy enemy
