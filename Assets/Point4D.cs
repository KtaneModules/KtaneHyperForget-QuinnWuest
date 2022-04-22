using System;
using UnityEngine;

namespace TheHypercube
{
    struct Point4D
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }
        public double W { get; private set; }

        public Point4D(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        private static readonly float wLen = .4f;//1 / Mathf.Sqrt(3);
        private static readonly Vector3 wVec = new Vector3(wLen, wLen, wLen);

        public Vector3 Project()
        {
            return new Vector3((float) X, (float) Y, (float) Z) + (float) W * wVec;
        }

        public static Point4D operator *(Point4D p, double[] matrix)
        {
            return new Point4D
            (
                matrix[0] * p.X + matrix[1] * p.Y + matrix[2] * p.Z + matrix[3] * p.W,
                matrix[4] * p.X + matrix[5] * p.Y + matrix[6] * p.Z + matrix[7] * p.W,
                matrix[8] * p.X + matrix[9] * p.Y + matrix[10] * p.Z + matrix[11] * p.W,
                matrix[12] * p.X + matrix[13] * p.Y + matrix[14] * p.Z + matrix[15] * p.W
            );
        }
    }
}
