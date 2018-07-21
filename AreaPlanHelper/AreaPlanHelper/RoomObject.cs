using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace AreaShooter
{
    public class RoomObject
    {
        #region Accessors
        public ElementId Id { get; set; }
        public String Name { get; set; }
        public String Number { get; set; }
        public IList<Segment> Boundaries { get; set; }
        public Double Score { get; set; }
        public XYZ Location { get; set; }
        public String RoomType { get; set; }
        public ElementId LevelId { get; set; }

        // the document that the room comes from.
        public Document Document { get; set; }
        #endregion

        #region constructor
        public RoomObject()
        {
            Boundaries = new List<Segment>();
        }
        #endregion
    }

    public class RoomObjectSummary
    {
        public string RoomType { get; set; }
        public int Count { get; set; }
        public Boolean Configured { get; set; }


        public static IList<RoomObjectSummary> Summarize(IList<RoomObject> rooms, Config config)
        {
            List<RoomObjectSummary> sums = new List<RoomObjectSummary>();

            foreach( var grp in rooms.GroupBy( r => r.RoomType.ToUpper()))
            {
                RoomObjectSummary sum = new RoomObjectSummary() { RoomType = grp.Key, Count = grp.Count() };

                var rtc = config.RoomTypes.FirstOrDefault(c => c.TypeName.ToUpper() == grp.Key);
                sum.Configured = (rtc != null);

                sums.Add(sum);
            }

            return sums;
        }
    }
        

}
