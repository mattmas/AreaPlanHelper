using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AreaShooter
{
    [Transaction(TransactionMode.Manual), Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document current = commandData.Application.ActiveUIDocument.Document;

                if (current.ActiveView.ViewType != ViewType.AreaPlan) throw new ApplicationException("Please run this command in an AreaPlan view!");

                UI.MainForm form = new UI.MainForm(current);
                if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return Result.Succeeded;
                }
                return Result.Cancelled;
            }
            catch (ApplicationException aex)
            {
                TaskDialog.Show("Error", aex.Message);
            }
            catch (Exception ex)
            {
                TaskDialog td = new TaskDialog("Unexpected Issue");
                td.MainContent = ex.GetType().Name + ":  " + ex.Message;
                td.ExpandedContent = "Developer Info: " + Environment.NewLine + ex.StackTrace;
                td.Show();

            }
            return Result.Failed;

        }
    }
}
