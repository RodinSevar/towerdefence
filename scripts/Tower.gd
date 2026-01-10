extends Node3D

var attack_range = 8.0
var damage = 25.0
var fire_rate = 0.5 # Seconds between shots
var time_since_last_shot = 0.0

# References
var target_enemy = null

func _process(delta):
	time_since_last_shot += delta
	
	# 1. If we have a target, check if it is still valid
	if target_enemy:
		if not is_instance_valid(target_enemy) or position.distance_to(target_enemy.position) > attack_range:
			target_enemy = null
	
	# 2. If no target, find a new one
	if not target_enemy:
		find_target()
	
	# 3. If we have a target, aim and shoot
	if target_enemy:
		# Aim (Optional visual)
		look_at(target_enemy.position, Vector3.UP)
		
		# Shoot
		if time_since_last_shot >= fire_rate:
			shoot()
			time_since_last_shot = 0.0

func find_target():
	# Access the main game map to get the list of enemies
	# We assume the parent of the tower is the GameWorld (Node3D), which has the 'enemies' array.
	# Note: In a larger project, use Groups or a Manager.
	var game_map = get_parent()
	if not "enemies" in game_map:
		return
		
	var best_target = null
	var closest_dist = attack_range
	
	for enemy in game_map.enemies:
		if is_instance_valid(enemy):
			var dist = position.distance_to(enemy.position)
			if dist < closest_dist:
				closest_dist = dist
				best_target = enemy
	
	target_enemy = best_target

func shoot():
	if not is_instance_valid(target_enemy):
		return
		
	# Spawn Projectile
	var proj_script = load("res://scripts/Projectile.gd")
	var proj = proj_script.new()
	
	# Add to GameMap (parent of tower) so it flies independently
	get_parent().add_child(proj)
	
	# Initialize
	proj.setup(position, target_enemy, damage)
