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
                if (room.Name.ToUpper().Contains("CORR"))
                {
                    ignoreRoom(room);
                }
                else
                {
                    

                    // adjust lines by rules
                    adjustByRules(room);
                }
            }

        }
        #endregion

        #region PrivateMethods
        private void adjustByRules(RoomObject ro)
        {
            foreach( var seg in ro.Boundaries )
            {
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
