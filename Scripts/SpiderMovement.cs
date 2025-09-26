using Godot;
using System;
using System.Diagnostics;
using VectorExtensions;
using static Godot.Mathf;

public partial class SpiderMovement : CharacterBody3D
{
    public float CurrentRotation { get; set; }

    public override void _PhysicsProcess(double delta)
    {
        Rotate(Vector3.Up, CurrentRotation);

        MoveAndSlide();
        //MoveFeet();
    }
}