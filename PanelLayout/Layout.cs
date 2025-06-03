using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using PanelLayout;
using System;
using System.Collections.Generic;
using System.Linq;
using Face = Autodesk.AutoCAD.BoundaryRepresentation.Face;

namespace ConcaveConvexEdgeVisualizer
{
    public class Commands
    {
        // Static list to keep track of temporary lines created during PNL command
        private static List<ObjectId> tempLines = new List<ObjectId>();

        public const double EPSILON = 10e-3;

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
                    var edgeToFaces = new Dictionary<(Point3d, Point3d), List<Face>>(new Point3dPairComparer());

                    foreach (Face face in faces)
                    {
                        foreach (BoundaryLoop loop in face.Loops)
                        {
                            foreach (Edge edge in loop.Edges)
                            {
                                Point3d v1 = edge.Vertex1.Point;
                                Point3d v2 = edge.Vertex2.Point;

                                // Ensure v1 is always the smaller point to avoid duplicates
                                var temp = (v2 - v1).GetNormal();
                                if (temp.X < 0 || temp.Y < 0 || temp.Z < 0)
                                {
                                    var temp2 = v1;
                                    v1 = v2;
                                    v2 = temp2;
                                }
                                var key = (v1, v2);

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

                    var index = 0;
                    foreach (var kvp in edgeToFaces)
                    {
                        var key = kvp.Key;
                        var faceList = kvp.Value;

                        if (faceList.Count != 2) continue;

                        Point3d edgeMid = new Point3d(
                            (key.Item1.X + key.Item2.X) / 2,
                            (key.Item1.Y + key.Item2.Y) / 2,
                            (key.Item1.Z + key.Item2.Z) / 2
                        );

                        Vector3d? n1 = Utils.TryGetSurfaceNormal(faceList[0].Surface);
                        Vector3d? n2 = Utils.TryGetSurfaceNormal(faceList[1].Surface);

                        if (n1.HasValue && n2.HasValue)
                        {
                            //if (Math.Abs((key.Item1.Y - key.Item2.Y)) > EPSILON)
                            //{
                                index++;

                                if (index > 0)
                                {
                                    bool isConvex = IsEdgeConvex(solid, faceList, edgeMid, key);

                                    int colorIndex;
                                    string colorName;
                                    if (isConvex)
                                    {
                                        colorIndex = 1; // Concave → Red
                                        colorName = "Red - Concave";
                                    }
                                    else
                                    {
                                        colorIndex = 3; // Convex → Green
                                        colorName = "Green - Convex";
                                    }

                                    Line edgeLine = new Line(key.Item1, key.Item2)
                                    {
                                        ColorIndex = colorIndex
                                    };

                                    ed.WriteMessage($"\nEdge {index}:");
                                    ed.WriteMessage($"\n    Co-ordinate: {Math.Round(key.Item1.X)},{Math.Round(key.Item1.Y)},{Math.Round(key.Item1.Z)} → " +
                                                                       $"{Math.Round(key.Item2.X)},{Math.Round(key.Item2.Y)},{Math.Round(key.Item2.Z)}");
                                    ed.WriteMessage($"\n    isConvex: {isConvex}");
                                    ed.WriteMessage($"\n    Color: {colorName}");
                                    ed.WriteMessage($"\n    Legend: Green = Convex, Red = Concave\n");

                                    btr.AppendEntity(edgeLine);
                                    tr.AddNewlyCreatedDBObject(edgeLine, true);


                                    //Point3d centroid0 = Utils.TryGetFaceCentroid(faceList[0]).GetValueOrDefault();
                                    //Point3d centroid1 = Utils.TryGetFaceCentroid(faceList[1]).GetValueOrDefault();
                                    //Point3d testPoint = new Point3d(
                                    //    (centroid0.X + centroid1.X) / 2,
                                    //    (centroid0.Y + centroid1.Y) / 2,
                                    //    (centroid0.Z + centroid1.Z) / 2
                                    //);

                                    //Vector3d direction = testPoint - edgeMid;
                                    //Point3d endPoint = edgeMid + direction.GetNormal() * 100; // scale to some visible length
                                    //Line testLine = new Line(edgeMid, endPoint)
                                    //{
                                    //    ColorIndex = 6
                                    //};

                                    //btr.AppendEntity(testLine);
                                    //tr.AddNewlyCreatedDBObject(testLine, true);

                                    tempLines.Add(edgeLine.ObjectId); // Store reference
                                }




                                //var centroid = Utils.TryGetFaceCentroid(faceList[0]);
                                //if (centroid.HasValue)
                                //{
                                //    DBText label = new DBText
                                //    {
                                //        Position = centroid.Value,
                                //        Height = 0.5,
                                //        TextString = $"{index}",
                                //        ColorIndex = 2 // yellow text
                                //    };

                                //    btr.AppendEntity(label);
                                //    tr.AddNewlyCreatedDBObject(label, true);
                                //}

                                //centroid = Utils.TryGetFaceCentroid(faceList[1]);
                                //if (centroid.HasValue)
                                //{
                                //    DBText label = new DBText
                                //    {
                                //        Position = centroid.Value,
                                //        Height = 0.5,
                                //        TextString = $"F1",
                                //        ColorIndex = 4 // yellow text
                                //    };

                                //    btr.AppendEntity(label);
                                //    tr.AddNewlyCreatedDBObject(label, true);
                                //}

                            }
                        //}
                    }
                }

                tr.Commit();
            }
        }

