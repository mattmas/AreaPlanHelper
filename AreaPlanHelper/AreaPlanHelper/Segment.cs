using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace AreaShooter
{
    public class Segment
    {
        #region Accessor
        public RoomObject Parent { get; set; }
        public String ElementType { get; set; }
        public WallKind WallKind { get; set; }
        public Curve Curve { get; set; }
        public double Length { get; set; }
        public RoomObject OppositeRoom { get; set; }
        public XYZ MidPoint { get; set; }
        public XYZ OutsideRoomVector { get; set; }
        public double Thickness { get; set; }
        public ElementId Element { get; set; }
        public Boolean Draw { get; set; }
        public ModelCurve ModelCurve { get; set; }

        public bool IsExterior { get; set; }

        public int Counter { get; private set; }

        public static int _Count = 0;
        #endregion

        #region Constructor
        public Segment()
        {
            //set defaults;
            WallKind = WallKind.Unknown;
            Draw = true;
            IsExterior = false;
            _Count++;
            Counter = _Count;
        }
        #endregion
    }
}
