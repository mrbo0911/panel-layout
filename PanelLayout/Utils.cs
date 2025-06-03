using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Linq;


namespace PanelLayout
{
    public static class Utils
    {
        public static Point3d? TryGetFaceCentroid(Autodesk.AutoCAD.BoundaryRepresentation.Face face)
        {
            try
            {
                var loops = face.Loops.Cast<BoundaryLoop>();
                var vertices = new List<Point3d>();

                foreach (var loop in loops)
                {
                    foreach (Edge edge in loop.Edges)
                    {
                        vertices.Add(edge.Vertex1.Point);
                        vertices.Add(edge.Vertex2.Point);
                    }
                }

                // Remove duplicates  
                var unique = vertices.Distinct(new Point3dEqualityComparer()).ToList();

                if (unique.Count == 0)
                    return null;

                // Average coordinates  
                double x = unique.Average(p => p.X);
                double y = unique.Average(p => p.Y);
                double z = unique.Average(p => p.Z);

                return new Point3d(x, y, z);
            }
            catch
            {
                return null;
            }
        }

        public static List<BoundedPlane> GetBoundedPlane(Autodesk.AutoCAD.BoundaryRepresentation.Face face)
        {
            var boundedPlanes = new List<BoundedPlane>();

            var loops = face.Loops.Cast<BoundaryLoop>();
            var vertices = new List<Point3d>();

            foreach (var loop in loops)
            {
                foreach (Edge edge in loop.Edges)
                {
                    vertices.Add(edge.Vertex1.Point);
                    vertices.Add(edge.Vertex2.Point);
                }
            }

            // Remove duplicates  
            var uniqueVertices = vertices.Distinct(new Point3dEqualityComparer()).ToList();

            if (uniqueVertices.Count < 3)
                return boundedPlanes; // Not enough points to form a plane

            // reorder vertices to ensure they are in a consistent clockwise order
            uniqueVertices = uniqueVertices.OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z).ToList();

            for (int i = 0; i < uniqueVertices.Count - 2; i++)
            {
                Point3d p1 = uniqueVertices[i];
                Point3d p2 = uniqueVertices[i+1];
                Point3d p3 = uniqueVertices[i+2];
                // Create a bounded plane from the three points
                BoundedPlane boundedPlane = new BoundedPlane(p1, p2, p3);
                boundedPlanes.Add(boundedPlane);
            }

            return boundedPlanes;
        }

        public static bool IsPointInsideSolid(Solid3d solid, Point3d midPoint, Point3d testPoint)
        {
            Vector3d direction = testPoint - midPoint;
            Ray3d ray = new Ray3d(midPoint, direction);

            int intersectionCount = 0;
            List<Point3d> intersectionPoints = new List<Point3d>(); // Changed from Point3dCollection to List<Point3d>  

            using (Brep brep = new Brep(solid))
            {
                var faces = brep.Faces.Cast<Autodesk.AutoCAD.BoundaryRepresentation.Face>().ToArray();

                // Iterate through each face and check for intersections  
                foreach (var face in faces)
                {
                    Vector3d? n = TryGetSurfaceNormal(face.Surface);
                    if (n.HasValue)
                    {
                        //// Check if the ray is parallel to the X-axis
                        //if (n.Value.IsParallelTo(Vector3d.YAxis))
                        //{
                            List<BoundedPlane> boundedPlanes = GetBoundedPlane(face);
                            foreach (var boundedPlane in boundedPlanes)
                            {
                                Point3d[] pts = ray.IntersectWith(boundedPlane);
                                if (pts == null || pts.Length == 0)
                                {
                                    continue;
                                }
                                else
                                {
                                    foreach (var pt in pts)
                                    {
                                        intersectionPoints.Add(pt);
                                    }
                                }
                            }
                        //}
                    }
                }

                // remove duplicate points and midpoint  
                intersectionPoints = intersectionPoints.Distinct(new Point3dEqualityComparer()).ToList(); // Fixed the error by using List<T>.Distinct()  
                intersectionCount += intersectionPoints.Count;

                // Check if there is 3 intersection points forms a line
                var count = intersectionPoints.Count;
                if (count >= 3)
                {
                    for (int i = 0; i < count - 2; i++)
                    {
                        for (int j = i + 1; j < count - 1; j++)
                        {
                            for (int k = j + 1; k < count; k++)
                            {
                                Point3d p1 = intersectionPoints[i];
                                Point3d p2 = intersectionPoints[j];
                                Point3d p3 = intersectionPoints[k];

                                // Check if the points are collinear
                                if ((p2 - p1).IsParallelTo(p3 - p1))
                                {
                                    intersectionCount -= 1; // Reduce the count by 1 for each collinear set
                                }
                            }
                        }
                    }
                }
            }

            // Return true if the number of intersections is odd
            if (intersectionCount % 2 == 1) // Concave
                return true;
            else
                return false;
        }

        private static bool IsPointOnFace(Autodesk.AutoCAD.BoundaryRepresentation.Face face, Point3d point)
        {
            try
            {
                // Check if the point is contained within the face boundary  
                PointContainment containment;
                face.GetPointContainment(point, out containment);
                return containment == PointContainment.Inside;
            }
            catch
            {
                return false;
            }
        }

        public static Vector3d? TryGetSurfaceNormal(Autodesk.AutoCAD.Geometry.Surface surf)
        {
            try
            {
                var envelope = surf.GetEnvelope();
                double u = (envelope[0].LowerBound + envelope[0].UpperBound) / 2;
                double v = (envelope[1].LowerBound + envelope[1].UpperBound) / 2;

                Point2d param = new Point2d(u, v);
                Vector3dCollection derivs = new Vector3dCollection();
                Vector3d normal = new Vector3d();

                surf.EvaluatePoint(param, 1, ref derivs, ref normal);
                return normal.GetNormal();
            }
            catch
            {
                return null;
            }
        }
    }
}
