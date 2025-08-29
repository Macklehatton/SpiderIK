using Godot;
using System;
using System.Collections.Generic;

public partial class ProceduralWalk : CharacterBody3D
{
    [Export] private float raycastDistance;
    [Export] private float raycastHeight;
    [Export] private Node3D footContainer;

    [Export] private float walkSpeed;

    private Dictionary<Node3D, RayCast3D> RayCasts;

    public override void _Ready()
    {
        RayCasts = new Dictionary<Node3D, RayCast3D>();
        AddRayCasts(footContainer);

        footContainer.CallDeferred("reparent", GetTree().Root);
        AlignAll();
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = new Vector3(0.0f, 0.0f, walkSpeed);
        MoveAndSlide();
    }

    // Temporary test
    public void AlignAll()
    {
        foreach (Node3D foot in RayCasts.Keys)
        {
            AlignToRaycast(foot);
        }
    }

    public void AlignToRaycast(Node3D foot)
    {
        RayCast3D raycast = RayCasts[foot];

        if (!raycast.IsColliding())
        {
            return;
        }

        Vector3 targetPosition = raycast.GetCollisionPoint();
        foot.GlobalPosition = targetPosition;
    }

    public void AddRayCasts(Node3D footContainer)
    {
        foreach (Node3D child in footContainer.GetChildren())
        {
            RayCast3D rayCast = new RayCast3D();
            rayCast.Name = "Raycast_" + child.Name;
            AddChild(rayCast);
            rayCast.GlobalPosition = child.GlobalPosition;
            rayCast.GlobalPosition += new Vector3(0.0f, raycastHeight, 0.0f);
            rayCast.TargetPosition = new Vector3(0.0f, -raycastDistance, 0.0f);

            RayCasts.Add(child, rayCast);
        }
    }
}
