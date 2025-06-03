using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace PanelLayout
{
    public class Point3dPairComparer : IEqualityComparer<(Point3d, Point3d)>
    {
        public bool Equals((Point3d, Point3d) a, (Point3d, Point3d) b)
        {
            return (IsClose(a.Item1, b.Item1) && IsClose(a.Item2, b.Item2)) ||
                   (IsClose(a.Item1, b.Item2) && IsClose(a.Item2, b.Item1));
        }

        public int GetHashCode((Point3d, Point3d) key)
        {
            // Order-independent hash
            int h1 = key.Item1.GetHashCode();
            int h2 = key.Item2.GetHashCode();
            return h1 ^ h2;
        }

        private bool IsClose(Point3d p1, Point3d p2, double tol = 1e-4)
        {
            return p1.DistanceTo(p2) < tol;
        }
    }

}
