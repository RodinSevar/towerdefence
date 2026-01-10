extends Node3D

# Wintermaul Logic: The Grid
# The map is divided into a grid of cells. 
# 0 = Empty (Walkable)
# 1 = Wall/Tower (Blocked)

const GRID_SIZE = 32
const CELL_SIZE = 2.0 # In 3D world units

# Game Economy
var gold = 100
var lives = 20
var selected_tower_type = "Normal"
var tower_costs = {
	"Normal": 15,
	"Ice": 30,
	"Sniper": 50
}

# UI References
var hud_script = load("res://scripts/HUD.gd")
var hud = null

# Wave Configuration
# Each wave: { "count": int, "interval": float, "hp": float, "speed": float, "reward": int, "color": Color, "scale": float }
var waves = [
	{ "count": 10, "interval": 1.0, "hp": 50, "speed": 8.0, "reward": 5, "color": Color(1, 0, 0), "scale": 1.0 },       # Wave 1: Red
	{ "count": 15, "interval": 0.8, "hp": 80, "speed": 10.0, "reward": 6, "color": Color(0, 0.5, 1), "scale": 0.8 },     # Wave 2: Blue (Fast/Small)
	{ "count": 5, "interval": 1.5, "hp": 300, "speed": 6.0, "reward": 15, "color": Color(0.5, 0, 0.5), "scale": 2.0 },   # Wave 3: Purple (Boss/Big)
]
var current_wave_index = -1
var enemies_remaining_to_spawn = 0
var wave_spawn_timer = 0.0
var is_wave_active = false

# Visual settings
@export var ground_mesh: MeshInstance3D
@export var cursor_mesh: MeshInstance3D

var astar = AStarGrid2D.new()
var enemies = [] # Track active enemies
var occupied_cells = {} # Dictionary to track built towers { Vector2i: Node3D }

func _ready():
	setup_ui()
	setup_visuals()
	setup_grid()
	# Test pathfinding debug
	print("Grid ready. Testing path from (0,0) to (31,31)...")
	var path = get_path_route(Vector2i(0,0), Vector2i(31,31))
	print("Path found: ", path)
	
	start_next_wave()

func setup_grid():
	# Configure the AStarGrid2D
	astar.region = Rect2i(0, 0, GRID_SIZE, GRID_SIZE)
	astar.cell_size = Vector2(CELL_SIZE, CELL_SIZE)
	
	# WC3 Style: Allow diagonals, but NOT if squeezing through two walls.
	astar.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES
	# Use Euclidean heuristic for more natural diagonal paths
	astar.default_compute_heuristic = AStarGrid2D.HEURISTIC_EUCLIDEAN
	astar.default_estimate_heuristic = AStarGrid2D.HEURISTIC_EUCLIDEAN
	
	# Center the path points in the cells
	astar.offset = Vector2(CELL_SIZE / 2, CELL_SIZE / 2)
	
	astar.update() # Build the initial grid
	
	print("Map initialized with size: ", GRID_SIZE, "x", GRID_SIZE)
	print("Starting Gold: ", gold)

func start_next_wave():
	current_wave_index += 1
	if current_wave_index >= waves.size():
		print("ALL WAVES COMPLETED! YOU WIN!")
		if hud: hud.show_win()
		return
		
	var wave_data = waves[current_wave_index]
	enemies_remaining_to_spawn = wave_data["count"]
	is_wave_active = true
	
	if hud: hud.update_wave(current_wave_index + 1)
	print("Starting Wave ", current_wave_index + 1)

func setup_ui():
	hud = hud_script.new()
	add_child(hud)
	
	# Connect Restart Button
	hud.restart_button.pressed.connect(restart_game)
	# Connect Tower Selection
	hud.tower_selected.connect(on_tower_selected)
	
	# Initialize HUD values
	hud.update_gold(gold)
	hud.update_lives(lives)

func on_tower_selected(type: String):
	selected_tower_type = type
	print("Builder switched to: ", type)

func restart_game():
	print("Restarting...")
	get_tree().paused = false # Unpause!
	get_tree().reload_current_scene()

func _process(delta):
	handle_input()
	
	if is_wave_active and enemies_remaining_to_spawn > 0:
		wave_spawn_timer -= delta
		if wave_spawn_timer <= 0:
			spawn_enemy()
			var wave_data = waves[current_wave_index]
			wave_spawn_timer = wave_data["interval"]

