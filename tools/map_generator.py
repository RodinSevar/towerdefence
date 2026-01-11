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
    img = img.convert("RGBA") # Check Alpha too
    width, height = img.size
    
    print(f"Processing Image: {width}x{height}")
    
    # Analyze colors to help user debug
    print("Sampling center pixel (128, 128):", img.getpixel((128, 128)))
    print("Sampling top-left pixel (0, 0):", img.getpixel((0, 0)))
    
    wall_rects = []
    spawn_rects = [] 
    
    # Thresholds
    WALL_THRESHOLD = 50 
    RED_THRESHOLD = 100 
    
    # 1. Scan for Walls (Dark)
    raw_grid = [[False for _ in range(width)] for _ in range(height)]
    
    for y in range(height):
        for x in range(width):
            r, g, b, a = img.getpixel((x, y))
            
            if a < 100:
                # Transparent = Empty (Not Wall)
                is_wall = False
            else:
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

    # Pass 3: Smoothing (Majority Rule 3x3)
    final_grid = [[False for _ in range(width)] for _ in range(height)]
    for y in range(1, height-1):
        for x in range(1, width-1):
            wall_count = 0
            for dy in [-1, 0, 1]:
                for dx in [-1, 0, 1]:
                    if clean_grid[y+dy][x+dx]:
                        wall_count += 1
            
            # If 5 or more pixels in 3x3 area are walls, this becomes a wall.
            if wall_count >= 5:
                final_grid[y][x] = True
            else:
                final_grid[y][x] = False
    
    # 2. Convert Clean Grid to Rects
    for y in range(height):
        current_run_start = -1
        
        for x in range(width):
            is_wall = final_grid[y][x]
            
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

    # 3. Scan for Spawn Zones (Red) using Connected Components (Flood Fill)
    # We want to identify distinct "Blobs" of red pixels.
    visited = [[False for _ in range(width)] for _ in range(height)]
    spawn_rects = []
    
    for y in range(height):
        for x in range(width):
            if visited[y][x]:
                continue
                
            r, g, b, a = img.getpixel((x, y))
            # Same red logic as before
            is_red = (r > RED_THRESHOLD) and (g < 150) and (b < 150) and (r > g + 20) and (r > b + 20)
            
            if is_red:
                # Found a new blob! Flood fill to find extent.
                min_x, max_x = x, x
                min_y, max_y = y, y
                stack = [(x, y)]
                visited[y][x] = True
                
                while stack:
                    cx, cy = stack.pop()
                    
                    min_x = min(min_x, cx)
                    max_x = max(max_x, cx)
                    min_y = min(min_y, cy)
                    max_y = max(max_y, cy)
                    
                    # Check 4 neighbors
                    for dx, dy in [(0, 1), (0, -1), (1, 0), (-1, 0)]:
                        nx, ny = cx + dx, cy + dy
                        
                        if 0 <= nx < width and 0 <= ny < height and not visited[ny][nx]:
                            nr, ng, nb, na = img.getpixel((nx, ny))
                            nis_red = (nr > RED_THRESHOLD) and (ng < 150) and (nb < 150) and (nr > ng + 20) and (nr > nb + 20)
                            
                            if nis_red:
                                visited[ny][nx] = True
                                stack.append((nx, ny))
                
                # Blob finished. Create bounding box.
                w = (max_x - min_x) + 1
                h = (max_y - min_y) + 1
                spawn_rects.append({"x": min_x, "y": min_y, "w": w, "h": h})

    # Save to JSON
    data = {
        "grid_width": width,
        "grid_height": height,
        "obstacles": wall_rects,
        "spawns": spawn_rects
    }
    
    with open(output_path, "w") as f:
        json.dump(data, f, indent=4)
        
    print(f"Saved to {output_path}")
    print(f"Walls: {len(wall_rects)} rects. Spawns: {len(spawn_rects)} rects.")

if __name__ == "__main__":
    generate_map_code()
