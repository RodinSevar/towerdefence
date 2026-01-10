from PIL import Image
import os
import json

# Path to your map image
image_path = r"assets/stock_map.png"
output_path = r"assets/map_data.json"

def generate_map_code():
    if not os.path.exists(image_path):
        print(f"Error: Could not find image at {image_path}")
        return

    img = Image.open(image_path)
    img = img.resize((256, 256), Image.Resampling.NEAREST) # Force 256x256
    img = img.convert("RGB") # Color analysis
    width, height = img.size
    
    print(f"Processing Image: {width}x{height}")
    
    wall_rects = []
    spawn_rects = [] # List of Rect2i equivalent
    
    # Thresholds
    WALL_THRESHOLD = 50 
    RED_THRESHOLD = 100 
    
    # 1. Scan for Walls (Dark)
    # First pass: Read raw grid
    raw_grid = [[False for _ in range(width)] for _ in range(height)]
    
    for y in range(height):
        for x in range(width):
            r, g, b = img.getpixel((x, y))
            brightness = (r + g + b) / 3
            is_red = (r > RED_THRESHOLD) and (g < 150) and (b < 150) and (r > g + 20) and (r > b + 20)
            is_wall = (brightness < WALL_THRESHOLD) and not is_red
            raw_grid[y][x] = is_wall

    # Pass 1: Fill Holes (Dilation-ish)
    # If a pixel is PATH but surrounded by >= 3 WALL neighbors, make it WALL.
    filled_grid = [row[:] for row in raw_grid] # Deep copy
    for y in range(1, height-1):
        for x in range(1, width-1):
            if not raw_grid[y][x]: # If empty
                neighbors = 0
                if raw_grid[y-1][x]: neighbors += 1
                if raw_grid[y+1][x]: neighbors += 1
                if raw_grid[y][x-1]: neighbors += 1
                if raw_grid[y][x+1]: neighbors += 1
                
                if neighbors >= 3:
                    filled_grid[y][x] = True

    # Pass 2: Remove noise (Speckles) from the filled grid
    clean_grid = [[False for _ in range(width)] for _ in range(height)]
    
    for y in range(1, height-1):
        for x in range(1, width-1):
            if filled_grid[y][x]:
                neighbors = 0
                if filled_grid[y-1][x]: neighbors += 1
                if filled_grid[y+1][x]: neighbors += 1
                if filled_grid[y][x-1]: neighbors += 1
                if filled_grid[y][x+1]: neighbors += 1
                
                # Keep if it has neighbors (structure), otherwise kill it (noise)
                if neighbors >= 1:
                    clean_grid[y][x] = True
    
    # 2. Convert Clean Grid to Rects
    for y in range(height):
        current_run_start = -1
        
        for x in range(width):
            is_wall = clean_grid[y][x]
            
            if is_wall:
                if current_run_start == -1:
                    current_run_start = x
            else:
                if current_run_start != -1:
                    w = x - current_run_start
                    wall_rects.append({"x": current_run_start, "y": y, "w": w, "h": 1})
                    current_run_start = -1
        
        if current_run_start != -1:
            w = width - current_run_start
            wall_rects.append({"x": current_run_start, "y": y, "w": w, "h": 1})

    # 3. Scan for Spawn Zones (Red)
    # We want to group contiguous red pixels into logical zones.
    # Simple approach: Find red pixels, build a bounding box for connected components?
    # Simpler approach: Just find all red pixels, min_x, max_x, min_y, max_y.
    # Assuming standard "Wintermaul" layout has distinct red boxes.
    
    # Let's just find the bounding box of ALL red pixels for now, 
    # OR if there are multiple separated ones, we might need a flood fill.
    # For now, let's just find any red pixel and assume it's part of a zone.
    
    # Actually, the user wants multiple spawn zones. Let's try to detect separated zones.
    # A simple clustering or just creating a rect for every horizontal line of red (like walls) 
    # works, but is inefficient for game logic.
    # Let's stick to Run-Length for Red too, and `SpawnLocation` can handle many small rects.
    
    for y in range(height):
        current_run_start = -1
        for x in range(width):
            r, g, b = img.getpixel((x, y))
            is_red = (r > RED_THRESHOLD) and (g < 200) and (b < 200) and (r > g + 20) and (r > b + 20)
            
            if is_red:
                if current_run_start == -1:
                    current_run_start = x
            else:
                if current_run_start != -1:
                    w = x - current_run_start
                    spawn_rects.append({"x": current_run_start, "y": y, "w": w, "h": 1})
                    current_run_start = -1
        if current_run_start != -1:
            w = width - current_run_start
            spawn_rects.append({"x": current_run_start, "y": y, "w": w, "h": 1})

    # Save to JSON
    data = {
        "grid_width": width,
        "grid_height": height,
        "obstacles": wall_rects,
        "spawns": spawn_rects
    }
    
    with open(output_path, "w") as f:
        json.dump(data, f)
        
    print(f"Saved to {output_path}")
    print(f"Walls: {len(wall_rects)} rects. Spawns: {len(spawn_rects)} rects.")

if __name__ == "__main__":
    generate_map_code()
