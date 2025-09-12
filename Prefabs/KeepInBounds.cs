using Godot;
using System;

public partial class KeepInBounds : Node3D
{
    [Export] public CharacterBody3D character;
    [Export] public Area3D bounds;

    public override void _Ready()
    {
        bounds.BodyEntered += OnBodyEntered;
    }

    public void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("spider"))
        {
            character.GlobalPosition = Vector3.Zero;
        }
    }
}
