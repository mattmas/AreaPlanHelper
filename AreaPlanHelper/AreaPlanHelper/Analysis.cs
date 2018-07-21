using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaShooter
{
    /// <summary>
    /// Implement a strategy for Cleanup
    /// </summary>
    public class Analysis
    {
        private Document _roomDoc;
        private Phase _targetPhase;
        
        private IList<RoomObject> _allRooms;

        private enum OffsetDir { Outward, Inward, None};

        #region PublicMethods
        public void Analyze(Document roomDoc, Phase targetPhase, IList<RoomObject> rooms)
        {
            // strategy 1:
            _roomDoc = roomDoc;
            _targetPhase = targetPhase;
            _allRooms = rooms;

            foreach( var room in rooms )
            {
                // work on each room
               
                    

                    // adjust lines by rules
                    adjustByRules(room);

                    try
                    {
                        cleanupRules(room);
                    }
                    catch (Exception ex)
                    {
                        log("Error during cleanup of room: " + room.Number + ": " + ex.GetType().Name + ": " + ex.Message);
                    }
              
            }

        }
        #endregion

        #region PrivateMethods
        private void adjustByRules(RoomObject ro)
        {
            // remove any column type boundaries
            var columns = ro.Boundaries.Where(b => b.ElementType == "Columns").ToList();
            foreach (var col in columns) ro.Boundaries.Remove(col);

            bool isCorridor = (ro.Name.ToUpper().Contains("CORR"));
            if (isCorridor)
            {
                ignoreRoom(ro);
                return;
            }

                // go through the boundaries and offset.
            foreach ( var seg in ro.Boundaries )
            {
                if (isCorridor)
                {
                    // we skip it unless it doesn't have an opposite
                    if (seg.OppositeRoom != null) continue; 
                }
                
                OffsetDir offset = OffsetDir.None;
                // determine whether to offset inwards, outwards, or leave
                if (seg.IsExterior)
                {
                    if (seg.WallKind != WallKind.Curtain) offset = OffsetDir.Inward;
                }
                else
                {
                    // compare against an opposite room
                    if (seg.OppositeRoom != null)
                    {
                        if (seg.OppositeRoom.Score > ro.Score) offset = OffsetDir.Inward;
                        if (seg.OppositeRoom.Score < ro.Score) offset = OffsetDir.Outward;
                    }
                }
                

                // offset the segment:
                if (offset != OffsetDir.None) offsetSegment(seg, offset);
            }
        }

        private void cleanupRules(RoomObject room)
        {

            // we want to go around and see if there are closable gaps, based on a few different scenarios.

            double acceptableGap = 1.5 / 12.0;
            double collinearGap = 2.25; // allowable in the case of a gap to a collinear line (bigger than a column)

            for (int i=0; i<room.Boundaries.Count;i++)
            {
                
                double dist = -1;
                int closestEnd = -1;
                Segment current = room.Boundaries[i];

                System.Diagnostics.Debug.WriteLine("Segment: " + current.Counter);
                for (int e = 0; e < 2; e++)
                {
                    Segment nearest = getNearest(current, e, i, room.Boundaries, out dist, out closestEnd);

                
                    // determine if there is a condition we should attempt to close.

                    if (dist < acceptableGap) continue; // don't sweat.
                    double closeGap = nearest.Thickness * .75;

                    System.Diagnostics.Debug.WriteLine("==> Looking at cleaning up " + current.Counter+ " " + current.ElementType + " " + current.Element + "(end " + e + ") vs " + nearest.Counter + " " + nearest.ElementType + " " + nearest.Element + "(end " + closestEnd + ")");
                    System.Diagnostics.Debug.WriteLine("==> dist: " + dist);

                    if (dist <= closeGap)
                    {
                        // attempt to close gap
                        Segment created = closeTheGap(current, e, nearest, closestEnd);
                        if (created != null) room.Boundaries.Add(created);
                    }
                    else
                    {
                        if ((dist < collinearGap) && (current.Length >= collinearGap))
                        {
                            // close a collinear gap.
                            Segment created = closeIfCollinear(current, e, nearest, closestEnd);
                            if (created != null) room.Boundaries.Add(created);
                        }
                    }
                }
            }

        }

        private Segment closeIfCollinear(Segment s1, int end1, Segment s2, int end2)
        {
            Line ln1 = s1.Curve as Line;
            Line ln2 = s2.Curve as Line;

            if (ln1 == null) return null;
            if (ln2 == null) return null;

            double ang = ln1.Direction.AngleTo(ln2.Direction);
            if ((ang < 0.01)||(ang >Math.PI*0.9))
            {
                // it is collinear or close
            }
            else
            {
                return null;
            }

            // midpoint has changed, so we need to refigure:
            XYZ p1a = ln1.GetEndPoint(0);
            XYZ p1b = ln1.GetEndPoint(1);
            XYZ p2a = ln2.GetEndPoint(0);
            XYZ p2b = ln2.GetEndPoint(1);

            // need to make the lines unbound
            ln1 = Line.CreateUnbound(ln1.Origin, ln1.Direction);
            ln2 = Line.CreateUnbound(ln2.Origin, ln2.Direction);

           

            XYZ s1MidPoint = new XYZ((p1a.X + p1b.X)/ 2.0, (p1a.Y + p1b.Y) / 2.0, (p1a.Z + p1b.Z)/2.0);
            XYZ s2MidPoint = new XYZ((p2a.X + p2b.X)/ 2.0, (p2a.Y + p2b.Y) / 2.0, (p2a.Z + p2b.Z)/2.0);
            IntersectionResult result1 = ln1.Project(s2MidPoint);
            IntersectionResult result2 = ln2.Project(s1MidPoint);

            if ((result1.Distance < 0.02)&&(result2.Distance<0.02))
            {
                // collinear or close:

                Line stitch = Line.CreateBound(s1.Curve.GetEndPoint(end1), s2.Curve.GetEndPoint(end2));

                Segment fix = new Segment() { Curve = stitch, Draw = true, Length = stitch.Length };

                return fix;

            }

            return null;
        }

        private Segment closeTheGap( Segment s1, int end1, Segment s2, int end2)
        {
            // mixed cases probably too hard for now..
            if ((s1.Curve is Line) && (s2.Curve is Line))
            {
                Line ln1 = s1.Curve as Line;
                Line ln2 = s2.Curve as Line;

                IntersectionResultArray ira = null;
                SetComparisonResult result = ln1.Intersect(ln2, out ira);

                if (result == SetComparisonResult.Overlap)
                {
                    // we have an intersection
                    XYZ intersect = null;
                    int count = 0;
                    foreach( IntersectionResult ir in ira )
                    {
                        count++;
                        intersect = ir.XYZPoint;
                        if (count > 1) return null; // not sure of the scenario, but we'll pass.
                    }

                    // reset each line to the intersection point.
                    XYZ start1 = s1.Curve.GetEndPoint(getOtherEnd(end1));
                    XYZ start2 = s2.Curve.GetEndPoint(getOtherEnd(end2));

                    s1.Curve = Line.CreateBound(start1, intersect);
                    s2.Curve = Line.CreateBound(start2, intersect);
                }
                else
                {
                    // no overlap, try a different method:
                    XYZ p1 = null; XYZ p2 = null; bool isParallel = false;
                    GetClosestIntersectionPoints(ln1, ln2, out p1, out p2, out isParallel);

                    if (isParallel)
                    {
                        // we want to insert another segment between the two closest points.
                        Line newLine = Line.CreateBound(p1, p2);
                        Segment closer = new Segment()
                        {
                            Curve = newLine,
                            Draw = true,
                            IsExterior = false,
                            Length = newLine.ApproximateLength,
                            MidPoint = new XYZ((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0, (p1.Z + p2.Z) / 2.0),
                        };
                        return closer;
                    }
                    else
                    {
                        // in this case, we want to extend the two existing lines to the intersection point...
                        XYZ start1 = s1.Curve.GetEndPoint(getOtherEnd(end1));
                        XYZ start2 = s2.Curve.GetEndPoint(getOtherEnd(end2));

                        s1.Curve = Line.CreateBound(start1, p1);
                        s2.Curve = Line.CreateBound(start2, p1);
                    }

                }
               
            }
            return null;
        }

        private int getOtherEnd(int end)
        {
            if (end == 0) return 1;
           return 0;

           
        }

        /// <summary>
        /// Get the closest intersection points of unbound lines
        /// </summary>
        /// <param name="other"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="isParallel"></param>
        public void GetClosestIntersectionPoints(Line current, Line other, out XYZ p1, out XYZ p2, out bool isParallel)
        {
            // inspired by:
            // http://geomalgorithms.com/a07-_distance.html

            p1 = null; p2 = null;

            XYZ w0 = current.Origin.Subtract(other.Origin);
            double a = current.Direction.DotProduct(current.Direction);
            double b = current.Direction.DotProduct(other.Direction);
            double c = other.Direction.DotProduct(other.Direction);
            double d = current.Direction.DotProduct(w0);
            double e = other.Direction.DotProduct(w0);

            double denom = (a * c) - (b * b);

            if (denom == 0)
            {
                isParallel = true;
                // arbitrary points
                double tc = e / c;
                p1 = current.Origin;
                p2 = other.Origin.Add(other.Direction.Multiply(tc));
            }
            else
            {
                isParallel = false;
                double sc = ((b * e) - (c * d)) / denom;
                double tc = ((a * e) - (b * d)) / denom;

                p1 = current.Origin.Add(current.Direction.Multiply(sc));
                p2 = other.Origin.Add(other.Direction.Multiply(tc));
            }

        }

        private Segment getNearest(Segment seg, int end, int segIndex, IList<Segment> allSegments, out double minDist, out int closeEnd)
        {
            Segment nearest = null;
            closeEnd = -1;
            minDist = 99999;

            XYZ p1 = seg.Curve.GetEndPoint(end);

            for (int i=0; i<allSegments.Count;i++)
            {
                if (i == segIndex) continue;

                XYZ p2a = allSegments[i].Curve.GetEndPoint(0);
                XYZ p2b = allSegments[i].Curve.GetEndPoint(1);

                double dist = p1.DistanceTo(p2a);
                if (p1.DistanceTo(p2a) < minDist)
                {
                    nearest = allSegments[i];
                    minDist = dist;
                    closeEnd = 0;
                }
                dist = p1.DistanceTo(p2b);
                if ( dist< minDist)
                {
                    nearest = allSegments[i];
                    minDist = dist;
                    closeEnd = 1;
                }
                
            }

            return nearest;
        }

      

        private void offsetSegment(Segment seg, OffsetDir dir)
        {
            //simple version:
            XYZ vector = seg.OutsideRoomVector;
            if (dir == OffsetDir.Inward) vector = vector.Negate();

            // see online help for Curve.CreateOffset.
            // we don't specify the direction, we specify the up direction so that the curve is offset to the right.

            Transform t = seg.Curve.ComputeDerivatives(0.5, true);
            XYZ forward = t.BasisX;
            XYZ cross = vector.CrossProduct(forward).Normalize();

            // then use the cross product (presumably up or down) as the reference vector

            seg.Curve = seg.Curve.CreateOffset(seg.Thickness/2.0, cross);
        }
       

        private void log(String msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            _roomDoc.Application.WriteJournalComment(msg, false);
        }
        private void ignoreRoom(RoomObject ro)
        {
            // do not draw
            foreach (var segment in ro.Boundaries) segment.Draw = false;
        }
        #endregion

    }
}
