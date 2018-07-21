using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AreaShooter;

namespace AreaPlanHelper.UI
{
    public partial class RoomTypeForm : Form
    {
        public Config Configuration { get; set; }

        public RoomTypeForm(Config c)
        {
            InitializeComponent();
            Configuration = c;
            render();
        }

        private void render()
        {
            textBox1.Text = Configuration.Name;
            dataGridView1.DataSource = null;
            dataGridView1.DataSource = Configuration.RoomTypes;

        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    Configuration = Config.LoadFrom(openFileDialog1.FileName);
                    render();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            try
            {
                saveFileDialog1.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (saveFileDialog1.ShowDialog(this) == DialogResult.OK)
                {
                    Configuration.Save(saveFileDialog1.FileName);
                    MessageBox.Show("File Saved...");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