func spawn_enemy():
	var wave_data = waves[current_wave_index]
	
	var enemy_script = load("res://scripts/Enemy.gd")
	var enemy = enemy_script.new()
	add_child(enemy)
	
	# Configure Stats based on Wave
	enemy.max_health = wave_data["hp"]
	enemy.current_health = wave_data["hp"]
	enemy.base_speed = wave_data["speed"] # Set Base Speed
	enemy.speed = wave_data["speed"]
	enemy.gold_reward = wave_data["reward"]
	
	# Apply Visuals
	enemy.setup_visuals(wave_data["color"], wave_data["scale"])
	
	# Start at (0,0)
	enemy.position = Vector3(1.0, 0.5, 1.0) 
	
	# Give initial path
	var path = get_path_route(Vector2i(0,0), Vector2i(31,31))
	enemy.set_path(path)
	
	enemies.append(enemy)
	
	enemies_remaining_to_spawn -= 1
	
	if enemies_remaining_to_spawn <= 0:
		is_wave_active = false
		print("Wave Spawning Finished. Waiting for clear...")

func enemy_died(enemy):
	# Check if wave is cleared
	# We need to wait a frame for the enemy to actually leave the array/tree
	await get_tree().process_frame
	
	var active_count = 0
	for e in enemies:
		if is_instance_valid(e) and not e.is_queued_for_deletion():
			active_count += 1
			
	if active_count == 0 and not is_wave_active:
		print("Wave Cleared!")
		# Auto start next wave after 3 seconds
		await get_tree().create_timer(3.0).timeout
		start_next_wave()

func setup_visuals():
	# Create a simple plane for the ground
	if not ground_mesh:
		var plane = PlaneMesh.new()
		plane.size = Vector2(GRID_SIZE * CELL_SIZE, GRID_SIZE * CELL_SIZE)
		
		ground_mesh = MeshInstance3D.new()
		ground_mesh.mesh = plane
		add_child(ground_mesh)
		
		# Center the ground. 
		# Grid (0,0) is usually top-left, but PlaneMesh is centered by default.
		# We'll offset it so (0,0) corresponds to the corner.
		ground_mesh.position = Vector3(GRID_SIZE * CELL_SIZE / 2.0, 0, GRID_SIZE * CELL_SIZE / 2.0)
		
	# Create a cursor to show where we are building
	if not cursor_mesh:
		var box = BoxMesh.new()
		box.size = Vector3(CELL_SIZE, 0.5, CELL_SIZE)
		
		var material = StandardMaterial3D.new()
		material.albedo_color = Color(0, 1, 0, 0.5) # Semi-transparent Green
		material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		box.material = material
		
		cursor_mesh = MeshInstance3D.new()
		cursor_mesh.mesh = box
		add_child(cursor_mesh)

func handle_input():
	# Raycast from camera to find grid position
	var camera = get_viewport().get_camera_3d()
	if not camera: return
	
	var mouse_pos = get_viewport().get_mouse_position()
	var ray_origin = camera.project_ray_origin(mouse_pos)
	var ray_direction = camera.project_ray_normal(mouse_pos)
	
	# Intersect with the ground plane (Y=0)
	# Math: O + D * t = P. We want P.y = 0.
	# O.y + D.y * t = 0  =>  t = -O.y / D.y
	if ray_direction.y == 0: return # Parallel to ground
	
	var t = -ray_origin.y / ray_direction.y
	if t < 0: return # Behind camera
	
	var intersection = ray_origin + ray_direction * t
	
	# Convert world position to grid coordinates
	var grid_x = floor(intersection.x / CELL_SIZE)
	var grid_y = floor(intersection.z / CELL_SIZE) # 3D Z is 2D Y
	var grid_pos = Vector2i(grid_x, grid_y)
	
	# Move cursor
	if is_valid_pos(grid_pos):
		cursor_mesh.visible = true
		cursor_mesh.position = Vector3(
			grid_pos.x * CELL_SIZE + CELL_SIZE/2, 
			0.25, 
			grid_pos.y * CELL_SIZE + CELL_SIZE/2
		)
		
		# Build on click
		if Input.is_action_just_pressed("ui_accept") or Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
			if build_tower(grid_pos):
				place_tower_visual(grid_pos)
	else:
		cursor_mesh.visible = false

