using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AreaShooter.UI
{
    public partial class MainForm : Form
    {
        private Autodesk.Revit.DB.Document _currentDoc;
        private Controller _controller;
        private Config _config = Config.GetDefaults();
        private IList<RoomObject> _rooms;
        private IList<DocName> _docsWithRooms = new List<DocName>();
        private List<DocName> _docsToProcess = new List<DocName>();

        private IList<Config> _allConfigs = new List<Config>();

        public MainForm(Autodesk.Revit.DB.Document current)
        {
            InitializeComponent();
            tabControl1.Appearance = TabAppearance.FlatButtons; tabControl1.ItemSize = new Size(0, 1); tabControl1.SizeMode = TabSizeMode.Fixed;

            _currentDoc = current;
            initialRender();
        }

        private void initialRender()
        {
            this.Text = this.Text.Replace("<version>", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            List<DocName> docNames = new List<DocName>();
            var docs = Controller.GetLinksWithRooms(_currentDoc);
            foreach (var doc in docs) docNames.Add(new DocName() { Doc = doc, Name = doc.Title, IsCurrent = false });

            foreach (var doc in docNames) _docsWithRooms.Add(doc);

            if (Controller.HasRooms(_currentDoc)) docNames.Insert(0, new DocName() { Doc = _currentDoc, Name = _currentDoc.Title, IsCurrent = true });

            if (docNames.Count==0)
            {
                MessageBox.Show("There are no models with rooms to work with?");
                return;
            }
            cbModel.Items.AddRange(docNames.ToArray());
            cbModel.SelectedIndex = 0;

            configRender();
        }

        private void configRender()
        {
            try
            {
               _allConfigs = Config.LoadAll(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
                cbConfigs.Items.Clear();
                if (_allConfigs.Count == 0) _allConfigs.Add(Config.GetDefaults());
                cbConfigs.Items.AddRange(_allConfigs.ToArray());
                cbConfigs.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading configuration files: " + ex.Message);
            }
        }

        private void onModelChange(object sender, EventArgs e)
        {
            cbParameter.Items.Clear();
            var docName = cbModel.SelectedItem as DocName;

            cbParameter.Items.AddRange(Controller.GetRoomTextParams(docName.Doc).ToArray());

            cbParameter.SelectedIndex = 0;

            _docsToProcess.Clear();
            var archDoc = cbModel.SelectedItem as DocName;
            if (archDoc != null) _docsToProcess.Add(archDoc);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if ((cbModel.SelectedItem==null) && (_docsToProcess.Count==0))
            {
                MessageBox.Show("Please select a model!");
                return;
            }
            if (cbParameter.SelectedItem == null)
            {
                MessageBox.Show("Please select a room types parameter");
                return;
            }
            if (cbConfigs.SelectedItem == null)
            {
                MessageBox.Show("Please select a room type configuration file!");
                return;
            }

            if (retrieveAndRender())
            {
                tabControl1.SelectedTab = tabPage2;
            }
        }

        private bool retrieveAndRender()
        {
            try
            {

                List<Autodesk.Revit.DB.Document> docs = new List<Autodesk.Revit.DB.Document>();
                foreach (var doc in _docsToProcess) docs.Add(doc.Doc);

                _controller = new Controller(docs, _currentDoc, cbParameter.SelectedItem.ToString(), _config );

                Autodesk.Revit.DB.Parameter p = _currentDoc.ActiveView.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.VIEW_PHASE);
                if (p == null) throw new ApplicationException("No view phase info?");
                string phaseName = _currentDoc.GetElement(p.AsElementId()).Name;

                _rooms = _controller.RetrieveRooms(_currentDoc.ActiveView.GenLevel.Name, phaseName);

                // if this is only the current model, we can do more.

                if (docs[0].Title == _currentDoc.Title)
                {
                    int openCount = Controller.CountOpenSpots(_currentDoc, _currentDoc.ActiveView.GenLevel.Name, phaseName);
                    linkOpenSpots.Text = "NOTE: There are " + openCount + " open areas in your room model that do not have Room elements. PLEASE REVIEW!";
                    linkOpenSpots.Visible = (openCount > 0);
                }
                else
                {
                    linkOpenSpots.Text = "NOTE: please ensure that all possible rooms are created, including vertical penetration.";
                    linkOpenSpots.Visible = true;
                }
               

                IList<RoomObjectSummary> summaries = RoomObjectSummary.Summarize(_rooms, _config);
                dataGridView1.DataSource = null;
                dataGridView1.DataSource = summaries.ToArray();

                return true;
            }
            catch (ApplicationException aex)
            {
                MessageBox.Show(aex.Message);
            }
            catch (Exception ex)
            {
                var td = new Autodesk.Revit.UI.TaskDialog("Unexpected issue");
                td.MainContent = ex.GetType().Name + ": " + ex.Message;
                td.ExpandedContent = "Developer Info: " + Environment.NewLine + ex.StackTrace;
                td.Show();
            }
            return false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage1;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            try
            {
                //var archDoc = cbModel.SelectedItem as DocName;
                _controller.Create(_docsToProcess[0].Doc, _rooms, _currentDoc.ActiveView.GenLevel);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (ApplicationException aex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", aex.Message);
            }
            catch (Exception ex)
            {
                var td = new Autodesk.Revit.UI.TaskDialog("Unexpected Error");
                td.MainContent = ex.GetType().Name + ": " + ex.Message;
                td.ExpandedContent = "Developer Info: " + Environment.NewLine + ex.StackTrace;
                td.Show();
            }
        }

        private void btnPriorities_Click(object sender, EventArgs e)
        {
            AreaPlanHelper.UI.RoomTypeForm form = new AreaPlanHelper.UI.RoomTypeForm(_config);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                configRender();
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            _config = cbConfigs.SelectedItem as Config;
            if (_config == null) _config = Config.GetDefaults();

            AreaPlanHelper.UI.RoomTypeForm form = new AreaPlanHelper.UI.RoomTypeForm(_config);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _config = form.Configuration;
                cbConfigs.Items.Insert(0, _config);
                cbConfigs.SelectedIndex = 0;
            }
        }

        private void btnMulti_Click(object sender, EventArgs e)
        {
            DocName main = new DocName() { Doc = _currentDoc, IsCurrent = true, Name = _currentDoc.Title };
            AreaPlanHelper.UI.MultiSelModels multi = new AreaPlanHelper.UI.MultiSelModels(main, _docsWithRooms);
            cbModel.Enabled = false;  // can no longer select dropdown.

            if (multi.ShowDialog(this) == DialogResult.OK)
            {
                _docsToProcess.Clear();
                if (multi.Selected.Count == 1)
                {
                    cbModel.SelectedItem = multi.Selected[0];
                    _docsToProcess.Add(multi.Selected[0]);
                }
                if (multi.Selected.Count > 1)
                {
                    cbModel.Items.Clear();
                    cbModel.DataSource = null;
                    cbModel.DisplayMember = "";
                    cbModel.Items.Add("(multiple selected)");
                    _docsToProcess.AddRange(multi.Selected);
                }
                
            }
        }
    }

    public class DocName
    {
        public Autodesk.Revit.DB.Document Doc { get; set; }
        public String Name { get; set; }
        public Boolean IsCurrent { get; set; }
    }
}
