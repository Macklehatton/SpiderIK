using Godot;
using System;
using System.Diagnostics;
using VectorExtensions;

public partial class ProceduralWalk : CharacterBody3D
{
    [ExportGroup("References")]
    [Export] private Node3D footContainer;
    [Export] private Skeleton3D skeleton;
    [Export] private Node3D projection;

    [ExportGroup("Raycasts")]
    [Export] private float raycastDistance;
    [Export] private float raycastHeight;

    [ExportGroup("Debug")]
    [Export] private Vector3 debugOffsetRaycastContainerLocal;
    [Export] private Vector3 debugOffsetRaycastContainer;
    [Export] private float debugRotateRaycastContainer;
    [Export] private int projectionIterations;

    [ExportGroup("")]
    [Export] private float footTargetRadialProjection;
    [Export] private float cycleAddFactor;
    [Export] private Curve cycleBySpeedRotation;

    [ExportGroup("Projection Translation")]
    [Export(PropertyHint.Range, "-10,50")] private float moveSpeed;
    [Export(PropertyHint.Range, "0,50")] private float maxSpeed;
    [Export] private Curve cycleBySpeed;
    [Export] private Curve projectionOffsetBySpeed;
    [Export] private Curve relativeOffsetBySpeed;

    [Export] private Curve relativeOffsetZBySpeedRotation;
    [Export] private Curve relativeOffsetXBySpeedRotation;

    [Export] private Curve projectionOffsetReductionByRotation;
    [Export] private Curve projectionOffsetReductionBySpeedRotation;


    [ExportGroup("Projection Rotation")]
    [Export(PropertyHint.Range, "-0.1,0.1")] private float turnSpeed;
    [Export(PropertyHint.Range, "0,0.1")] private float factorMaxRotation;
    [Export] private Curve cycleByRotation;
    [Export] private Curve rotationCycleInfluenceReductionBySpeed;

    [Export] private Curve targetRotationByRotation;
    [Export] private Curve rotationReductionBySpeed;
    [Export] private float radialDifferentialByRotation;
    [Export] private Curve projectionDifferentialByRotation;

    [ExportGroup("Height")]
    [Export] private bool enableStepHeight = true;
    [Export] private float maxHeight;
    [Export] private float maxStride;

    [ExportGroup("Foot Speed")]
    [Export] private float footSpeedBySpeed;
    [Export] private float footSpeedByRotation;



    private Node3D[] feet;
    private RayCast3D[] rayCasts;
    private Node3D raycastContainer;
    private int[] legRoots;
    private int[] footBones;
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
    private float currentMoveFactor;

    public bool ResetFeetFlag { get; set; }

    public override void _Ready()
    {
        // Giving my GPU a break
        Engine.MaxFps = 60;

        feet = GetFeet();
        rayCasts = AddRayCasts(feet);
        feetMoving = new bool[feet.Length];

        inCycle = new bool[feet.Length];
        SetAlternateFeet();

        currentTargets = new Vector3[feet.Length];
        footOrigins = new Vector3[feet.Length];
        SetInitialTargets();

        legRoots = GetLegRoots();
        footBones = GetFootBones();

        // We don't want the foot IK targets moving with the character
        footContainer.CallDeferred("reparent", GetTree().Root);

        projection.CallDeferred("reparent", GetTree().Root);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (ResetFeetFlag)
        {
            ResetFeet();
            ResetFeetFlag = false;
            return;
        }

        UpdateCycle();

        currentRotation = turnSpeed;
        Rotate(Vector3.Up, currentRotation);
        Velocity = -Transform.Basis.Z * moveSpeed;

        MoveAndSlide();
        MoveFeet();

        UpdateProjection();
        UpdateRaycastProjections();
        DrawDebugs();
    }

