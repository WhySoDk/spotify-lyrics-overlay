using System.Diagnostics;
using System.Text.RegularExpressions;

namespace spotify_lyrics_overlay
{
    public partial class Form1 : Form
    {
        Boolean isStarted = false;
        private bool isInitializing = true;
        private LyricsOverlay overlay;

        public Form1()
        {

            var config = ConfigManager.Instance.LoadConfig();

            InitializeComponent();

            //populate font combobox
            comboBoxFont.DrawItem += ComboBoxFonts_DrawItem;
            comboBoxFont.DataSource = System.Drawing.FontFamily.Families.ToList();
            comboBoxFont.DisplayMember = "Name";

            //populate monitor combobox
            monitorComboBox.DrawItem += monitorComboBox_DrawItem;
            monitorComboBox.DataSource = Screen.AllScreens.ToList();
            monitorComboBox.DisplayMember = "DeviceName";


            //innitialize default values
            if (config.newlyGenerated == true)
            {
                //font
                var selectedFont = comboBoxFont.Items.Cast<FontFamily>()
                    .FirstOrDefault(f => f.Name.Equals("arial", StringComparison.OrdinalIgnoreCase));

                if (selectedFont != null)
                    comboBoxFont.SelectedItem = selectedFont;

                //fontsize
                fontSizeBox.Text = "27";

                //stlye
                boldCheckBox.Checked = false;
                italicCheckBox.Checked = false;
                dropShadowCheckBox.Checked = false;

                //monitor
                monitorComboBox.SelectedIndex = Screen.AllScreens.ToList().FindIndex(s => s == Screen.PrimaryScreen);

                //x&y offset
                xOffset.Value = 0;
                yOffset.Value = 0;

                //color
                colorPictureBox.BackColor = ColorTranslator.FromHtml("#f3ce32");
                colorHexBox.Text = "#f3ce32";

                //key
                apiKeyBox.Text = "";
                apiRememberCheck.Checked = false;
            }
            else
            {
                //font
                var selectedFont = comboBoxFont.Items.Cast<FontFamily>()
                    .FirstOrDefault(f => f.Name.Equals(config.fontName, StringComparison.OrdinalIgnoreCase));

                if (selectedFont != null)
                    comboBoxFont.SelectedItem = selectedFont;

                //fontsize
                fontSizeBox.Text = config.fontSize.ToString();

                //style
                boldCheckBox.Checked = config.bold;
                italicCheckBox.Checked = config.italic;
                dropShadowCheckBox.Checked = config.dropShadow;

                //monitor
                int monitorIndex = Screen.AllScreens.ToList().FindIndex(s => s.DeviceName == config.screenName);
                if (monitorIndex != -1)
                    monitorComboBox.SelectedIndex = monitorIndex;

                //x&y offset
                xOffset.Value = config.xOffset;
                yOffset.Value = config.yOffset;

                //color
                colorPictureBox.BackColor = ColorTranslator.FromHtml(config.fontColorHex);
                colorHexBox.Text = config.fontColorHex;

                //key
                apiKeyBox.Text = config.apiKey;
                apiRememberCheck.Checked = config.rememberApiKey;
            }


            var (minX, maxX, minY, maxY) = GetOffsetLimits(Screen.AllScreens[monitorComboBox.SelectedIndex]);
            xOffset.Minimum = minX;
            xOffset.Maximum = maxX;
            yOffset.Minimum = minY;
            yOffset.Maximum = maxY;

            isInitializing = false;
            updateConfig();
            //overlay = new LyricsOverlay(() => isStarted);
            //overlay.Show();
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

        private void monitorComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {

            var comboBox = (ComboBox)sender;
            var screen = (Screen)comboBox.Items[e.Index];
            string monitorInfo = $"{screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";

            e.DrawBackground();
            e.Graphics.DrawString(monitorInfo, comboBox.Font, Brushes.Black, e.Bounds.X, e.Bounds.Y);
        }

        private (int minX, int maxX, int minY, int maxY) GetOffsetLimits(Screen screen)
        {
            var bounds = screen.Bounds;
            int halfWidth = bounds.Width / 2;
            int halfHeight = bounds.Height / 2;

            return (-halfWidth, halfWidth, -halfHeight, halfHeight);
        }

        private void monitorComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var (minX, maxX, minY, maxY) = GetOffsetLimits(Screen.AllScreens[monitorComboBox.SelectedIndex]);
            xOffset.Minimum = minX;
            xOffset.Maximum = maxX;
            yOffset.Minimum = minY;
            yOffset.Maximum = maxY;
            updateConfig();
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
            updateConfig();
        }


