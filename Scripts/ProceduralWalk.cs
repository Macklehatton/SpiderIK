using Godot;
using System;

public partial class ProceduralWalk : CharacterBody3D
{
    [Export] private float raycastDistance;
    [Export] private float raycastHeight;
    [Export] private float raycastForwardOffset;

    [Export] private Node3D footContainer;
    [Export] private float strideDistance;
    [Export] private float cycleRate;
    [Export] private float footSpeed;

    [Export] private float moveSpeed;
    [Export] private float turnRate;
    [Export] private float turnProjection;

    private Node3D[] feet;
    private RayCast3D[] rayCasts;
    // Feet that we currently prefer to move 
    // for a balanced walk
    private bool[] inCycle;
    // Feet currently moving
    private bool[] feetMoving;
    private Vector3[] currentTargets;

    private bool offFoot;
    private float strideDistanceSquared;

    private float currentCycle;

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

        strideDistanceSquared = strideDistance * strideDistance;

        // We don't want the foot IK targets moving with the character
        footContainer.CallDeferred("reparent", GetTree().Root);
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateCycle((float)delta);

        Rotate(Vector3.Up, Mathf.Pi * turnRate * moveSpeed * (float)delta);

        Velocity = -Transform.Basis.Z * moveSpeed;
        MoveAndSlide();
        MoveFeet();
        DebugDraw();
    }

    public void DebugDraw()
    {
        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            //DebugDraw3D.DrawBox(rayCasts[i].GlobalPosition, rayCasts[i].Quaternion, Vector3.One);
            DebugDraw3D.DrawSphere(currentTargets[i]);
        }
    }


    public void UpdateCycle(float delta)
    {
        currentCycle += cycleRate * moveSpeed * delta;

        // Wrap
        if (currentCycle > 1.0f)
        {
            currentCycle = currentCycle - 1.0f;
            SwapInCycle();
        }
    }

    public void UpdateRaycasts()
    {
        Vector3 forward = -Transform.Basis.Z;
        float dot = forward.Dot(Velocity);

        Vector3 projectionOffset = new Vector3(0.0f, 0.0f, 0.0f);

        //projectionOffset.Rotated(Vector3.Up, turnProjection * dot);
        //projectionOffset += new Vector3(0.0f, 0.0f, -raycastForwardOffset);

        for (int i = 0; i <= rayCasts.Length - 1; i++)
        {
            rayCasts[i].Position = projectionOffset;
        }
    }

    public void MoveFeet()
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

    public bool CheckMoveFoot(int footIndex)
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

    public bool CheckDistance(int footIndex)
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

    public void MoveFoot(int footIndex)
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

    public Node3D[] GetFeet()
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


    public RayCast3D[] AddRayCasts(Node3D[] feet)
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

            rayCast.Position = new Vector3(0.0f, 0.0f, -raycastForwardOffset);
            rayCast.TargetPosition = new Vector3(0.0f, -raycastDistance, 0.0f);
            rayCasts[i] = rayCast;
        }
        return rayCasts;
    }

    public void SwapInCycle()
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

    public void SetAlternateFeet()
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
    public int Opposite(int i)
    {
        int row = feet.Length / 2;
        int opposite = i + row;

        if (opposite > feet.Length - 1)
        {
            opposite = opposite - feet.Length;
        }
        return opposite;
    }

    public void SetInitialTargets()
    {
        for (int i = 0; i <= currentTargets.Length - 1; i++)
        {
            currentTargets[i] = feet[i].GlobalPosition + new Vector3(0.0f, 0.0f, -raycastForwardOffset);
        }
    }
}