# Call this when the player builds a tower
func build_tower(grid_pos: Vector2i) -> bool:
	if not is_valid_pos(grid_pos):
		return false
	
	# Check affordability
	var cost = tower_costs[selected_tower_type]
	if gold < cost:
		print("Not enough gold! Cost: ", cost, " Have: ", gold)
		return false
	
	# Fix: Check if we already built here
	if occupied_cells.has(grid_pos):
		print("Cell already occupied!")
		return false
	
	# Check if any enemy is currently standing on this tile
	for enemy in enemies:
		if is_instance_valid(enemy):
			var enemy_grid_pos = Vector2i(
				floor(enemy.position.x / CELL_SIZE), 
				floor(enemy.position.z / CELL_SIZE)
			)
			if enemy_grid_pos == grid_pos:
				print("Cannot build: Enemy in the way!")
				return false
		
	# Check if this blocks the path (Wintermaul rule: Cannot block completely)
	# For now, we just set it solid to test
	astar.set_point_solid(grid_pos, true)
	
	# Verify if enemies still have a path
	if not check_path_exists():
		print("Cannot build here! Blocks path.")
		astar.set_point_solid(grid_pos, false) # Revert
		return false
		
	print("Tower built at: ", grid_pos)
	
	# Update all enemies with new path
	update_enemy_paths()
	
	# Pay for the tower
	gold -= cost
	if hud: hud.update_gold(gold)
	print("Tower built! Gold remaining: ", gold)
	
	return true

func add_gold(amount: int):
	gold += amount
	if hud: hud.update_gold(gold)
	print("Gold added: ", amount, ". Total: ", gold)

func lose_life():
	lives -= 1
	if hud: hud.update_lives(lives)
	print("Life lost! Remaining: ", lives)
	
	if lives <= 0:
		game_over()

func game_over():
	print("GAME OVER")
	# For now, just pause or restart
	get_tree().paused = true
	if hud: hud.update_lives(0)

func update_enemy_paths():
	for enemy in enemies:
		if is_instance_valid(enemy):
			# Calculate path from enemy's current grid position to end
			var current_grid_x = floor(enemy.position.x / CELL_SIZE)
			var current_grid_y = floor(enemy.position.z / CELL_SIZE)
			var start_pos = Vector2i(current_grid_x, current_grid_y)
			var end_pos = Vector2i(31, 31)
			
			var new_path = get_path_route(start_pos, end_pos)
			enemy.set_path(new_path)

func is_valid_pos(pos: Vector2i) -> bool:
	return astar.region.has_point(pos)

func check_path_exists() -> bool:
	# In Wintermaul, enemies go from Start to End.
	# Let's assume (0,0) is Start and (31,31) is End for now.
	var path = astar.get_id_path(Vector2i(0,0), Vector2i(31,31))
	return not path.is_empty()

func get_path_route(start: Vector2i, end: Vector2i) -> Array:
	return astar.get_point_path(start, end)

func place_tower_visual(grid_pos: Vector2i):
	# Create the Tower Logic Node
	var tower_script = load("res://scripts/Tower.gd")
	var tower_node = Node3D.new()
	tower_node.set_script(tower_script)
	add_child(tower_node)
	
	# Configure it!
	tower_node.configure(selected_tower_type)
	
	# Track it!
	occupied_cells[grid_pos] = tower_node
	
	# Position the tower
	tower_node.position = Vector3(
		grid_pos.x * CELL_SIZE + CELL_SIZE/2, 
		0.0, 
		grid_pos.y * CELL_SIZE + CELL_SIZE/2
	)
	
	# Create the Visuals (Box) as a child of the Tower Node
	var box = BoxMesh.new()
	box.size = Vector3(CELL_SIZE, 2.0, CELL_SIZE)
	var mesh_inst = MeshInstance3D.new()
	mesh_inst.mesh = box
	
	# Color code the towers
	var material = StandardMaterial3D.new()
	if selected_tower_type == "Normal":
		material.albedo_color = Color(0.5, 0.5, 0.5) # Grey
	elif selected_tower_type == "Ice":
		material.albedo_color = Color(0, 1, 1) # Cyan
	elif selected_tower_type == "Sniper":
		material.albedo_color = Color(0.2, 0.2, 0.2) # Dark Grey
		box.size.y = 4.0 # Taller!
		
	mesh_inst.material_override = material
	
	# Lift the visual up so it sits on the ground (Height 2.0 / 2 = 1.0)
	mesh_inst.position.y = box.size.y / 2.0 
	
	tower_node.add_child(mesh_inst)
