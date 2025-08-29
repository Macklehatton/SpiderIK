@tool

class_name FootConstraint extends Node3D

@export var ik_effector: GodotIKEffector
@export var initialized: bool
@export var offset: Vector3

var initial_position
var leaf_rest_position

func initialize(p_ik_effector, skeleton, leaf_index):
	ik_effector = p_ik_effector
	leaf_rest_position = skeleton.get_bone_global_rest(leaf_index).origin
	leaf_rest_position = skeleton.to_global(leaf_rest_position)
	initial_position = ik_effector.global_position
	global_position = leaf_rest_position
	offset = initial_position - leaf_rest_position
	initialized = true

func _process(_delta: float) -> void:
	if !initialized:
		return
	ik_effector.global_position = global_position + offset