        private void fontSizeBox_TextChanged(object sender, EventArgs e)
        {
            if (Int32.Parse(fontSizeBox.Text) > 1000)
            {
                fontSizeBox.Text = "1000";
            }
            updateConfig();
        }

        private void fontSizeBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '\b')
            {
                e.Handled = true;
            }
            updateConfig();
        }

        private void addFontSizeButton_Click(object sender, EventArgs e)
        {
            if (Int32.Parse(fontSizeBox.Text) < 1000)
            {
                int size = Int32.Parse(fontSizeBox.Text) + 1;
                fontSizeBox.Text = size.ToString();
            }
            updateConfig();
        }

        private void decreaseFontSizeButton_Click(object sender, EventArgs e)
        {
            if (Int32.Parse(fontSizeBox.Text) > 0)
            {
                int size = Int32.Parse(fontSizeBox.Text) - 1;
                fontSizeBox.Text = size.ToString();
            }
            updateConfig();
        }

        private void colorHexBox_stopFocus(object sender, EventArgs e)
        {
            string inputValue = colorHexBox.Text;

            if (!isValidHexColor(inputValue))
            {
                colorHexBox.Text = "#f3ce32";
            }

            colorPictureBox.BackColor = ColorTranslator.FromHtml(colorHexBox.Text);
        }

        //validate hex color format
        private bool isValidHexColor(string value)
        {
            return Regex.IsMatch(value, @"^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$");
        }

        private void colorPickerButton_Click(object sender, EventArgs e)
        {
            var res = colorDialog.ShowDialog();
            if (res == DialogResult.OK)
            {
                colorPictureBox.BackColor = colorDialog.Color;
            }
        }

        private void colorPictureBox_BackColorChanged(object sender, EventArgs e)
        {
            colorHexBox.Text = ColorTranslator.ToHtml(colorPictureBox.BackColor);
            updateConfig();
        }

        private void boldCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            updateConfig();
        }
        private void italicCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            updateConfig();
        }
        private void dropShadowCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            updateConfig();
        }

        private void yOffset_ValueChanged(object sender, EventArgs e)
        {
            updateConfig();
        }
        private void xOffset_ValueChanged(object sender, EventArgs e)
        {
            updateConfig();
        }

        private void apiRememberCheck_CheckedChanged(object sender, EventArgs e)
        {
            updateConfig();
        }

        private void apiKeyBox_TextChanged(object sender, EventArgs e)
        {
            updateConfig();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            updateConfig();
            ConfigManager.Instance.SaveConfig();

        }

        private void updateConfig()
        {
            if (isInitializing) return;

            var config = ConfigManager.Instance.LoadConfig();

            config.newlyGenerated = false;

            if (comboBoxFont.SelectedItem is FontFamily selectedFont)
            {
                config.fontName = selectedFont.Name;
            }

            if (int.TryParse(fontSizeBox.Text, out int parsedSize))
            {
                config.fontSize = parsedSize;
            }

            config.bold = boldCheckBox.Checked;
            config.italic = italicCheckBox.Checked;
            config.dropShadow = dropShadowCheckBox.Checked;

            int monitorIndex = monitorComboBox.SelectedIndex;
            if (monitorIndex >= 0 && monitorIndex < Screen.AllScreens.Length)
            {
                config.screenName = Screen.AllScreens[monitorIndex].DeviceName;
            }

            config.xOffset = (int)xOffset.Value;
            config.yOffset = (int)yOffset.Value;

            config.fontColorHex = colorHexBox.Text;

            config.rememberApiKey = apiRememberCheck.Checked;
            config.apiKey = apiRememberCheck.Checked ? apiKeyBox.Text : "";
        }

        private void runButton_Click(object sender, EventArgs e)
        {
            isStarted = !isStarted;
            if (isStarted)
            {
                runButton.Text = "Stop";
                runButton.BackColor = Color.DarkRed;

                if (overlay == null)
                {
                    overlay = new LyricsOverlay(() => isStarted);
                    overlay.Show();
                }

            }
            else
            {
                runButton.Text = "Start";
                runButton.BackColor = Color.Green;
            }
        }
    }
}
