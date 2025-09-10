using Godot;
using System;
using VectorExtensions;

public partial class ProceduralWalk : CharacterBody3D
{
    [ExportGroup("References")]
    [Export] private Node3D footContainer;
    [Export] private Skeleton3D skeleton;

    [ExportGroup("Raycasts")]
    [Export] private float raycastDistance;
    [Export] private float raycastHeight;
    [Export] private float maxForwardOffset;
    [Export] private float forwardOffsetBySpeed;

    [ExportGroup("")]
    [Export] private float strideDistance;
    //[Export] private float footSpeed;
    [Export] private float forwardDifferential;
    [Export] private float radialDifferential;
    [Export] private float footTargetRadialDistance;

    [ExportGroup("Speed")]
    [Export] private float moveSpeed;
    [Export] private float cycleBySpeed;

    [ExportGroup("Rotation")]
    [Export(PropertyHint.Range, "0,0.1")] private float turnSpeed;
    [Export(PropertyHint.Range, "0,0.1")] private float factorMaxRotation;
    [Export] private float cycleByRotationLow;
    [Export] private float cycleByRotationHigh;
    [Export] private float footSpeedByRotationLow;
    [Export] private float footSpeedByRotationHigh;
    //[Export] private float radialProjectionByRotation;
    [Export] private float targetRotationByRotationLow;
    [Export] private float targetRotationByRotationHigh;


    private Node3D[] feet;
    private RayCast3D[] rayCasts;
    private Node3D raycastContainer;
    private int[] legRoots;
    // Feet that we currently prefer to move 
    // for a balanced walk
    private bool[] inCycle;
    // Feet currently moving
    private bool[] feetMoving;
    private Vector3[] currentTargets;
    private Vector3[] footOrigins;

    private bool offFoot;
    private float strideDistanceSquared;

    private float currentCycle;
    private float currentRotation;
    private float currentRotationFactor;
    //private float currentRadialMagnitude;

    public override void _Ready()
    {
        // Giving my GPU a break
        Engine.MaxFps = 120;

        feet = GetFeet();
        rayCasts = AddRayCasts(feet);
        feetMoving = new bool[feet.Length];

        inCycle = new bool[feet.Length];
        SetAlternateFeet();

        currentTargets = new Vector3[feet.Length];
        footOrigins = new Vector3[feet.Length];
        SetInitialTargets();

        legRoots = GetLegRoots();

        // We don't want the foot IK targets moving with the character
        footContainer.CallDeferred("reparent", GetTree().Root);
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateCycle();

        currentRotation = turnSpeed;
        Rotate(Vector3.Up, currentRotation);
        Velocity = -Transform.Basis.Z * moveSpeed;

        UpdateRaycastProjections();

        MoveAndSlide();
        MoveFeet();
        DrawDebugs();
    }

