using Godot;
using System;
using System.Diagnostics;
using System.Linq;

public partial class ProceduralWalk : CharacterBody3D
{
    [Export] private Node3D footContainer;
    [Export] private Skeleton3D skeleton;

    [Export] private float raycastDistance;
    [Export] private float raycastHeight;
    [Export] private float raycastForwardOffset;

    [Export] private float strideDistance;
    [Export] private float cycleRate;
    [Export] private float footSpeed;
    [Export] private float maxDifferential;

    [Export] private float moveSpeed;
    [Export] private float turnRate;
    [Export] private float projectionRotation;
    [Export] private float radialProjection;
    [Export] private float factorMaxRotation;

    private Node3D[] feet;
    private RayCast3D[] rayCasts;
    private int[] legRoots;
    // Feet that we currently prefer to move 
    // for a balanced walk
    private bool[] inCycle;
    // Feet currently moving
    private bool[] feetMoving;
    private Vector3[] currentTargets;

    private bool offFoot;
    private float strideDistanceSquared;

    private float currentCycle;
    private float currentRotation;

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
        SetInitialTargets();

        legRoots = GetLegRoots();

        strideDistanceSquared = strideDistance * strideDistance;

        // We don't want the foot IK targets moving with the character
        footContainer.CallDeferred("reparent", GetTree().Root);
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateCycle((float)delta);

        currentRotation = turnRate / Engine.PhysicsTicksPerSecond * moveSpeed;
        Rotate(Vector3.Up, currentRotation);
        Velocity = -Transform.Basis.Z * moveSpeed;

        UpdateRaycastProjections();

        MoveAndSlide();
        MoveFeet();
        DebugDraw();
    }

    private void DebugDraw()
    {
        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            //DebugDraw3D.DrawBox(rayCasts[i].GlobalPosition, rayCasts[i].Quaternion, Vector3.One);
            DebugDraw3D.DrawSphere(currentTargets[i], 0.25f, Colors.PaleVioletRed);
        }
    }

    private void UpdateCycle(float delta)
    {
        currentCycle += cycleRate * moveSpeed * delta;

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

        float rotationFactor = Remap(currentRotation, -factorMaxRotation, factorMaxRotation, -1.0f, 1.0f);
        float projectionRotation = this.projectionRotation * rotationFactor;



        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            Vector3 legRootPosition = skeleton.GetBoneGlobalPose(legRoots[i]).Origin;
            legRootPosition = ToGlobal(legRootPosition);

            Vector3 radialOffset = (legRootPosition - GlobalPosition).Normalized();
            radialOffset = radialOffset * radialProjection;
            radialOffset = radialOffset.Rotated(Vector3.Up, projectionRotation);

            //Vector3 differentialApplied = ApplyDifferential(projectionOffset, rotationFactor, i);

            rayCasts[i].GlobalPosition = legRootPosition + radialOffset;

            DebugDraw3D.DrawSphere(rayCasts[i].GlobalPosition);
            // Root to current
            DebugDraw3D.DrawLine(legRootPosition, rayCasts[i].GlobalPosition);
        }
    }

    private Vector3 ApplyDifferential(Vector3 projectionOffset, float rotationFactor, int raycastIndex)
    {
        Vector3 differentialApplied = projectionOffset;

        if (rotationFactor > 0.0f)
        {
            if (LeftLeg(raycastIndex))
            {
                differentialApplied *= 1.0f - maxDifferential;
            }
            else
            {
                differentialApplied *= 1.0f + maxDifferential * 0.25f;
            }
        }
        else
        {
            if (!LeftLeg(raycastIndex))
            {
                differentialApplied *= 1.0f - maxDifferential;
            }
            else
            {
                differentialApplied *= 1.0f + maxDifferential * 0.25f;
            }
        }
        return differentialApplied;
    }

    private static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
    {
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

        Vector3 targetPosition = currentTargets[footIndex];

        Node3D foot = feet[footIndex];

        foot.GlobalPosition = foot.GlobalPosition.MoveToward(targetPosition, moveSpeed * footSpeed);

        // Uncached
        //foot.GlobalPosition = foot.GlobalPosition.MoveToward(raycast.GetCollisionPoint(), moveSpeed * footSpeed);


        //foot.GlobalPosition = foot.GlobalPosition.Lerp(targetPosition, currentCycle);
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

        for (int i = 0; i <= feet.Length - 1; i++)
        {
            Node3D foot = feet[i];

            Node3D raycastOrigin = new Node3D();
            AddChild(raycastOrigin);
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
    // 0 4
    // 1 5
    // 2 6
    // 3 7
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
