@tool

class_name FootConstraint extends Node3D

@export var ik_effector : GodotIKEffector
var initial_position
var leaf_rest_position
var offset
var initialized = false

func initialize(ik_effector, skeleton, godot_ik, leaf_index):
	self.ik_effector = ik_effector
	self.leaf_rest_position = skeleton.get_bone_global_rest(leaf_index).origin
	self.initial_position = ik_effector.global_position
	global_position = leaf_rest_position
	self.offset = initial_position - leaf_rest_position
	initialized = true

func _process(delta: float) -> void:
	if !initialized:
		return
	ik_effector.global_position = global_position + offset
