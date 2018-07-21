using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AreaShooter
{
    public class Config
    {
        public String Name { get; set; }
        public List<RoomTypeConfig> RoomTypes { get; set; }

        public Config()
        {
            RoomTypes = new List<RoomTypeConfig>();
        }

        public static Config GetDefaults()
        {
            Config c = new Config();
            c.Name = "BOMA";
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

        public static Config LoadFrom(string filePath)
        {
            JavaScriptSerializer deserialize = new JavaScriptSerializer();
            Config c = deserialize.Deserialize<Config>(System.IO.File.ReadAllText(filePath));

            return c;
        }

        public static IList<Config> LoadAll(string folder)
        {
            List<Config> all = new List<Config>();

            string[] files = System.IO.Directory.GetFiles(folder, "*.rts");
            foreach( string file in files )
            {
                try
                {
                    Config c = Config.LoadFrom(file);
                    all.Add(c);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Error reading config file: " + file + " - " + ex.GetType().Name  +":" + ex.Message);
                }
                
            }

            return all.OrderBy(c => c.Name).ToList();

        }

        public void Save(string filePath)
        {
            JavaScriptSerializer serialize = new JavaScriptSerializer();
            string contents = serialize.Serialize(this);
            System.IO.File.WriteAllText(filePath, contents);
        }
    }
    public class RoomTypeConfig
    {
        public String TypeName { get; set; }
        public double Score { get; set; }

    }

    
}
