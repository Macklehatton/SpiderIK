using Godot;
using System;

public partial class ProceduralWalk : CharacterBody3D
{
    [Export] private Node3D footContainer;

    public override void _Ready()
    {
        ClearChildren();

        AddRaycasts(footContainer);
    }

    public void AddRaycasts(Node3D footContainer)
    {
        foreach (Node3D child in footContainer.GetChildren())
        {
            RayCast3D rayCast = new RayCast3D();
            rayCast.Name = "Raycast_" + child.Name;
            AddChild(rayCast);
        }
    }

    public void ClearChildren()
    {
        foreach (Node3D child in GetChildren())
        {
            child.QueueFree();
            RemoveChild(child);
        }
    }
}
