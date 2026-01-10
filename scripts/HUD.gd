extends CanvasLayer

var gold_label: Label
var lives_label: Label
var wave_label: Label
var restart_button: Button

func _ready():
	setup_ui()

func setup_ui():
	# Create a Control node for positioning
	var control = Control.new()
	control.set_anchors_preset(Control.PRESET_FULL_RECT)
	# Allow mouse to pass through to the game (Important!)
	control.mouse_filter = Control.MOUSE_FILTER_IGNORE 
	add_child(control)
	
	# --- GOLD LABEL ---
	gold_label = Label.new()
	gold_label.position = Vector2(20, 20) # Top Left
	
	var gold_settings = LabelSettings.new()
	gold_settings.font_size = 32
	gold_settings.font_color = Color(1, 0.84, 0) # Gold Color
	gold_settings.outline_size = 4
	gold_settings.outline_color = Color(0, 0, 0)
	gold_label.label_settings = gold_settings
	control.add_child(gold_label)
	
	# --- LIVES LABEL ---
	lives_label = Label.new()
	lives_label.position = Vector2(20, 70) # Below Gold
	
	var lives_settings = LabelSettings.new()
	lives_settings.font_size = 32
	lives_settings.font_color = Color(1, 0.2, 0.2) # Red Color
	lives_settings.outline_size = 4
	lives_settings.outline_color = Color(0, 0, 0)
	lives_label.label_settings = lives_settings
	control.add_child(lives_label)
	
	# --- WAVE LABEL (Preparing for next step) ---
	wave_label = Label.new()
	wave_label.position = Vector2(20, 120) 
	
	var wave_settings = LabelSettings.new()
	wave_settings.font_size = 24
	wave_settings.font_color = Color(1, 1, 1) 
	wave_label.label_settings = wave_settings
	control.add_child(wave_label)

	# --- RESTART BUTTON ---
	restart_button = Button.new()
	restart_button.text = "RESTART"
	restart_button.set_anchors_preset(Control.PRESET_CENTER)
	restart_button.position = Vector2(0, 50) # Offset slightly down from center
	restart_button.size = Vector2(200, 60)
	restart_button.visible = false # Hidden initially
	
	# Enable mouse for button even if control ignores it
	restart_button.mouse_filter = Control.MOUSE_FILTER_STOP
	
	# CRITICAL FIX: Allow this button to work when the game is paused!
	restart_button.process_mode = Node.PROCESS_MODE_ALWAYS
	
	control.add_child(restart_button)

func update_gold(amount: int):
	gold_label.text = "Gold: " + str(amount)

func update_lives(amount: int):
	if amount <= 0:
		lives_label.text = "GAME OVER"
		show_restart_button()
	else:
		lives_label.text = "Lives: " + str(amount)

func show_restart_button():
	restart_button.visible = true

func update_wave(wave_num: int):
	wave_label.text = "Wave: " + str(wave_num)
