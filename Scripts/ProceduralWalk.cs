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
    //[Export] private float forwardDifferential;
    //[Export] private float radialDifferential;
    [Export] private float footTargetRadialProjection;

    [ExportGroup("Speed")]
    [Export] private float moveSpeed;
    [Export] private float cycleBySpeed;
    [Export] private float footSpeedBySpeed;

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
    [Export] private float forwardDifferentialByRotation;
    [Export] private float radialDifferentialByRotation;

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
        bool turningLeft = direction > 0.0f;

        // How quickly we're rotating, normalized
        currentRotationFactor = Remap(currentRotation, 0.0f, factorMaxRotation, 0.0f, 1.0f);
        float rotationTargetInfluence = Mathf.Lerp(
            targetRotationByRotationLow,
            targetRotationByRotationHigh,
            currentRotationFactor);
        float rotation = rotationTargetInfluence * direction; // + currentRotationFactor * rotationProjection;

        raycastContainer.Rotation = new Vector3(0.0f, rotation, 0.0f);
        float forwardOffset = forwardOffsetBySpeed * moveSpeed;
        raycastContainer.Position = new Vector3(0.0f, 0.0f, -forwardOffset);

        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            RayCast3D rayCast = rayCasts[i];
            Node3D raycastOrigin = (Node3D)rayCast.GetParent();
            Node3D raycastPivot = (Node3D)rayCast.GetParent().GetParent();
            //float radialDifferential = GetRadialDifferential(rotation, radialDifferentialByRotation, direction, currentRotationFactor, i);
            raycastOrigin.Position = raycastOrigin.Basis * new Vector3(0.0f, 0.0f, -footTargetRadialProjection);

            raycastPivot.Rotation = Vector3.Zero;

            // Radial diff
            if (turningLeft)
            {
                if (!LeftLeg(i))
                {
                    raycastPivot.Rotation = new Vector3(0.0f, 0.5f, 0.0f);
                }
            }

            rayCast.GlobalRotation = Vector3.Zero;

            // Reduce forward offset of feet on the side that needs to move less
            //float differentialWeight = GetDifferential(forwardDifferentialByRotation, currentRotationFactor, i);
            //rayCast.GlobalPosition += forward * differentialWeight;

            DebugDraw3D.DrawSphere(rayCasts[i].GlobalPosition);
            DebugDraw3D.DrawLine(GlobalPosition, rayCasts[i].GlobalPosition);
        }
    }

    private float GetRadialDifferential(float differential, int direction, float rotationFactor, int footIndex)
    {
        float radialDifferential = 0.0f;

        if (direction > 0.0f)
        {
            if (LeftLeg(footIndex))
            {
                radialDifferential = differential * rotationFactor;
            }
        }
        else
        {
            if (!LeftLeg(footIndex))
            {
                radialDifferential = differential * rotationFactor;
            }
        }
        return radialDifferential;
    }

    private float GetDifferential(float differential, float rotationFactor, int raycastIndex)
    {
        float differentialApplied = 0.0f;

        if (rotationFactor > 0.0f)
        {
            if (!LeftLeg(raycastIndex))
            {
                differentialApplied = differential * rotationFactor;
            }
        }
        else
        {
            if (LeftLeg(raycastIndex))
            {
                differentialApplied = differential * rotationFactor;
                //differentialApplied *= 1.0f - negative;
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
        float movementFootSpeed = footSpeedBySpeed * moveSpeed;
        float currentFootSpeed = movementFootSpeed + rotationFootSpeed;
        foot.GlobalPosition = foot.GlobalPosition.MoveToward(targetPosition, currentFootSpeed);
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

            Node3D raycastPivot = new Node3D();
            raycastContainer.AddChild(raycastPivot);
            raycastPivot.Name = "RaycastPivot_" + foot.Name;


            Node3D raycastOrigin = new Node3D();
            raycastPivot.AddChild(raycastOrigin);
            raycastOrigin.Name = "RaycastOrigin_" + foot.Name;

            raycastOrigin.GlobalPosition = foot.GlobalPosition;
            raycastOrigin.GlobalPosition += new Vector3(0.0f, raycastHeight, 0.0f);

            Vector3 lookDirection = GlobalPosition.PlanarPosition() - raycastOrigin.GlobalPosition.PlanarPosition();
            lookDirection = lookDirection.Normalized();
            float lookAngle = raycastOrigin.GlobalBasis.Z.SignedAngleTo(lookDirection, Vector3.Up);
            raycastOrigin.Rotate(Vector3.Up, lookAngle);
            raycastOrigin.GlobalPosition = raycastOrigin.Basis * new Vector3(0.0f, 0.0f, -footTargetRadialProjection);

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
