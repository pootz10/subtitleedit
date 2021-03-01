using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms.DCinema
{
    public partial class DCinemaFontBrowser : Form
    {

        private readonly string _fontsFolder;

        public string FontFileName { get; private set; }
        public string ShortFontFileName { get; private set; }
        public string FontName { get; private set; }

        public DCinemaFontBrowser()
        {
            InitializeComponent();

            labelPreview.Text = string.Empty;
            _fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void DCinemaFontBrowser_Shown(object sender, EventArgs e)
        {
            var fileNames = Directory.GetFiles(_fontsFolder, "*.ttf")
                .Select(Path.GetFileName)
                .OrderBy(p => p);

            listBoxFonts.Items.AddRange(fileNames.ToArray<object>());
        }

        private void listBoxFonts_SelectedIndexChanged(object sender, EventArgs e)
        {
            var idx = listBoxFonts.SelectedIndex;
            if (idx < 0)
            {
                return;
            }

            ShortFontFileName = listBoxFonts.Items[idx].ToString();
            var fileName = Path.Combine(_fontsFolder, ShortFontFileName);
            FontFileName = fileName;
            var privateFontCollection = new PrivateFontCollection();
            var fontBytes = FileUtil.ReadAllBytesShared(fileName);
            var handle = GCHandle.Alloc(fontBytes, GCHandleType.Pinned);
            var pointer = handle.AddrOfPinnedObject();
            try
            {
                privateFontCollection.AddMemoryFont(pointer, fontBytes.Length);
            }
            finally
            {
                handle.Free();
            }

            var fontFamily = privateFontCollection.Families.FirstOrDefault();
            if (fontFamily == null)
            {
                return;
            }

            labelPreview.Text = fontFamily.Name;
            FontName = fontFamily.Name;
            pictureBoxPreview.Image?.Dispose();
            pictureBoxPreview.Image = new Bitmap(pictureBoxPreview.Width, pictureBoxPreview.Height);
            using (var font = new Font(fontFamily, 25, FontStyle.Regular))
            using (var graphics = Graphics.FromImage(pictureBoxPreview.Image))
            {
                graphics.DrawString(fontFamily.Name + Environment.NewLine +
                                    Environment.NewLine +
                                    "The quick brown fox" + Environment.NewLine + 
                                    "0123456789",
                                    font, 
                                    Brushes.Orange, 
                                    12f, 
                                    23);
            }
            privateFontCollection.Dispose();
        }

        private void DCinemaFontBrowser_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
        }
    }
}
