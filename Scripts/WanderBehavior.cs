using Godot;
using System;
using System.Diagnostics;
using VectorExtensions;
using static Godot.Mathf;

public partial class WanderBehavior : Node3D
{
    [Export] private bool strafe;
    [Export] private float wanderRadius;
    [Export] private float closeEnoughThreshold;
    [Export] private float rotationEpsilon;

    [Export] private float wanderMinSpeed;
    [Export] private float wanderMaxSpeed;

    [Export] private float wanderMinRotation;
    [Export] private float wanderMaxRotation;

    [Export] private float slowDistance;

    [Export] private Curve slowCurve;

    [Export] private bool debugDestination;

    private SpiderMovement characterBody;
    private float currentRotationRate;
    private float currentSpeed;
    private Vector3 destination;
    private float currentDistance;

    public override void _Ready()
    {
        characterBody = (SpiderMovement)GetParent();
        UpdateDestination();
    }

    public override void _Process(double delta)
    {
        currentDistance = GlobalPosition.DistanceTo(destination);

        if (debugDestination)
        {
            DebugDraw3D.DrawSphere(destination, 1.0f, Colors.Black);
        }

        if (CloseEnough())
        {
            UpdateDestination();
        }

        MoveToDestination();
    }

    private void MoveToDestination()
    {
        UpdateRotation();
        UpdateSpeed();
    }

    private void UpdateRotation()
    {
        Vector3 direction = (destination - GlobalPosition.PlanarVector()).Normalized();
        Vector3 forward = -GlobalTransform.Basis.Z;

        float turnAngle = direction.SignedAngleTo(forward, Vector3.Up);
        float turnDirection = Sign(turnAngle);

        if (Abs(turnAngle) <= rotationEpsilon)
        {
            characterBody.CurrentRotation = -turnAngle * currentRotationRate;
            return;
        }

        characterBody.CurrentRotation = -turnDirection * currentRotationRate;
    }

    private void UpdateSpeed()
    {
        if (strafe)
        {
            characterBody.CurrentSpeed = currentSpeed;
            Vector3 direction = (destination - GlobalPosition.PlanarVector()).Normalized();
            characterBody.Velocity = direction * currentSpeed;
            return;
        }

        float slowFactor = currentDistance / slowDistance;
        slowFactor = Min(1.0f, slowFactor);
        slowFactor = 1.0f - slowFactor;

        characterBody.CurrentSpeed = currentSpeed * slowCurve.Sample(slowFactor);
        characterBody.Velocity = -characterBody.Basis.Z * currentSpeed;
    }

    private void UpdateDestination()
    {
        RandomNumberGenerator rng = new RandomNumberGenerator();

        // Radius implicitly around zero
        destination = RandomDirection() * rng.RandfRange(0.0f, wanderRadius);

        currentSpeed = rng.RandfRange(wanderMinSpeed, wanderMaxSpeed);
        currentRotationRate = rng.RandfRange(wanderMinRotation, wanderMaxRotation);
    }

    private bool CloseEnough()
    {
        if (currentDistance <= closeEnoughThreshold)
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
