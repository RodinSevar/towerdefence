class_name SpawnLocation

var zone: Rect2i
var cell_size: float

func _init(rect: Rect2i, size: float):
	zone = rect
	cell_size = size

# Returns the world position for the Nth unit in this zone
func get_spawn_pos(index: int) -> Vector3:
	var capacity = zone.size.x * zone.size.y
	# Wrap index if we exceed capacity (stacking units)
	var local_index = index % capacity
	
	var grid_x_local = local_index % zone.size.x
	var grid_y_local = (local_index / zone.size.x) % zone.size.y
	
	var spawn_grid_x = zone.position.x + grid_x_local
	var spawn_grid_y = zone.position.y + grid_y_local
	
	# Add slight jitter
	var jitter = randf_range(-0.1, 0.1)
	
	var spawn_x = (spawn_grid_x + 0.5 + jitter) * cell_size
	var spawn_z = (spawn_grid_y + 0.5 + jitter) * cell_size
	
	return Vector3(spawn_x, 0.5, spawn_z)

# Returns a list of all grid cells in this zone (for visuals/blocking)
func get_occupied_cells() -> Array:
	var cells = []
	for x in range(zone.position.x, zone.position.x + zone.size.x):
		for y in range(zone.position.y, zone.position.y + zone.size.y):
			cells.append(Vector2i(x, y))
	return cells

# Returns a representative point for pathfinding validation (Top Center of zone)
func get_start_point() -> Vector2i:
	return Vector2i(zone.position.x + zone.size.x / 2, zone.position.y)
