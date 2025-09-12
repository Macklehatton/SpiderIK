using Godot;
using System;

public partial class KeepInBounds : Node3D
{
    [Export] public ProceduralWalk character;
    [Export] public Area3D bounds;

    public override void _Ready()
    {
        bounds.BodyEntered += OnBodyEntered;
    }

    public void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("spider"))
        {
            character.ResetFeetFlag = true;
        }
    }
}
