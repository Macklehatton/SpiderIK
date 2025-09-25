using Godot;
using System;
using System.Diagnostics;
using VectorExtensions;
using static Godot.Mathf;

public partial class WanderBehavior : Node3D
{
    [Export] private float wanderRadius;
    [Export] private float closeEnoughThreshold;

    [Export] private float wanderMinSpeed;
    [Export] private float wanderMaxSpeed;

    [Export] private float wanderMinRotation;
    [Export] private float wanderMaxRotation;

    private ProceduralWalk characterBody;
    private float currentRotationRate;
    private Vector3 destination;

    public override void _Ready()
    {
        characterBody = (ProceduralWalk)GetParent();
        UpdateDestination();
    }

    public override void _Process(double delta)
    {
        DebugDraw3D.DrawSphere(destination, 2.0f);

        if (CloseEnough())
        {
            UpdateDestination();
        }

        MoveToDestination();
    }

    private void MoveToDestination()
    {
        Vector3 direction = (destination - GlobalPosition.PlanarVector()).Normalized();
        Vector3 forward = -GlobalTransform.Basis.Z;

        float dot = forward.PlanarVector().Dot(direction);
        dot = 1.0f - dot;

        characterBody.currentRotation = dot * currentRotationRate;
    }

    private void UpdateDestination()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();

        // Radius implicitly around zero
        destination = RandomDirection() * rng.RandfRange(0.0f, wanderRadius);

        characterBody.moveSpeed = rng.RandfRange(wanderMinSpeed, wanderMaxSpeed);
        currentRotationRate = rng.RandfRange(wanderMinRotation, wanderMaxRotation);
    }

    private bool CloseEnough()
    {
        if (GlobalPosition.DistanceSquaredTo(destination) <= closeEnoughThreshold)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static Vector3 RandomDirection()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();
        float theta = rng.Randf() * Tau;
        float phi = Acos(2.0f * rng.Randf() - 1.0f);

        Vector3 result = new Vector3(
            Cos(theta) * Sin(phi),
            0.0f,
            Cos(phi));
        return result;
    }
}
