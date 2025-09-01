@tool
class_name PerAxisRotationConstraint extends GodotIKConstraint
# Per-axis max rotation constraint for a bone using bone-local axes.
# Euler order for reconstruction is Z -> X -> Y.

@export var forward : bool = true
@export var backward : bool = true
@export var active : bool = true

@export var max_rotation_x_deg : float = 30.0
@export var max_rotation_y_deg : float = 30.0
@export var max_rotation_z_deg : float = 30.0

var _initial_rotation : Quaternion

func apply(
	pos_parent_bone: Vector3,
	pos_bone: Vector3,
	pos_child_bone: Vector3,
	dir: int
	) -> PackedVector3Array:

	var result = [pos_parent_bone, pos_bone, pos_child_bone]
	if not active:
		return result

	var dir_pb = pos_parent_bone.direction_to(pos_bone)
	var dir_bc = pos_bone.direction_to(pos_child_bone)

	if get_ik_controller().get_current_iteration() == 0:
		_initial_rotation = calculate_initial_rotation()

	var current_rotation = Quaternion(dir_pb, dir_bc)
	var rotation_to_initial = _initial_rotation * current_rotation.inverse()

	var bone_transform = get_skeleton().get_bone_pose(bone_idx)
	var bone_basis = bone_transform.basis

	var rotation_to_initial_basis_global = Basis(rotation_to_initial)
	var rotation_to_initial_local_basis = bone_basis.inverse() * rotation_to_initial_basis_global * bone_basis

	var euler_local = rotation_to_initial_local_basis.get_euler()

	var max_x = deg_to_rad(max_rotation_x_deg)
	var max_y = deg_to_rad(max_rotation_y_deg)
	var max_z = deg_to_rad(max_rotation_z_deg)

	var clamped_local = Vector3(
		clamp(euler_local.x, -max_x, max_x),
		clamp(euler_local.y, -max_y, max_y),
		clamp(euler_local.z, -max_z, max_z)
	)

	if clamped_local == euler_local:
		return result

	var rot_local = Basis.IDENTITY
	rot_local = rot_local.rotated(Vector3(0,0,1), clamped_local.z) # Z
	rot_local = rot_local.rotated(Vector3(1,0,0), clamped_local.x) # X
	rot_local = rot_local.rotated(Vector3(0,1,0), clamped_local.y) # Y

	var rotation_to_initial_clamped_basis_global : Basis = bone_basis * rot_local * bone_basis.inverse()
	var rotation_to_initial_clamped : Quaternion = Quaternion(rotation_to_initial_clamped_basis_global)

	var new_current_rotation : Quaternion = rotation_to_initial_clamped.inverse() * _initial_rotation

	if dir == FORWARD and forward:
		var new_dir_bc = new_current_rotation * dir_pb
		result[2] = pos_bone + new_dir_bc * (pos_child_bone - pos_bone).length()

	if dir == BACKWARD and backward:
		var new_dir_pb = new_current_rotation.inverse() * dir_bc
		result[0] = pos_bone - new_dir_pb * (pos_parent_bone - pos_bone).length()

	return result


func calculate_initial_rotation() -> Quaternion:
	var bone_parent = get_skeleton().get_bone_parent(bone_idx)
	var bone_children = get_skeleton().get_bone_children(bone_idx)
	assert(bone_children.size() == 1)

	var pos_p = get_skeleton().get_bone_global_pose(bone_parent).origin
	var pos_b = get_skeleton().get_bone_global_pose(bone_idx).origin
	var pos_c = get_skeleton().get_bone_global_pose(bone_children[0]).origin

	var dir_pb = pos_p.direction_to(pos_b)
	var dir_bc = pos_b.direction_to(pos_c)

	return Quaternion(dir_pb, dir_bc)
