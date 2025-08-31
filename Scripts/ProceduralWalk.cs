using Godot;
using System;

public partial class ProceduralWalk : CharacterBody3D
{
    [Export] private float raycastDistance;
    [Export] private float raycastHeight;
    [Export] private Node3D footContainer;
    [Export] private float strideDistance;
    [Export] private float cycleRate;

    [Export] private float moveSpeed;
    [Export] private float turnRate;

    private Node3D[] feet;
    private RayCast3D[] rayCasts;
    // Feet that we currently prefer to move 
    // for a balanced walk
    private bool[] inCycle;
    // Feet currently moving
    private bool[] feetMoving;

    private bool offFoot;
    private float strideDistanceSquared;

    private float currentCycle;

    public override void _Ready()
    {
        // Giving my GPU a break
        Engine.MaxFps = 60;

        feet = GetFeet();
        rayCasts = AddRayCasts(feet);
        inCycle = new bool[feet.Length];
        feetMoving = new bool[feet.Length];
        SetAlternateFeet();

        strideDistanceSquared = strideDistance * strideDistance;

        // We don't want the foot IK targets moving with the character
        footContainer.CallDeferred("reparent", GetTree().Root);
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateCycle((float)delta);

        Rotate(Vector3.Up, Mathf.Pi * turnRate * moveSpeed * (float)delta);

        Velocity = Transform.Basis.Z * moveSpeed;
        MoveAndSlide();
        MoveFeet();
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

        if (CheckDistance(footIndex))
        {
            return true;
        }

        return false;
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

        Vector3 targetPosition = raycast.GetCollisionPoint();

        Node3D foot = feet[footIndex];

        foot.GlobalPosition = foot.GlobalPosition.Lerp(targetPosition, currentCycle);
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

            RayCast3D rayCast = new RayCast3D();
            rayCast.Name = "Raycast_" + foot.Name;
            AddChild(rayCast);
            rayCast.GlobalPosition = foot.GlobalPosition;
            rayCast.GlobalPosition += new Vector3(0.0f, raycastHeight, 0.0f);
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
}
