using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace AreaShooter
{
    public class Controller
    {
        private string _phaseName { get; set; }
        private double _outsideRoomTestDist = 1.0;

        #region Accessors
        public Document CurrentDoc { get; set; }
        public List<Document> RoomDocs { get; set; }
        public bool SameModel { get; set; }
        public String TypeParam { get; private set; } 
        public Config Configuration { get; private set; }

        #endregion

        #region Constructors
        public Controller(List<Document> archDocs, Document current, string roomParam, Config c)
        {
            CurrentDoc = current;
            RoomDocs = archDocs;

            SameModel = false;
            foreach (var doc in RoomDocs) if (doc.Title == CurrentDoc.Title) SameModel = true;

            
            TypeParam = roomParam;
            Configuration = c;
            
        }
        #endregion

        #region PublicMethods
        public static IList<Document> GetLinksWithRooms(Document doc)
        {
            FilteredElementCollector coll = new FilteredElementCollector(doc);
            coll.OfClass(typeof(RevitLinkInstance));

            List<Document> linkDocs = new List<Document>();
            foreach(RevitLinkInstance e in coll.ToElements().Cast<RevitLinkInstance>().ToList())
            {
                Document d = e.GetLinkDocument();

                // might have some unloaded links...
                if (d != null)
                {
                    if (HasRooms(d)) linkDocs.Add(d);
                }
            }

            return linkDocs;
        }

        public static IList<String> GetRoomTextParams(Document doc)
        {
            FilteredElementCollector coll = new FilteredElementCollector(doc);
            coll.OfClass(typeof(SpatialElement));
            List<string> paramNames = new List<string>();

            foreach( var elem in coll.ToElements())
            {
                if (elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Rooms)
                {
                    foreach(var param in elem.GetOrderedParameters())
                    {
                        if (param.StorageType == StorageType.String) paramNames.Add(param.Definition.Name);
                    }
                    break;
                }
            }

            if (paramNames.Count == 0) throw new ApplicationException("No rooms found in model!");
            paramNames.Sort();

            return paramNames;
        }

        public static int CountOpenSpots(Document doc, string levelName, string phaseName )
        {
            Phase matchingPhase = null;
            foreach( Phase p in doc.Phases)
            {
                if (p.Name.ToUpper() == phaseName.ToUpper()) matchingPhase = p;
            }
            if (matchingPhase == null) throw new ApplicationException("There is no matching Phase " + phaseName + " in the room model?");

            // need an open transaction for this!
            if (doc.IsLinked) return -1;

            Transaction t = null;
            if (doc.IsModifiable == false)
            {
                t = new Transaction(doc, "GetPlanTopo");
                t.Start();
            }

            var pts = doc.get_PlanTopologies(matchingPhase);

            int missing = 0;
            foreach( PlanTopology pt in pts)
            {
                if (pt.Level.Name.ToUpper() == levelName.ToUpper())
                {
                   
                    foreach (PlanCircuit circuit in pt.Circuits)
                    {
                        if (!circuit.IsRoomLocated) missing++;
                    }
                }
            }

            if (t != null) t.RollBack();
            return missing;

        }

        public IList<RoomObject> RetrieveRooms(string levelName, string phaseName)
        {
            List<RoomObject> rooms = new List<RoomObject>();

            var roomElems = getRoomsOnAllDocLevels(RoomDocs, levelName, phaseName);
            if (roomElems.Count == 0) throw new ApplicationException("There are no rooms on level " + levelName + "/ phase " + phaseName + " in the room model?");

            _phaseName = phaseName; // store for future reference.

            foreach( var room in roomElems)
            {
                var ro = buildRoom(room);
                rooms.Add(ro);
            }

            return rooms;
        }

        public static bool HasRooms(Document doc)
        {
            FilteredElementCollector coll = new FilteredElementCollector(doc);
            coll.OfClass(typeof(SpatialElement));

            foreach( var elem in coll.ToElements())
            {
                if (elem is Autodesk.Revit.DB.Architecture.Room) return true;
            }

            return false;
        }

        public void Create(Document roomDoc, IList<RoomObject> rooms, Level refLevel)
        {
            Phase roomPhase = null;
            foreach (Phase p in roomDoc.Phases) if (p.Name.ToUpper() == _phaseName.ToUpper()) roomPhase = p;
            if (roomPhase == null) throw new ApplicationException("Phase info doesn't match?"); // not sure how.

            Transaction t = null;
            try
            {
                // temporary geometry
                GeometryHelper.Initialize(this.CurrentDoc);

                log("Updating Room Geometry");
                foreach (var room in rooms) updateGeometry(room, rooms, roomPhase, refLevel);

               

                t = new Transaction(CurrentDoc, "Shoot Areas");
                t.Start();

                log("Applying re-arrangement strategy");

               

                // update the room elements
                Analysis strategy = new Analysis();
                strategy.Analyze(roomDoc, roomPhase, rooms);

                log("Drawing  " + rooms.Count + " rooms.");

                drawRooms(rooms);

                t.Commit();
            }
            catch
            {
                if (t != null) t.RollBack();  // to be nicer in Revit.
                throw;
            }
        }

        #endregion

        #region PrivateMethods

        private void log(string msg)
        {
            CurrentDoc.Application.WriteJournalComment(msg, false);
        }
        private void drawRooms(IList<RoomObject> rooms)
        {
            var sp = CurrentDoc.ActiveView.SketchPlane;

            List<Tuple<XYZ, ModelCurve>> alreadyCreated = new List<Tuple<XYZ, ModelCurve>>();

            foreach( var room in rooms )
            {
                foreach( var segment in room.Boundaries )
                {
                    if (segment.Draw)
                    {
                        if (isAlreadyCreated(segment, alreadyCreated) == false)
                        {

                           var mc = CurrentDoc.Create.NewAreaBoundaryLine(sp, segment.Curve, CurrentDoc.ActiveView as ViewPlan);
                            segment.ModelCurve = mc;
                            alreadyCreated.Add(new Tuple<XYZ, ModelCurve>(segment.Curve.Evaluate(0.5, true), mc));
                        }
                        
                    }
                }

                Area a =
                CurrentDoc.Create.NewArea(CurrentDoc.ActiveView as ViewPlan, new UV(room.Location.X, room.Location.Y));
                a.Name = room.Name;
                a.Number = room.Number;

                // does the parameter exist?
                Parameter aCat = a.GetParameters(TypeParam).FirstOrDefault();

                if (aCat == null)
                {
                    // add the parameter to the model...
                    addParameterToAreas();

                    aCat = a.GetParameters(TypeParam).FirstOrDefault();
                }

                if (aCat != null)
                {
                    aCat.Set(room.RoomType);
                }
                
            }
        }

        private void addParameterToAreas()
        {

            try
            {
                CategorySet set = new CategorySet();
                Category area = CurrentDoc.Settings.Categories.get_Item(BuiltInCategory.OST_Areas);
                set.Insert(area);
                var binding = CurrentDoc.Application.Create.NewInstanceBinding(set);

                if (String.IsNullOrEmpty(CurrentDoc.Application.SharedParametersFilename) || (System.IO.File.Exists(CurrentDoc.Application.SharedParametersFilename) == false))
                {
                    // make our own file.
                    string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RevitSharedParameters.txt");
                    CurrentDoc.Application.SharedParametersFilename = file;
                    System.IO.File.WriteAllText(file, "");
                    
                }
                DefinitionFile defFile = CurrentDoc.Application.OpenSharedParameterFile();

                var group = defFile.Groups.get_Item("AreaRoomTypes");
                if (group == null) group = defFile.Groups.Create("AreaRoomTypes");

                ExternalDefinitionCreationOptions opts = new ExternalDefinitionCreationOptions(TypeParam, ParameterType.Text);
                Definition def = group.Definitions.Create(opts);
                CurrentDoc.ParameterBindings.Insert(def, binding);
            }
            catch (Exception ex)
            {
                log("Tried to add the " + TypeParam + " parameter to Areas, but failed! " + ex.GetType().Name + ": " + ex.Message);
            }
            
        }

        private bool isAlreadyCreated(Segment seg, IList<Tuple<XYZ,ModelCurve>> already)
        {
            //double midTolerance = 0.5 / 12.0;
            double endTolerance = 1.0 / 12.0;
            double endOffsetTolerance = 2.0 / 12.0; 

            XYZ mid = seg.Curve.Evaluate(0.5, true);
            Line segLine = seg.Curve as Line;
            if (segLine == null) return false;

            foreach( var item in already )
            {
                Curve crv = (item.Item2.GeometryCurve);
                if ((crv is Line) == false) continue;

                Line ln = crv as Line;
                IntersectionResult res = ln.Project(mid);

                if (res.Distance <= endTolerance)
                {
                    // it is a close projection.

                    // check angle
                    double angle = ln.Direction.AngleTo(segLine.Direction);
                    if ((angle<0.01)||(angle>Math.PI*0.95))
                    {
                        // collinear
                    }
                    else
                    {
                        continue;
                    }
                    // now we need to look at how close the endpoints are
                    XYZ end1 = segLine.GetEndPoint(0);
                    XYZ end2 = segLine.GetEndPoint(1);

                    IntersectionResult res1 = ln.Project(end1);
                    IntersectionResult res2 = ln.Project(end2);

                    if ((res1.Distance <= endOffsetTolerance) && (res2.Distance < endOffsetTolerance))
                    {
                        return true;
                    }
                }

             
            }

            return false;
        }
        private RoomObject buildRoom(Autodesk.Revit.DB.Architecture.Room r)
        {
            RoomObject ro = new RoomObject() { Id = r.Id, Name = r.Name, Number = r.Number, Location = (r.Location as LocationPoint).Point };
            ro.Document = r.Document;

            Parameter p = r.GetParameters(TypeParam).FirstOrDefault();
            if (p != null)
            {
                ro.RoomType = p.AsString();
                ro.LevelId = r.LevelId;

                //if (String.IsNullOrEmpty(ro.RoomType)) ro.RoomType = "<MISSING>";
                if (ro.RoomType == null) ro.RoomType = "";

                // lookup the score.
                RoomTypeConfig rtc = Configuration.RoomTypes.FirstOrDefault(c => c.TypeName.ToUpper().Trim() == ro.RoomType.ToUpper().Trim());
                if (rtc != null)
                {
                    ro.Score = rtc.Score;
                }
                else
                {
                    ro.Score = 999;
                }

            }
            else
            {
                ro.RoomType = "<missing>";
            }

            return ro;
        }

        private IList<Autodesk.Revit.DB.Architecture.Room> getRoomsOnAllDocLevels(IList<Document> docs, string levelName, string phase)
        {
            IList<Autodesk.Revit.DB.Architecture.Room> allRooms = new List<Autodesk.Revit.DB.Architecture.Room>();

            foreach( var doc in docs)
            {
                var rooms = getRoomsOnLevel(doc, levelName, phase);
                foreach (var room in rooms) allRooms.Add(room);

            }

            return allRooms;
        }
        /// <summary>
        /// get rooms on a given level/phase
        /// </summary>
        /// <param name="levelName"></param>
        /// <param name="phaseName"></param>
        /// <returns></returns>
        private IList<Autodesk.Revit.DB.Architecture.Room> getRoomsOnLevel(Document doc, string levelName, string phaseName)
        {
            List<Autodesk.Revit.DB.Architecture.Room> rooms = new List<Autodesk.Revit.DB.Architecture.Room>();
            FilteredElementCollector collLev = new FilteredElementCollector(doc);
            collLev.OfClass(typeof(Level));

            Level lev = collLev.Cast<Level>().FirstOrDefault(l => l.Name.ToUpper() == levelName.ToUpper());

            if (lev == null) throw new ApplicationException("There is no matching level name " + levelName + " in the room model?");

            FilteredElementCollector collRoom = new FilteredElementCollector(doc);
            collRoom.OfClass(typeof(SpatialElement));

            // see if there is a matching phase name
            Phase matchPhase = null;
            foreach( Phase phase in doc.Phases)
            {
                if (phase.Name.ToUpper() == phaseName.ToUpper()) matchPhase = phase; 
            }
            if (matchPhase == null) throw new ApplicationException("There is no matching phase " + phaseName + " in the room model?");

            foreach( var elem in collRoom.ToElements())
            {
                var room = elem as Autodesk.Revit.DB.Architecture.Room;
                if (room != null)
                {
                    

                    // filter out rooms that are unplaced or unconstrained!
                    if (room.Location == null) continue;
                    if (room.Area < 0.01) continue;
                    if (room.LevelId == ElementId.InvalidElementId) continue;

                    if (room.LevelId != lev.Id)
                    {
                        // check if the room is actually on a lower level but expanding above the current level.
                        Level roomLev = doc.GetElement(room.LevelId) as Level;
                        if (roomLev == null) continue;
                        if (roomLev.ProjectElevation > lev.ProjectElevation) continue;

                        if ((roomLev.ProjectElevation + room.UnboundedHeight)>lev.ProjectElevation)
                        {
                            // then we should include it.
                        }
                        else
                        {
                            continue;
                        }
                    }

                    // get the room
                    Parameter p = room.get_Parameter(BuiltInParameter.ROOM_PHASE);
                    if (p.AsElementId() == matchPhase.Id)
                    {
                        rooms.Add(room);
                    }
                }
            }

            return rooms;
        }

        private void updateGeometry(RoomObject ro, IList<RoomObject> allRooms, Phase roomPhase, Level refLevel)
        {
            // add all of the geometry info...

            Autodesk.Revit.DB.Architecture.Room r = ro.Document.GetElement(ro.Id) as Autodesk.Revit.DB.Architecture.Room;

            SpatialElementBoundaryOptions opts = new SpatialElementBoundaryOptions() { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center };

            var segmentList = r.GetBoundarySegments(opts);
            foreach ( BoundarySegment roomSeg in segmentList[0]) // outermost segment only!
            {
                
                Segment seg = new Segment() { Parent = ro, Element = roomSeg.ElementId };
                fetchSegmentElemInfo(ro, roomSeg, refLevel, seg);
                ro.Boundaries.Add(seg);
               
            }

            calculateOppositeRooms(ro, allRooms, refLevel, roomPhase);

        }

        private void fetchSegmentElemInfo(RoomObject obj, BoundarySegment seg, Level refLevel, Segment target)
        {

            Curve crv = seg.GetCurve();
            target.Curve = crv;
            target.Length = crv.ApproximateLength;
            target.MidPoint = crv.Evaluate(0.5, true);

            Element e = obj.Document.GetElement(seg.ElementId);
            if (e is RevitLinkInstance)
            {
                RevitLinkInstance inst = e as RevitLinkInstance;
                Document doc = inst.GetLinkDocument();
                e = null;
                if (doc != null) e = doc.GetElement(seg.LinkElementId);
                
            }
            
            if (e == null)
            {
                target.ElementType = "UNKNOWN!?";
                target.Thickness = 0;
                return;
            }
            
                target.ElementType = e.Category.Name;
                if (e is Wall)
                {
                    Wall w = e as Wall;
                    target.Thickness = w.WallType.Width;
                    target.IsExterior = (w.WallType.Function == WallFunction.Exterior);
                    target.WallKind = w.WallType.Kind;

                }
                else
                {
                    target.Thickness = 0;

                }

            
        }

        private void calculateOppositeRooms(RoomObject ro, IList<RoomObject> allRooms, Level refLevel, Phase targetPhase)
        {
            foreach (var seg in ro.Boundaries)
            {
                Transform t = seg.Curve.ComputeDerivatives(0.5, true);

                XYZ alongDir = t.BasisX.Normalize();
                XYZ normalDir = XYZ.BasisZ.CrossProduct(alongDir).Normalize();
               
                seg.OutsideRoomVector = normalDir;

                // what room is outside?
                XYZ testPoint1 = t.Origin.Add(normalDir.Multiply(_outsideRoomTestDist));
                XYZ testPoint2 = t.Origin.Add(normalDir.Negate().Multiply(_outsideRoomTestDist));
                XYZ origin = t.Origin;
                if (ro.LevelId != refLevel.Id) origin = new XYZ(t.Origin.X, t.Origin.Y, t.Origin.Z);
                

                testPoint1 = new XYZ(testPoint1.X, testPoint1.Y, refLevel.ProjectElevation + 1.0); // one foot off the floor, just for safety...
                testPoint2 = new XYZ(testPoint2.X, testPoint2.Y, refLevel.ProjectElevation + 1.0);

                List<Document> all = new List<Document>(RoomDocs);
                all.Remove(ro.Document);
                all.Insert(0, ro.Document);

                // find the opposite room...
                Autodesk.Revit.DB.Architecture.Room opposite = null;

                foreach (Document testDoc in all)
                {

                    var revitRoom1 = testDoc.GetRoomAtPoint(testPoint1, targetPhase);
                    var revitRoom2 = testDoc.GetRoomAtPoint(testPoint2, targetPhase);

                    
                    if ((revitRoom1 != null) && (revitRoom2 != null))
                    {
                        if ((revitRoom1.Id != ro.Id)) opposite = revitRoom1;
                        if ((revitRoom2.Id != ro.Id))
                        {
                            opposite = revitRoom2;
                            seg.OutsideRoomVector = normalDir.Negate();
                            testPoint1 = testPoint2; // reset
                        }
                    }
                    else
                    {
                        if (revitRoom1 != null)
                        {
                            seg.OutsideRoomVector = normalDir.Negate();
                            testPoint1 = testPoint2;
                        }
                        if (revitRoom2 != null)
                        {
                            // do nothing.
                        }

                    }
                    if (opposite != null) break;
                }


                // Turn on for debugging line sides from rooms.
                //GeometryHelper.DrawLine(origin, testPoint1);

                if (opposite != null)
                {
                    // see if we can find it in the room list.
                    var match = allRooms.FirstOrDefault(r => r.Id == opposite.Id);
                    if (match == null)
                    {
                        log("NOTE: Found a matching room for a segment in " + ro.Number + " that points to Room Num: " + opposite.Number + "(" + opposite.Id + ") but we don't have that in our list!?!");
                    }
                    else
                    {
                        seg.OppositeRoom = match;
                    }
                }


            }
        }
        #endregion
    }
}
