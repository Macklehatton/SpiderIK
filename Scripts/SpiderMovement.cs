using Godot;
using System;

public partial class SpiderMovement : CharacterBody3D
{
    [Export(PropertyHint.Range, "-0.1,0.1")] private float currentRotation;
    [Export(PropertyHint.Range, "-10,50")] private float currentSpeed;
    [Export] private ProceduralWalk proceduralWalk;

    public float CurrentRotation { get => currentRotation; set => currentRotation = value; }
    public float CurrentSpeed { get => currentSpeed; set => currentSpeed = value; }

    public override void _PhysicsProcess(double delta)
    {
        Rotate(Vector3.Up, currentRotation);
        Velocity = -Transform.Basis.Z * currentSpeed;

        MoveAndSlide();
    }
}