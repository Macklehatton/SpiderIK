using Godot;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VectorExtensions
{
    public static class Vector3Extensions
    {
        public static Vector3I RoundToInt(this Vector3 vector)
        {
            int intX = (int)Math.Round(vector.X, MidpointRounding.AwayFromZero);
            int intY = (int)Math.Round(vector.Y, MidpointRounding.AwayFromZero);
            int intZ = (int)Math.Round(vector.Z, MidpointRounding.AwayFromZero);
            return new Vector3I(intX, intY, intZ);
        }

        public static bool AnyComponentNegative(Vector3I vector)
        {
            if (vector.X < 0 || vector.Y < 0 || vector.Z < 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static Vector3I DivideToNearest(Vector3I numerator, float demoninator)
        {
            float X = numerator.X / demoninator;
            float Y = numerator.Y / demoninator;
            float Z = numerator.Z / demoninator;

            int intX = (int)Math.Round(X, MidpointRounding.AwayFromZero);
            int intY = (int)Math.Round(Y, MidpointRounding.AwayFromZero);
            int intZ = (int)Math.Round(Z, MidpointRounding.AwayFromZero);

            return new Vector3I(intX, intY, intZ);
        }

        public static Vector2I RoundToOrthogonal(Vector3 vector)
        {
            Vector3 absoluteVector = vector.Abs();

            if (absoluteVector.X > absoluteVector.Z)
            {
                int value = Mathf.Sign(vector.X);
                return new Vector2I(value, 0);
            }
            else
            {
                int value = Mathf.Sign(vector.Y);
                return new Vector2I(0, value);
            }
        }

        public static Vector2I RoundToDiagonal(this Vector3 vector)
        {
            int x = Mathf.Sign(vector.X);
            int y = Mathf.Sign(vector.Z);

            return new Vector2I(x, y);
        }

        public static Vector3 RandomPlanarDirection()
        {
            RandomNumberGenerator random = new RandomNumberGenerator();

            float X = random.Randf() * RandomSign();
            float Y = 0.0f;
            float Z = random.Randf() * RandomSign();

            return new Vector3(X, Y, Z).Normalized();
        }

        public static Vector3 RandomUpwardVector()
        {
            RandomNumberGenerator random = new RandomNumberGenerator();

            float X = random.Randf() * RandomSign();
            float Y = random.Randf();
            float Z = random.Randf() * RandomSign();

            return new Vector3(X, Y, Z).Normalized();
        }

        public static float RandomSign()
        {
            RandomNumberGenerator random = new RandomNumberGenerator();

            float value = random.Randf();

            if (value >= 0.5)
            {
                return 1.0f;
            }
            else
            {
                return -1.0f;
            }
        }


        /// <summary>
        /// All positions around a position including diagonals and itself
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        /// . . .
        /// . X .
        /// . . .
        public static List<Vector3I> AdjacentPositions(this Vector3I position)
        {
            List<Vector3I> positions = new List<Vector3I>()
            {
                position + new Vector3I(-1,  0,  0),
                position + new Vector3I( 0,  0,  0),
                position + new Vector3I( 1,  0,  0),

                position + new Vector3I(-1,  0,  1),
                position + new Vector3I( 0,  0,  1),
                position + new Vector3I( 1,  0,  1),

                position + new Vector3I(-1,  0, -1),
                position + new Vector3I( 0,  0, -1),
                position + new Vector3I( 1,  0, -1),
            };
            return positions;
        }


        /// <summary>
        /// Positions adjacent to a position including itself and excluding diagonals
        /// </summary>
        ///   . 
        /// . X .
        ///   .
        public static List<Vector3I> OrthogonalPositions(this Vector3I position)
        {
            List<Vector3I> positions = new List<Vector3I>()
            {
                position + new Vector3I(-1,  0,  0),
                position + new Vector3I( 0,  0,  0),
                position + new Vector3I( 1,  0,  0),
                position + new Vector3I( 0,  0,  1),
                position + new Vector3I( 0,  0, -1),
            };
            return positions;
        }

        public static Vector3 PlanarPosition(this Vector3 position)
        {
            Vector3 planarPosition = new Vector3(
                position.X,
                0.0f,
                position.Z);

            return planarPosition;
        }

        public static Vector2 PlanarVector2(this Vector3 position)
        {
            Vector2 planarPosition = new Vector2(
                position.X,
                position.Z);

            return planarPosition;
        }

        public static Vector3I PlanarPosition(this Vector3I position)
        {
            Vector3I planarPosition = new Vector3I(
                position.X,
                0,
                position.Z);

            return planarPosition;
        }

        public static Vector2I PlanarVector2I(this Vector3I position)
        {
            Vector2I planarPosition = new Vector2I(
                position.X,
                position.Z);

            return planarPosition;
        }
    }
}