        [CommandMethod("DELPNL")]
        public void DeletePnlLines()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in tempLines)
                {
                    try
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite, false) as Entity;
                        if (ent != null)
                        {
                            ent.Erase();
                        }
                    }
                    catch
                    {
                        // Entity might already be erased or invalid
                    }
                }
                tr.Commit();
            }

            tempLines.Clear(); // Reset list
            ed.WriteMessage("\nTemporary PNL lines deleted.");
        }

        private bool IsEdgeConvex(Solid3d solid, List<Face> faceList, Point3d edgeMid, (Point3d, Point3d) key)
        {
            Point3d centroid1 = Utils.TryGetFaceCentroid(faceList[0]).GetValueOrDefault();
            Point3d centroid2 = Utils.TryGetFaceCentroid(faceList[1]).GetValueOrDefault();

            var edgeDirection = (key.Item2 - key.Item1).GetNormal();

            var n1 = Utils.TryGetSurfaceNormal(faceList[0].Surface);
            var n2 = Utils.TryGetSurfaceNormal(faceList[1].Surface);

            // calculate the cross vectors from normals and the edge direction
            Vector3d cross1 = n1.HasValue ? n1.Value.CrossProduct(edgeDirection) : Vector3d.ZAxis;
            Vector3d cross2 = n2.HasValue ? n2.Value.CrossProduct(edgeDirection) : Vector3d.ZAxis;

            // calculate the points offset from the edge midpoint follow the cross vectors
            Point3d offsetPoint1 = edgeMid + cross1 * 0.5; // offset in the direction of the first face normal
            Point3d offsetPoint2 = edgeMid + cross2 * 0.5; // offset in the direction of the second face normal

            PointContainment pointContainment1, pointContainment2;
            faceList[0].GetPointContainment(offsetPoint1, out pointContainment1);
            faceList[1].GetPointContainment(offsetPoint2, out pointContainment2);

            if (pointContainment1 != PointContainment.Inside)
            {
                offsetPoint1 = edgeMid - cross1 * 0.5; // reverse offset if angle is obtuse
            }

            if (pointContainment2 != PointContainment.Inside)
            {
                offsetPoint2 = edgeMid - cross2 * 0.5; // reverse offset if angle is obtuse
            }

            Point3d testPoint = new Point3d(
                (offsetPoint1.X + offsetPoint2.X) / 2,
                (offsetPoint1.Y + offsetPoint2.Y) / 2,
                (offsetPoint1.Z + offsetPoint2.Z) / 2
            );

            return Utils.IsPointInsideSolid(solid, edgeMid, testPoint);
        }
    }
}
