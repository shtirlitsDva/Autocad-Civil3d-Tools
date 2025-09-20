using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IntersectUtilities.Forms
{
    public partial class TestSuiteForm : Form
    {
        public TestSuiteForm()
        {
            this.AutoScroll = true;
            InitializeForms();
        }

        private void InitializeForms()
        {
            int formWidth = 300;
            int formHeight = 300;
            int spacing = 10;
            int x = spacing;

            for (int i = 1; i <= 8; i++)
            {
                var strings = this.GenerateRandomStrings(i, 5, 12);
                var stringGridForm = new StringGridForm(strings)
                {
                    Width = formWidth,
                    Height = formHeight,
                    TopLevel = false,
                    Visible = true
                };

                stringGridForm.Location = new Point(x, spacing);
                x += formWidth + spacing;

                this.Controls.Add(stringGridForm);
            }
        }

        private string GenerateRandomString(int length)
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var stringChars = new char[length];
            for (int i = 0; i < length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }
            return new string(stringChars);
        }

        private IEnumerable<string> GenerateRandomStrings(int count, int minLength, int maxLength)
        {
            var random = new Random();
            var strings = new List<string>();
            for (int i = 0; i < count; i++)
            {
                int length = random.Next(minLength, maxLength + 1);
                strings.Add(GenerateRandomString(length));
            }
            return strings;
        }
    }
}