    private void DrawDebugs()
    {
        DebugDraw3D.DrawSphere(projection.GlobalPosition, 0.75f, Colors.AliceBlue);

        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            RayCast3D rayCast = rayCasts[i];
            Node3D raycastOrigin = (Node3D)rayCast.GetParent();
            Node3D raycastPivot = (Node3D)rayCast.GetParent().GetParent();

            DebugDraw3D.DrawSphere(currentTargets[i], 0.25f, Colors.PaleVioletRed);
            DebugDraw3D.DrawSphere(rayCast.GlobalPosition);
            DebugDraw3D.DrawLine(raycastPivot.GlobalPosition, rayCast.GlobalPosition);
        }
    }

    private void UpdateCycle()
    {
        float cycleDelta = 0.0f;
        cycleDelta += cycleBySpeedRotation.Sample(Mathf.Sqrt(currentMoveFactor * currentRotationFactor));

        float rotationCycleInfluence = cycleByRotation.Sample(currentRotationFactor);
        rotationCycleInfluence *= rotationCycleInfluenceReductionBySpeed.Sample(currentMoveFactor);
        float moveCycleInfluence = cycleBySpeed.Sample(currentMoveFactor);

        // Take highest
        cycleDelta += Mathf.Max(moveCycleInfluence, rotationCycleInfluence);
        // Add other by factor
        cycleDelta += Mathf.Min(moveCycleInfluence, rotationCycleInfluence) * cycleAddFactor;

        currentCycle += cycleDelta;

        // Wrap
        if (currentCycle > 1.0f)
        {
            currentCycle = currentCycle - 1.0f;
            SwapInCycle();
        }
    }

    private void UpdateProjection()
    {
        projection.GlobalPosition = GlobalPosition;
        projection.GlobalRotation = GlobalRotation;

        Vector3 projectedGlobal = GlobalPosition;
        Vector3 projectedForward = -projection.Basis.Z * Velocity.Length() / 60.0f;
        float projectedRotation = 0.0f;

        int iterations = 0;

        while (iterations < projectionIterations)
        {
            iterations += 1;
            projectedRotation += currentRotation;
            projectedForward = projectedForward.Rotated(Vector3.Up, currentRotation);
            projectedGlobal += projectedForward;
        }

        projection.GlobalPosition = projectedGlobal;
        projection.Rotate(Vector3.Up, projectedRotation);
    }

    private void UpdateRaycastProjections()
    {
        Vector3 relativeVelocity = Velocity * Transform.Basis;
        int moveDirection = Mathf.Sign(relativeVelocity.Z);
        int turnDirection = Mathf.Sign(currentRotation);

        currentMoveFactor = Mathf.Abs(moveSpeed) / maxSpeed;
        currentRotationFactor = Mathf.Abs(currentRotation) / factorMaxRotation;

        UpdateRaycastRotation(turnDirection);
        UpdateRaycastPosition(moveDirection, turnDirection);
        UpdateIndividualRaycasts();

        // Debug
        raycastContainer.Position += raycastContainer.Basis.Z * debugOffsetRaycastContainerLocal.Z;
        raycastContainer.Position += raycastContainer.Basis.X * debugOffsetRaycastContainerLocal.X;
        raycastContainer.Position += debugOffsetRaycastContainer;
    }

    private void UpdateRaycastRotation(int turnDirection)
    {
        raycastContainer.GlobalRotation = projection.GlobalRotation;

        float rotation = targetRotationByRotation.Sample(currentRotationFactor);
        rotation *= rotationReductionBySpeed.Sample(currentMoveFactor);
        rotation += debugRotateRaycastContainer;
        rotation *= turnDirection;

        raycastContainer.Rotate(Vector3.Up, rotation);
    }

    private void UpdateRaycastPosition(int moveDirection, int turnDirection)
    {
        raycastContainer.GlobalPosition = projection.GlobalPosition;

        float projectionOffset = projectionOffsetBySpeed.Sample(currentMoveFactor);

        projectionOffset *= projectionOffsetReductionByRotation.Sample(currentRotationFactor);

        raycastContainer.GlobalPosition += raycastContainer.GlobalBasis.Z * moveDirection * projectionOffset;

        // float relativeOffsetZ = relativeOffsetBySpeed.Sample(currentMoveFactor);

        // float relativeOffsetX = relativeOffsetXBySpeedRotation.Sample(Mathf.Sqrt(currentMoveFactor * currentRotationFactor)) * turnDirection;
        // relativeOffsetZ += relativeOffsetZBySpeedRotation.Sample(Mathf.Sqrt(currentMoveFactor * currentRotationFactor));
        // Vector3 relativeOffset = new Vector3(relativeOffsetX * turnDirection, 0.0f, relativeOffsetZ * moveDirection);


        // projectionOffset *=
        //     projectionOffsetReductionBySpeedRotation.Sample(
        //         Mathf.Sqrt(currentMoveFactor * currentRotationFactor));

        // raycastContainer.Position += relativeOffset;
    }

    private void UpdateIndividualRaycasts()
    {
        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            RayCast3D rayCast = rayCasts[i];
            Node3D raycastOrigin = (Node3D)rayCast.GetParent();
            Node3D raycastPivot = (Node3D)rayCast.GetParent().GetParent();

            // // Radial projection. Lets us set a wider/narrower stance on the fly
            // raycastOrigin.Position = raycastOrigin.Basis * new Vector3(0.0f, 0.0f, -footTargetRadialProjection);

            // ApplyRadialDifferential(raycastPivot, turnDirection, moveDirection, i);
            // ApplyProjectionDifferential(raycastPivot, turnDirection, moveDirection, i);

            // Lock child rotation to ensure it's pointing down
            rayCast.GlobalRotation = Vector3.Zero;
        }
    }

    private void ApplyRadialDifferential(Node3D raycastPivot, int turnDirection, int moveDirection, int legIndex)
    {
        raycastPivot.Rotation = Vector3.Zero;
        float radialDifferential = radialDifferentialByRotation * currentRotationFactor * turnDirection;

        bool turningLeft = turnDirection > 0.0f;
        bool movingForward = moveDirection < 0.0f;

        // Radial diff
        if (movingForward && turningLeft)
        {
            if (!LeftLeg(legIndex))
            {
                raycastPivot.Rotation = new Vector3(0.0f, radialDifferential, 0.0f);
            }
        }
        else if (movingForward && !turningLeft)
        {
            if (LeftLeg(legIndex))
            {
                raycastPivot.Rotation = new Vector3(0.0f, radialDifferential, 0.0f);
            }
        }
        else if (!movingForward && turningLeft)
        {
            if (LeftLeg(legIndex))
            {
                raycastPivot.Rotation = new Vector3(0.0f, radialDifferential, 0.0f);
            }
        }
        else if (!movingForward && !turningLeft)
        {
            if (!LeftLeg(legIndex))
            {
                raycastPivot.Rotation = new Vector3(0.0f, radialDifferential, 0.0f);
            }
        }
    }

    private void ApplyProjectionDifferential(Node3D raycastPivot, int turnDirection, int moveDirection, int legIndex)
    {
        raycastPivot.Position = Vector3.Zero;
        float projectionDifferential = projectionDifferentialByRotation.Sample(currentRotation) * turnDirection;

        bool turningLeft = turnDirection > 0.0f;
        bool movingForward = moveDirection < 0.0f;

        // Radial diff
        if (movingForward && turningLeft)
        {
            if (LeftLeg(legIndex))
            {
                raycastPivot.Position = raycastPivot.Basis.Z * projectionDifferential;
            }
        }
        else if (movingForward && !turningLeft)
        {
            if (LeftLeg(legIndex))
            {
            }
        }
        else if (!movingForward && turningLeft)
        {
            if (LeftLeg(legIndex))
            {

            }
        }
        else if (!movingForward && !turningLeft)
        {
            if (!LeftLeg(legIndex))
            {

            }
        }
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
        Vector3 destination = currentTargets[footIndex];

        float distance = footOrigin.DistanceTo(destination);

        if (distance == 0.0f)
        {
            return;
        }

        if (maxStride == 0.0f)
        {
            GD.PushWarning("maxStride cannot be zero.");
            return;
        }

        float currentHeight = destination.Y;

        if (enableStepHeight)
        {
            float cycleOffset = Mathf.Sin(currentCycle * Mathf.Pi);
            float strideFactor = distance / maxStride;
            strideFactor = Mathf.Clamp(strideFactor, 0.0f, 1.0f);

            float targetHeight = Mathf.Lerp(0.0f, maxHeight * strideFactor, cycleOffset);

            currentHeight = targetHeight * Mathf.Sin(currentCycle * Mathf.Pi);
        }

        Vector3 targetPosition = new Vector3(destination.X, currentHeight, destination.Z);

        Node3D foot = feet[footIndex];

        foot.GlobalPosition = footOrigin.Lerp(targetPosition, currentCycle);

        // // MoveToward isn't guaranteed to reach the target
        // // It's a little easier to insert pauses with it
        // // It's useful for debugging cycle rate
        // float rotationFootSpeed = Mathf.Lerp(0.0f, footSpeedByRotation, currentRotationFactor);
        // float movementFootSpeed = footSpeedBySpeed * currentMoveFactor;
        // float currentFootSpeed = movementFootSpeed + rotationFootSpeed;
        // foot.GlobalPosition = foot.GlobalPosition.MoveToward(targetPosition, currentFootSpeed);
    }

    private int[] GetLegRoots()
    {
        int[] rootChildren = skeleton.GetBoneChildren(0);
        return rootChildren;
    }

    private int[] GetFootBones()
    {
        int[] footBones = new int[feet.Length];
        for (int i = 0; i <= feet.Length - 1; i++)
        {
            int rootIndex = legRoots[i];
            int endIndex = GetBoneEnd(rootIndex);
            footBones[i] = endIndex;
        }

        return footBones;
    }

    private int GetBoneEnd(int index)
    {
        int endIndex = -1;
        bool hasChild = true;
        int currentIndex = index;

        while (hasChild)
        {
            int[] children = skeleton.GetBoneChildren(currentIndex);
            if (children.Length == 0)
            {
                hasChild = false;
            }
            else
            {
                currentIndex = children[0];
            }
        }

        return endIndex;
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

    public void ResetFeet()
    {
        raycastContainer.Position = Vector3.Zero;
        raycastContainer.Rotation = Vector3.Zero;

        for (int i = 0; i <= footBones.Length - 1; i++)
        {
            rayCasts[i].ForceRaycastUpdate();

            Vector3 footGlobal = rayCasts[i].GetCollisionPoint();
            footOrigins[i] = footGlobal;
            currentTargets[i] = footGlobal;
            feet[i].GlobalPosition = footGlobal;
        }
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
            else
            {
                // Snap the rest of the way to destination
                // Fixes not reaching target at high cycle speeds
                feet[i].GlobalPosition = currentTargets[i];
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