    private void DrawDebugs()
    {
        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            DebugDraw3D.DrawSphere(currentTargets[i], 0.25f, Colors.PaleVioletRed);
        }
    }

    private void UpdateCycle()
    {
        float rotationCycleInfluence = Mathf.Lerp(cycleByRotationLow, cycleByRotationHigh, currentRotationFactor);
        float moveCycleInfluence = moveSpeed * cycleBySpeed;
        float cycleDelta = moveCycleInfluence + rotationCycleInfluence;
        currentCycle += cycleDelta;

        // Wrap
        if (currentCycle > 1.0f)
        {
            currentCycle = currentCycle - 1.0f;
            SwapInCycle();
        }
    }

    private void UpdateRaycastProjections()
    {
        Vector3 forward = -Transform.Basis.Z;
        int direction = Mathf.Sign(currentRotation);
        // How quickly we're rotating, normalized
        currentRotationFactor = Remap(currentRotation, 0.0f, factorMaxRotation, 0.0f, 1.0f);
        float rotationTargetInfluence = Mathf.Lerp(
            targetRotationByRotationLow,
            targetRotationByRotationHigh,
            currentRotationFactor);
        float rotation = rotationTargetInfluence * direction; // + currentRotationFactor * rotationProjection;

        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            // Adjust by rotation
            float adjustedForwardOffset = Mathf.Lerp(maxForwardOffset, 0.0f, currentRotationFactor);
            Vector3 forwardOffset = forward * adjustedForwardOffset;

            raycastContainer.Rotation = new Vector3(0.0f, rotation, 0.0f);

            // Reduce forward offset of feet on the side that needs to move less
            //forwardOffset = ApplyDifferential(forwardOffset, forwardDifferential, currentRotationFactor, i);

            DebugDraw3D.DrawSphere(rayCasts[i].GlobalPosition);
            DebugDraw3D.DrawLine(GlobalPosition, rayCasts[i].GlobalPosition);
        }
    }

    private float ApplyRadialDifferential(float rotation, float negative, float rotationFactor, int raycastIndex)
    {
        float differentialApplied = rotation;

        if (rotationFactor > 0.0f)
        {
            if (LeftLeg(raycastIndex))
            {
                differentialApplied *= 1.0f - (negative * rotationFactor);
            }
        }
        else
        {
            if (!LeftLeg(raycastIndex))
            {
                differentialApplied *= 1.0f - (negative * rotationFactor);
            }
        }
        return differentialApplied;
    }

    private Vector3 ApplyDifferential(Vector3 vector, float negative, float rotationFactor, int raycastIndex)
    {
        Vector3 differentialApplied = vector;

        if (rotationFactor > 0.0f)
        {
            if (LeftLeg(raycastIndex))
            {
                differentialApplied *= 1.0f - negative;
            }
        }
        else
        {
            if (!LeftLeg(raycastIndex))
            {
                differentialApplied *= 1.0f - negative;
            }
        }
        return differentialApplied;
    }

    private static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
    {
        value = Mathf.Clamp(value, inMin, inMax);
        return outMin + (value - inMin) * (outMax - outMin) / (inMax - inMin);
    }

    private void MoveFeet()
    {
        for (int i = 0; i <= feet.Length - 1; i++)
        {
            feetMoving[i] = CheckMoveFoot(i);

            if (feetMoving[i])
            {
                MoveFoot(i);
            }
        }
    }

    private bool CheckMoveFoot(int footIndex)
    {
        if (!inCycle[footIndex])
        {
            return false;
        }

        // if (currentTarget[footIndex].HasValue)
        // {
        //     return true;
        // }

        return true;

        // if (CheckDistance(footIndex))
        // {
        //     // Cache the target position
        //     currentTarget[footIndex] = rayCasts[footIndex].GetCollisionPoint();
        //     return true;
        // }
        // else
        // {
        //     currentTarget[footIndex] = null;
        //     feetMoving[footIndex] = false;
        // }

        // currentTarget[footIndex] = null;
        // return false;
    }

    private bool CheckDistance(int footIndex)
    {
        Vector3 footPosition = feet[footIndex].GlobalPosition;
        Vector3 targetPosition = rayCasts[footIndex].GetCollisionPoint();

        if (footPosition.DistanceSquaredTo(targetPosition) > strideDistanceSquared)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void MoveFoot(int footIndex)
    {
        RayCast3D raycast = rayCasts[footIndex];

        if (!raycast.IsColliding())
        {
            return;
        }

        Vector3 footOrigin = footOrigins[footIndex];
        Vector3 targetPosition = currentTargets[footIndex];

        Node3D foot = feet[footIndex];

        // MoveToward isn't guaranteed to reach the target
        // It's a little easier to insert pauses with it
        float rotationFootSpeed = Mathf.Lerp(footSpeedByRotationLow, footSpeedByRotationHigh, currentRotation);
        // movespeed
        foot.GlobalPosition = foot.GlobalPosition.MoveToward(targetPosition, rotationFootSpeed);
        //foot.GlobalPosition = footOrigin.Slerp(targetPosition, currentCycle);
    }

    private int[] GetLegRoots()
    {
        int[] rootChildren = skeleton.GetBoneChildren(0);
        return rootChildren;
    }

    private Node3D[] GetFeet()
    {
        // Personal preference not to just use the Godot.Array
        // that comes from GetChildren()
        var children = footContainer.GetChildren();
        Node3D[] feet = new Node3D[children.Count];

        for (int i = 0; i <= feet.Length - 1; i++)
        {
            feet[i] = (Node3D)children[i];
        }

        return feet;
    }


    private RayCast3D[] AddRayCasts(Node3D[] feet)
    {
        RayCast3D[] rayCasts = new RayCast3D[feet.Length];
        raycastContainer = new Node3D();
        AddChild(raycastContainer);

        for (int i = 0; i <= feet.Length - 1; i++)
        {
            Node3D foot = feet[i];

            Node3D raycastOrigin = new Node3D();
            raycastContainer.AddChild(raycastOrigin);
            raycastOrigin.Name = "RaycastOrigin_" + foot.Name;

            raycastOrigin.GlobalPosition = foot.GlobalPosition;
            raycastOrigin.GlobalPosition += new Vector3(0.0f, raycastHeight, 0.0f);

            RayCast3D rayCast = new RayCast3D();
            raycastOrigin.AddChild(rayCast);
            rayCast.Name = "Raycast_" + foot.Name;

            rayCast.TargetPosition = new Vector3(0.0f, -raycastDistance, 0.0f);
            rayCasts[i] = rayCast;
        }
        return rayCasts;
    }

    private void SwapInCycle()
    {
        for (int i = 0; i <= inCycle.Length - 1; i++)
        {
            inCycle[i] = !inCycle[i];

            if (inCycle[i])
            {
                currentTargets[i] = rayCasts[i].GetCollisionPoint();
                footOrigins[i] = feet[i].GlobalPosition;
            }
        }
    }

    private void SetAlternateFeet()
    {
        int row = feet.Length / 2;

        for (int i = 0; i <= row - 1; i++)
        {
            if (i % 2 == 0)
            {
                inCycle[i] = true;
            }
            else
            {
                int oppositeIndex = Opposite(i);
                inCycle[oppositeIndex] = true;
            }
        }
    }

    // Foot pairs
    // 0 1 2 3
    // 4 5 6 7
    private int Opposite(int i)
    {
        int row = feet.Length / 2;
        int opposite = i + row;

        if (opposite > feet.Length - 1)
        {
            opposite = opposite - feet.Length;
        }
        return opposite;
    }

    private void SetInitialTargets()
    {
        for (int i = 0; i <= currentTargets.Length - 1; i++)
        {
            currentTargets[i] = feet[i].GlobalPosition;
            footOrigins[i] = feet[i].GlobalPosition;
        }
    }

    private bool LeftLeg(int footIndex)
    {
        int row = feet.Length / 2;
        if (footIndex < row)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
