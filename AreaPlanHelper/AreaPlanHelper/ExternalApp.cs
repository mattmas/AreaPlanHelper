using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AreaPlanHelper
{
    public class ExternalApp : IExternalApplication
    {
        private static bool _Started = false;
        private static UIControlledApplication _App;

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _App = application;
                buildUI(application);
                return Result.Succeeded;
            }
            catch (Exception eX)
            {
                TaskDialog td = new TaskDialog("Error in Setup");
                td.ExpandedContent = eX.GetType().Name + ": " + eX.Message + Environment.NewLine + eX.StackTrace;
                td.Show();
                return Result.Failed;
            }
        }


        public static string GetUserInfo()
        {
            // make a reasonably unique identifier - but pretty anonymous. This is for analytics tracking.
            return (Environment.UserDomainName + "\\" + Environment.UserName).GetHashCode().ToString();
        }

        public static void FirstTimeRun()
        {
            if (_Started) return;
            //otherwise, record the fact that we started, for analytics purposes.
            startup();
        }

        private static void startup()
        {
            _App.ControlledApplication.WriteJournalComment("Starting up Revit AreaPlan Helper ...", false);
            _Started = true;
        }

        public static bool AnalyticsOptIn()
        {

            //NOTE: We do collect anonymized analytics with our official binary build (# of times each feature was launched).
            // we do this just to have a sense of whether anyone is using this application.
            // if you would still like to opt out of this, please create an "optout.txt" file in Metamorphosis folder.
            // if you have concerns about the analytics, and would like to see the analytical information we collect, please reach out to:
            // mmason (at) rand.com

            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string optOutFile = System.IO.Path.Combine(path, "optout.txt");

            return !System.IO.File.Exists(optOutFile);
        }

        private void buildUI(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel(Tab.AddIns, "AreaPlan" + Environment.NewLine + "Helper");

            var plan = new PushButtonData("PlanHelper", "AreaPlan Helper", System.Reflection.Assembly.GetExecutingAssembly().Location, "AreaShooter.Command");
            plan.ToolTip = "Help build an area plan from rooms";
            plan.LongDescription = "Using rooms either in the current model or a linked model to help create areas to meet your BOMA or other standard. ";
            plan.LargeImage = getImage("AreaPlanHelper.Images.areaicon32.png");
            plan.Image = getImage("AreaPlanHelper.Images.areaicon16.png");


            panel.AddItem(plan);
        }

        private System.Windows.Media.ImageSource getImage(string imageFile)
        {
            try
            {
                System.IO.Stream stream = this.GetType().Assembly.GetManifestResourceStream(imageFile);
                if (stream == null) return null;
                PngBitmapDecoder pngDecoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                return pngDecoder.Frames[0];

            }
            catch
            {
                return null; // no image


            }
        }
    }
}
