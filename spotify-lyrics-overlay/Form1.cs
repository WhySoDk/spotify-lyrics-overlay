using System.Text.RegularExpressions;

namespace spotify_lyrics_overlay
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            //populate combobox
            comboBoxFont.DrawItem += ComboBoxFonts_DrawItem;
            comboBoxFont.DataSource = System.Drawing.FontFamily.Families.ToList();
            comboBoxFont.DisplayMember = "Name";

            //try Helvetica
            int selectedIndex = comboBoxFont.Items.Cast<FontFamily>().ToList().FindIndex(f => f.Name == "Helvetica");

            //try Arial
            if (selectedIndex == -1)
                selectedIndex = comboBoxFont.Items.Cast<FontFamily>().ToList().FindIndex(f => f.Name == "Arial");

            //use whatever
            if (selectedIndex == -1 && comboBoxFont.Items.Count > 0)
                selectedIndex = 0;

            comboBoxFont.SelectedIndex = selectedIndex;

            fontSizeBox.Text = "27";

            //TODO: get main screen and get a w*h of the screen
        }

        //comboBoxFont thx2 https://stackoverflow.com/a/46038365
        private void ComboBoxFonts_DrawItem(object sender, DrawItemEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var fontFamily = (FontFamily)comboBox.Items[e.Index];
            var font = new Font(fontFamily, comboBox.Font.SizeInPoints);

            e.DrawBackground();
            e.Graphics.DrawString(font.Name, font, Brushes.Black, e.Bounds.X, e.Bounds.Y);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void comboBoxFont_SelectedIndexChanged(object sender, EventArgs e)
        {

            var comboBox = (ComboBox)sender;
            if (comboBox.SelectedItem is FontFamily selectedFont)
            {
                boldCheckBox.Enabled = selectedFont.IsStyleAvailable(FontStyle.Bold);
                italicCheckBox.Enabled = selectedFont.IsStyleAvailable(FontStyle.Italic);
            }
        }


        private void fontSizeBox_TextChanged(object sender, EventArgs e)
        {
            if (Int32.Parse(fontSizeBox.Text) > 1000)
            {
                fontSizeBox.Text = "1000";
            }
        }

        private void fontSizeBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '\b')
            {
                e.Handled = true;
            }
        }

        private void addFontSizeButton_Click(object sender, EventArgs e)
        {
            if (Int32.Parse(fontSizeBox.Text) < 1000)
            {
                int size = Int32.Parse(fontSizeBox.Text) + 1;
                fontSizeBox.Text = size.ToString();
            }
        }

        private void decreaseFontSizeButton_Click(object sender, EventArgs e)
        {
            if (Int32.Parse(fontSizeBox.Text) > 0)
            {
                int size = Int32.Parse(fontSizeBox.Text) - 1;
                fontSizeBox.Text = size.ToString();
            }

        }

        private void boldCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }
        private void italicCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }
        private void yOffset_ValueChanged(object sender, EventArgs e)
        {
            //TODO: calulate screen size and set a limit
        }
        private void xOffset_ValueChanged(object sender, EventArgs e)
        {
            //TODO: calulate screen size and set a limit
        }

        private void yText_Click(object sender, EventArgs e)
        {

        }

        private void apiRememberCheck_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}
