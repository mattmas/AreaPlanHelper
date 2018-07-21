using AreaShooter.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AreaPlanHelper.UI
{
    public partial class MultiSelModels : Form
    {
        public List<DocName> Selected { get; set; }
        public MultiSelModels(DocName main, IList<DocName> others)
        {
            InitializeComponent();

            
            // build the tree:
            TreeNode root = treeView1.Nodes.Add(main.Name);
            root.Tag = main;

            foreach( var other in others )
            {
                TreeNode link = root.Nodes.Add(other.Name);
                link.Tag = other;
            }
            root.Expand();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Selected = new List<DocName>();
            TreeNode root = treeView1.Nodes[0];

            if (root.Checked) Selected.Add(root.Tag as DocName);

            foreach( TreeNode link in root.Nodes )
            {
                if (link.Checked) Selected.Add(link.Tag as DocName);
            }

            if (Selected.Count==0)
            {
                MessageBox.Show("Please check some models to process!");
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
