using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace PanelLayout
{
    public class Point3dEqualityComparer : IEqualityComparer<Point3d>
    {
        private const double Tolerance = 1e-6;

        public bool Equals(Point3d p1, Point3d p2)
        {
            return p1.IsEqualTo(p2, new Tolerance(Tolerance, Tolerance));
        }

        public int GetHashCode(Point3d p)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + p.X.GetHashCode();
                hash = hash * 23 + p.Y.GetHashCode();
                hash = hash * 23 + p.Z.GetHashCode();
                return hash;
            }
        }
    }
}
