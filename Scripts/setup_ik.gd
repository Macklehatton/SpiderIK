@tool

extends Node3D

@export var run : bool
@export var skeleton : Skeleton3D
@export var chain_length : int
@export var godot_ik : GodotIK
@export var foot_container : Node3D

func _process(_delta: float) -> void:
	if !run:
		return
		
	if foot_container == null:
		foot_container = Node3D.new()
		add_child(foot_container)
		foot_container.owner = get_tree().edited_scene_root
		foot_container.name = "FootContainer"
	
	for node in foot_container.get_children():
		node.queue_free()
		remove_child(node)
	
	if godot_ik == null:
		godot_ik = GodotIK.new()
		skeleton.add_child(godot_ik)
		godot_ik.owner = get_tree().edited_scene_root
		
	for node in godot_ik.get_children():
		node.queue_free()
		remove_child(node)
	
	for bone_id in skeleton.get_bone_count():
		var bone_name = skeleton.get_bone_name(bone_id)
		if bone_name.find("leaf") != -1:
			var parent_id = skeleton.get_bone_parent(bone_id)
			var parent_name = skeleton.get_bone_name(parent_id)
			
			var effector = add_effector(parent_id, parent_name)
			
			godot_ik.add_child(effector)
			effector.owner = get_tree().edited_scene_root
			var pole = add_pole(parent_id, parent_name)
			effector.add_child(pole)
			pole.owner = get_tree().edited_scene_root
			
			add_foot_constraint(effector, bone_id, bone_name)
			
	run = !run

func add_effector(id, effector_name) -> GodotIKEffector:
	var effector = GodotIKEffector.new()
	effector.name = "ik_" + effector_name
	effector.bone_name = effector_name
	effector.bone_idx = id
	effector.transform = skeleton.get_bone_global_rest(id)
	effector.chain_length = chain_length
	effector.transform_mode = GodotIKEffector.FULL_TRANSFORM
	return effector
	
func add_pole(id, pole_name) -> PoleBoneConstraint:
	var pole = PoleBoneConstraint.new()
	pole.name = "pole_" + pole_name
	var grandparent_ID = skeleton.get_bone_parent(id)
	var grandparent_name = skeleton.get_bone_name(id)
	pole.bone_idx = grandparent_ID
	pole.bone_name = grandparent_name
	pole.pole_direction.y = 1
	return pole
	
func add_foot_constraint(effector, leaf_id, foot_name) -> FootConstraint:
	var foot_constraint = FootConstraint.new()
	foot_constraint.name = "foot_target_" + foot_name
	foot_container.add_child(foot_constraint)
	foot_constraint.owner = get_tree().edited_scene_root
	foot_constraint.initialize(effector, skeleton, leaf_id)
	return foot_constraint
