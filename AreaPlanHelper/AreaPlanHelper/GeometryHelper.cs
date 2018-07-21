using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaShooter
{
    public static class GeometryHelper
    {
        private static Document _doc;

        public static void Initialize(Document doc)
        {
            _doc = doc;
        }

        public static void DrawLine(XYZ p1, XYZ p2)
        {
            Transaction t = null;
            if (_doc.IsModifiable == false)
            {
                t = new Transaction(_doc, "Draw Line");
                t.Start();
            }

            Line ln = Line.CreateBound(p1, p2);
            if (p1.DistanceTo(p2) < 1.0 / 32.0) return; // too small!

            XYZ v1 = p2.Subtract(p1).Normalize();
            XYZ other = XYZ.BasisX;
            double ang = (v1.AngleTo(other));
            if ((ang > 0.9 * Math.PI) || (ang < 0.1)) other = XYZ.BasisY;
            XYZ norm = v1.CrossProduct(other).Normalize();
            Plane p = Plane.CreateByNormalAndOrigin(norm, p1);
            SketchPlane sp = SketchPlane.Create(_doc, p);

            _doc.Create.NewModelCurve(ln, sp);

            if (t != null) t.Commit();
        }
    }
}
