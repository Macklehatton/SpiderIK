using Godot;
using System;

public partial class KeepInBounds : Node3D
{
    private CharacterBody3D character;
    [Export] private Area3D bounds;

    public override void _Ready()
    {
        bounds.BodyEntered += OnBodyEntered;

        character = (CharacterBody3D)GetParent();
    }

    public void OnBodyEntered(Node3D body)
    {
        if (body.IsInGroup("spider"))
        {
            character.GlobalPosition = Vector3.Zero;
            //character.ResetFeetFlag = true;
        }
    }
}
