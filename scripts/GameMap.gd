extends Node3D

# Wintermaul Logic: The Grid
# The map is divided into a grid of cells. 
# 0 = Empty (Walkable)
# 1 = Wall/Tower (Blocked)

const GRID_SIZE = 256
const CELL_SIZE = 1.0 # In 3D world units (Smaller cells for higher res)
const END_POINT = Vector2i(128, 250)

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
	{ "count": 20, "interval": 0.25, "hp": 40, "speed": 8.0, "reward": 3, "color": Color(1, 0, 0), "scale": 1.0 },       # Wave 1: Pack of 20 Red
	{ "count": 30, "interval": 0.15, "hp": 60, "speed": 12.0, "reward": 4, "color": Color(0, 0.5, 1), "scale": 0.8 },    # Wave 2: Swarm of 30 Blue
	{ "count": 5, "interval": 1.0, "hp": 400, "speed": 6.0, "reward": 20, "color": Color(0.5, 0, 0.5), "scale": 2.0 },   # Wave 3: 5 Bosses
]
var current_wave_index = -1
var enemies_remaining_to_spawn = 0
var wave_spawn_timer = 0.0
var is_wave_active = false
var spawn_counter = 0 # To track position in the spawn grid
var spawn_zones = [] # List of SpawnLocation objects

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
	setup_map_design() # Add obstacles
	setup_camera() # New: Auto-position camera
	# Test pathfinding debug
	print("Grid ready. Testing path from (0,0) to ", END_POINT, "...")
	var path = get_path_route(Vector2i(0,0), END_POINT)
	print("Path found: ", path)
	
	start_next_wave()

func setup_camera():
	var camera = get_viewport().get_camera_3d()
	if camera:
		# Center of 256x256 map is 128, 128
		# Position high up and pulled back
		camera.position = Vector3(128, 200, 240)
		camera.rotation_degrees = Vector3(-55, 0, 0)
		
		# Optional: If you want Orthographic (2D style)
		# camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		# camera.size = 260

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

func setup_map_design():
	# Load map data from JSON
	var file_path = "res://assets/map_data.json"
	if not FileAccess.file_exists(file_path):
		print("Error: Map data not found at ", file_path)
		return
		
	var file = FileAccess.open(file_path, FileAccess.READ)
	var content = file.get_as_text()
	var data = JSON.parse_string(content)
	
	if not data:
		print("Error parsing map JSON")
		return
		
	print("Loading Map: ", data["grid_width"], "x", data["grid_height"])
	
	var obstacles = data["obstacles"] # List of {x,y,w,h}
	
	for rect in obstacles:
		var x = rect["x"]
		var y = rect["y"]
		var w = rect["w"]
		var h = rect["h"]
		
		# 1. Update Grid
		# Loop through all cells in this rect
		for i in range(x, x + w):
			for j in range(y, y + h):
				var pos = Vector2i(i, j)
				astar.set_point_solid(pos, true)
				occupied_cells[pos] = true 
		
		# 2. Visuals: Create ONE big block for the whole rect (Optimization)
		var box = BoxMesh.new()
		box.size = Vector3(w * CELL_SIZE, 3.0, h * CELL_SIZE)
		
		var mesh_inst = MeshInstance3D.new()
		mesh_inst.mesh = box
		
		var material = StandardMaterial3D.new()
		material.albedo_color = Color(0.3, 0.25, 0.2) # Dark Brown Rock
		mesh_inst.material_override = material
		
		add_child(mesh_inst)
		
		# Center position
		var center_x = x + w / 2.0
		var center_y = y + h / 2.0
		
		mesh_inst.position = Vector3(
			center_x * CELL_SIZE, 
			1.5, 
			center_y * CELL_SIZE
		)

	# --- SPAWN ZONES ---
	# Define dynamic spawn zones from JSON
	spawn_zones.clear()
	
	if data.has("spawns"):
		for s_rect in data["spawns"]:
			var zone_rect = Rect2i(s_rect["x"], s_rect["y"], s_rect["w"], s_rect["h"])
			spawn_zones.append(SpawnLocation.new(zone_rect, CELL_SIZE))
	
	# Fallback if no spawns in JSON
	if spawn_zones.is_empty():
		print("Warning: No spawn zones in JSON. Using defaults.")
		spawn_zones.append(SpawnLocation.new(Rect2i(110, 5, 10, 10), CELL_SIZE))

	for zone in spawn_zones:
		var cells = zone.get_occupied_cells()
		for pos in cells:
			# Visual Marker (Red Floor)
			var marker = MeshInstance3D.new()
			var plane = PlaneMesh.new()
			plane.size = Vector2(CELL_SIZE, CELL_SIZE)
			marker.mesh = plane
			
			var material = StandardMaterial3D.new()
			material.albedo_color = Color(0.5, 0, 0, 0.5) # Semi-transparent Red
			material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			marker.material_override = material
			
			add_child(marker)
			marker.position = Vector3(
				pos.x * CELL_SIZE + CELL_SIZE/2, 
				0.05, # Slightly above ground to avoid z-fighting
				pos.y * CELL_SIZE + CELL_SIZE/2
			)
			
			# Mark as occupied so player CANNOT build here
			occupied_cells[pos] = marker

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
	
	# Instant Spawn (Wintermaul Style)
	spawn_counter = 0 # Reset grid position for new wave
	while enemies_remaining_to_spawn > 0:
		spawn_enemy()

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
	# Spawning is now instant in start_next_wave, so no timer logic needed here.

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
	
	# --- SPLIT SPAWN LOGIC ---
	# AlteDYNAMIC SPAWN LOGIC ---
	# 1. Round Robin selection of zones
	var zone_index = spawn_counter % spawn_zones.size()
	var zone = spawn_zones[zone_index]
	
	# 2. Calculate local index within that zone
	var index_in_zone = floor(spawn_counter / spawn_zones.size())
	
	# 3. Get World Position
	enemy.position = zone.get_spawn_pos(index_in_zone)
	
	# Increment counter
	spawn_counter += 1
	
	# 4. Pathfinding from spawn point
	var start_grid_x = floor(enemy.position.x / CELL_SIZE)
	var start_grid_y = floor(enemy.position.z / CELL_SIZE)
	
	# Give initial path to Bottom Center
	var path = get_path_route(Vector2i(start_grid_x, start_grid_y), END_POINT)
	enemy.set_path(path)
	
	# Wintermaul Style: Idle for a moment before rushing
	enemy.start_idle(2.0)
	
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
		# 2x2 Size
		box.size = Vector3(CELL_SIZE * 2.0, 0.5, CELL_SIZE * 2.0)
		
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
	if ray_direction.y == 0: return # Parallel to ground
	
	var t = -ray_origin.y / ray_direction.y
	if t < 0: return # Behind camera
	
	var intersection = ray_origin + ray_direction * t
	
	# Convert world position to grid coordinates
	var grid_x = floor(intersection.x / CELL_SIZE)
	var grid_y = floor(intersection.z / CELL_SIZE) 
	var grid_pos = Vector2i(grid_x, grid_y)
	
	# Move cursor (Centered on 2x2 block)
	# Top-Left is grid_pos. Center is grid_pos + 1.0 (in world units if cell=1.0)
	if can_build_at(grid_pos):
		cursor_mesh.visible = true
		cursor_mesh.position = Vector3(
			(grid_pos.x * CELL_SIZE) + CELL_SIZE, # Center of 2x2
			0.25, 
			(grid_pos.y * CELL_SIZE) + CELL_SIZE
		)
		
		# Build on click
		if Input.is_action_just_pressed("ui_accept") or Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
			if build_tower(grid_pos):
				place_tower_visual(grid_pos)
	else:
		cursor_mesh.visible = false

