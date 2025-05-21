using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Face = Autodesk.AutoCAD.BoundaryRepresentation.Face;
using Surface = Autodesk.AutoCAD.Geometry.Surface;

namespace ConcaveConvexEdgeVisualizer
{
    public class Commands
    {
        public const double EPSILON = 10e-10;

        [CommandMethod("PNL")]
        public void HighlightConcaveConvexEdges()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nSelect 3D solid: ");
                peo.SetRejectMessage("\nOnly 3D solids are allowed.");
                peo.AddAllowedClass(typeof(Solid3d), false);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                Solid3d solid = (Solid3d)tr.GetObject(per.ObjectId, OpenMode.ForRead);

                using (Brep brep = new Brep(solid))
                {
                    var faces = brep.Faces.Cast<Face>().ToArray();
                    var edgeToFaces = new Dictionary<(Point3d, Point3d), List<Face>>();

                    foreach (Face face in faces)
                    {
                        foreach (BoundaryLoop loop in face.Loops)
                        {
                            foreach (Edge edge in loop.Edges)
                            {
                                Point3d v1 = new Point3d(
                                    Math.Round(edge.Vertex1.Point.X),
                                    Math.Round(edge.Vertex1.Point.Y),
                                    Math.Round(edge.Vertex1.Point.Z)
                                );
                                Point3d v2 = new Point3d(
                                    Math.Round(edge.Vertex2.Point.X),
                                    Math.Round(edge.Vertex2.Point.Y),
                                    Math.Round(edge.Vertex2.Point.Z)
                                );

                                var key = (v1.X < v2.X) || (v1.X == v2.X && v1.Y < v2.Y) || (v1.X == v2.X && v1.Y == v2.Y && v1.Z < v2.Z)
                                    ? (v1, v2)
                                    : (v2, v1);

                                if (!edgeToFaces.TryGetValue(key, out var faceList))
                                {
                                    faceList = new List<Face>();
                                    edgeToFaces[key] = faceList;
                                }

                                if (!faceList.Contains(face))
                                    faceList.Add(face);
                            }
                        }
                    }

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var kvp in edgeToFaces)
                    {
                        var key = kvp.Key;
                        var faceList = kvp.Value;

                        if (faceList.Count != 2) continue;

                        Vector3d? n1 = TryGetSurfaceNormal(faceList[0].Surface);
                        Vector3d? n2 = TryGetSurfaceNormal(faceList[1].Surface);

                        if (n1.HasValue && n2.HasValue)
                        {
                            Vector3d u1 = n1.Value.GetNormal();
                            Vector3d u2 = n2.Value.GetNormal();

                            //if ((u1.Z - u2.Z) > EPSILON)
                            //{
                                Vector3d cross = u1.CrossProduct(u2);
                                Vector3d direction = key.Item2 - key.Item1;
                                double sign = cross.DotProduct(direction) >= 0 ? 1 : -1;
                                double angleDeg = u1.GetAngleTo(u2) * (180.0 / Math.PI) * sign;

                                ed.WriteMessage($"\nEdge: {key.Item1} → {key.Item2}");
                                ed.WriteMessage($"\n    Sign: {sign}");
                                ed.WriteMessage($"\n    Normal 1: {u1}");
                                ed.WriteMessage($"\n    Normal 2: {u2}");
                                ed.WriteMessage($"\n    Angle: {angleDeg:F1}°");

                                int colorIndex;
                                if (angleDeg > 0)
                                    colorIndex = 1; // Concave → Red
                                else
                                    colorIndex = 3; // Convex → Green

                                Line edgeLine = new Line(key.Item1, key.Item2)
                                {
                                    ColorIndex = colorIndex
                                };

                                btr.AppendEntity(edgeLine);
                                tr.AddNewlyCreatedDBObject(edgeLine, true);
                            //}
                        }
                    }
                }

                tr.Commit();
            }
        }

        public static Vector3d? TryGetSurfaceNormal(Surface surf)
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
