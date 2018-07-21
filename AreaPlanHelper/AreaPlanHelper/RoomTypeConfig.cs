using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaShooter
{
    public class Config
    {
        public IList<RoomTypeConfig> RoomTypes { get; set; }

        public Config()
        {
            RoomTypes = new List<RoomTypeConfig>();
        }

        public static Config GetDefaults()
        {
            Config c = new Config();
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "MVP", Score = 7 });
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "BSA", Score = 6 });
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "PKG", Score = 5 });
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "FSA", Score = 4 });
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "BBC", Score = 3 });
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "OSA", Score = 2 });
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "OA", Score = 2 });
            c.RoomTypes.Add(new RoomTypeConfig() { TypeName = "BAA", Score = 2 });

            return c;
        }
    }
    public class RoomTypeConfig
    {
        public String TypeName { get; set; }
        public double Score { get; set; }

    }

    
}
