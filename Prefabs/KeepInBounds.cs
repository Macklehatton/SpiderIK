using Godot;
using System;

public partial class KeepInBounds : Node3D
{
    [Export] public CollisionObject3D bounds;

    public override void _Process(double delta)
    {
    }

}