# Call this when the player builds a tower
func build_tower(grid_pos: Vector2i) -> bool:
	# Check 2x2 validity (Occupied? Enemy in way? Out of bounds?)
	if not can_build_at(grid_pos):
		print("Cannot build here (Blocked or Enemy inside)")
		return false
	
	# Check affordability
	var cost = tower_costs[selected_tower_type]
	if gold < cost:
		print("Not enough gold! Cost: ", cost, " Have: ", gold)
		return false
	
	# Temporarily block the 2x2 area to test pathing
	# We don't have the visual node yet, so pass null for occupied_cells value
	set_area_solid(grid_pos, true, null)
	
	# Verify if enemies still have a path
	if not check_path_exists():
		print("Cannot build here! Blocks path.")
		set_area_solid(grid_pos, false) # Revert
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
			
			var new_path = get_path_route(start_pos, END_POINT)
			enemy.set_path(new_path)

func is_valid_pos(pos: Vector2i) -> bool:
	return astar.region.has_point(pos)

# Helper to check 2x2 area
func can_build_at(pos: Vector2i) -> bool:
	# Check all 4 cells: (x,y), (x+1,y), (x,y+1), (x+1,y+1)
	var offsets = [Vector2i(0,0), Vector2i(1,0), Vector2i(0,1), Vector2i(1,1)]
	
	for offset in offsets:
		var p = pos + offset
		# Check bounds
		if not astar.region.has_point(p):
			return false
		# Check if already occupied (building on top of another tower/rock)
		if occupied_cells.has(p):
			print("Build blocked at ", p, ". Occupied: ", occupied_cells[p])
			return false
		# Check if an enemy is inside this specific cell
		for enemy in enemies:
			if is_instance_valid(enemy):
				var enemy_grid_pos = Vector2i(
					floor(enemy.position.x / CELL_SIZE), 
					floor(enemy.position.z / CELL_SIZE)
				)
				if enemy_grid_pos == p:
					return false
	return true

# Helper to mark 2x2 area solid/occupied
func set_area_solid(pos: Vector2i, is_solid: bool, visual_node: Node3D = null):
	var offsets = [Vector2i(0,0), Vector2i(1,0), Vector2i(0,1), Vector2i(1,1)]
	for offset in offsets:
		var p = pos + offset
		astar.set_point_solid(p, is_solid)
		if is_solid:
			occupied_cells[p] = visual_node # Mark all 4 cells as occupied by this tower
		else:
			occupied_cells.erase(p)

func check_path_exists() -> bool:
	# Check ALL spawn zones
	for zone in spawn_zones:
		var path = astar.get_id_path(zone.get_start_point(), END_POINT)
		if path.is_empty():
			return false
	return true

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
	
	# Mark 2x2 area as occupied
	set_area_solid(grid_pos, true, tower_node)
	
	# Position the tower (Center of 2x2 area)
	tower_node.position = Vector3(
		(grid_pos.x * CELL_SIZE) + CELL_SIZE, 
		0.0, 
		(grid_pos.y * CELL_SIZE) + CELL_SIZE
	)
	
	# Create the Visuals (Box) as a child of the Tower Node
	var box = BoxMesh.new()
	box.size = Vector3(CELL_SIZE * 2.0, 2.0, CELL_SIZE * 2.0)
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
