using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriPaletteSet
{
    public partial class CheckGroupBox : System.Windows.Forms.GroupBox
    {
        public CheckGroupBox() { }
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            var checkBox = e.Control as CheckBox;
            if (checkBox != null) checkBox.Click += checkBox_Click;
        }

        void checkBox_Click(object sender, EventArgs e)
        {
            var checkBox = (CheckBox)sender;
            if (!checkBox.Checked) checkBox.Checked = true;
        }
    }
}
