namespace spotify_lyrics_overlay
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            fontText = new Label();
            comboBoxFont = new ComboBox();
            boldCheckBox = new CheckBox();
            italicCheckBox = new CheckBox();
            fontSizeBox = new TextBox();
            decreaseFontSizeButton = new Button();
            addFontSizeButton = new Button();
            xOffset = new NumericUpDown();
            xText = new Label();
            yText = new Label();
            yOffset = new NumericUpDown();
            apiKeyText = new Label();
            apiKeyBox = new TextBox();
            apiRememberCheck = new CheckBox();
            monitorComboBox = new ComboBox();
            monitorText = new Label();
            runButton = new Button();
            colorDialog = new ColorDialog();
            colorPictureBox = new PictureBox();
            colorText = new Label();
            colorHexBox = new TextBox();
            colorPickerButton = new Button();
            dropShadowCheckBox = new CheckBox();
            backgroundCheckBox = new CheckBox();
            backgroundColorPictureBox = new PictureBox();
            backgroundColorHexBox = new TextBox();
            backgroundColorPickerButton = new Button();
            backgroundSpreadText = new Label();
            backgroundSpread = new NumericUpDown();
            backgroundOpacityText = new Label();
            backgroundOpacity = new TrackBar();
            backgroundOpacityValue = new Label();
            transitionText = new Label();
            transitionComboBox = new ComboBox();
            ((System.ComponentModel.ISupportInitialize)xOffset).BeginInit();
            ((System.ComponentModel.ISupportInitialize)yOffset).BeginInit();
            ((System.ComponentModel.ISupportInitialize)colorPictureBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)backgroundColorPictureBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)backgroundSpread).BeginInit();
            ((System.ComponentModel.ISupportInitialize)backgroundOpacity).BeginInit();
            SuspendLayout();
            // 
            // fontText
            // 
            fontText.AutoSize = true;
            fontText.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            fontText.ForeColor = SystemColors.ControlLight;
            fontText.Location = new Point(9, 15);
            fontText.Name = "fontText";
            fontText.Size = new Size(44, 21);
            fontText.TabIndex = 0;
            fontText.Text = "Font";
            fontText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // comboBoxFont
            // 
            comboBoxFont.BackColor = Color.White;
            comboBoxFont.DrawMode = DrawMode.OwnerDrawFixed;
            comboBoxFont.Font = new Font("Segoe UI", 12F);
            comboBoxFont.ForeColor = SystemColors.ControlText;
            comboBoxFont.FormattingEnabled = true;
            comboBoxFont.Location = new Point(12, 35);
            comboBoxFont.Name = "comboBoxFont";
            comboBoxFont.Size = new Size(166, 30);
            comboBoxFont.TabIndex = 1;
            comboBoxFont.SelectedIndexChanged += comboBoxFont_SelectedIndexChanged;
            // 
            // boldCheckBox
            // 
            boldCheckBox.AutoSize = true;
            boldCheckBox.Font = new Font("Candara", 12F);
            boldCheckBox.ForeColor = SystemColors.ControlLight;
            boldCheckBox.Location = new Point(8, 65);
            boldCheckBox.Name = "boldCheckBox";
            boldCheckBox.RightToLeft = RightToLeft.Yes;
            boldCheckBox.Size = new Size(59, 23);
            boldCheckBox.TabIndex = 7;
            boldCheckBox.Text = "Bold";
            boldCheckBox.UseVisualStyleBackColor = true;
            boldCheckBox.CheckedChanged += boldCheckBox_CheckedChanged;
            // 
            // italicCheckBox
            // 
            italicCheckBox.AutoSize = true;
            italicCheckBox.Font = new Font("Candara", 12F);
            italicCheckBox.ForeColor = SystemColors.ControlLight;
            italicCheckBox.Location = new Point(80, 65);
            italicCheckBox.Name = "italicCheckBox";
            italicCheckBox.RightToLeft = RightToLeft.Yes;
            italicCheckBox.Size = new Size(60, 23);
            italicCheckBox.TabIndex = 8;
            italicCheckBox.Text = "Italic";
            italicCheckBox.UseVisualStyleBackColor = true;
            italicCheckBox.CheckedChanged += italicCheckBox_CheckedChanged;
            // 
            // fontSizeBox
            // 
            fontSizeBox.Font = new Font("Segoe UI", 12F);
            fontSizeBox.ForeColor = SystemColors.ControlText;
            fontSizeBox.Location = new Point(219, 34);
            fontSizeBox.Name = "fontSizeBox";
            fontSizeBox.Size = new Size(53, 29);
            fontSizeBox.TabIndex = 3;
            fontSizeBox.TextAlign = HorizontalAlignment.Center;
            fontSizeBox.TextChanged += fontSizeBox_TextChanged;
            fontSizeBox.KeyPress += fontSizeBox_KeyPress;
            // 
            // decreaseFontSizeButton
            // 
            decreaseFontSizeButton.ForeColor = SystemColors.ControlText;
            decreaseFontSizeButton.Location = new Point(184, 35);
            decreaseFontSizeButton.Name = "decreaseFontSizeButton";
            decreaseFontSizeButton.Size = new Size(29, 29);
            decreaseFontSizeButton.TabIndex = 4;
            decreaseFontSizeButton.Text = "-";
            decreaseFontSizeButton.UseVisualStyleBackColor = true;
            decreaseFontSizeButton.Click += decreaseFontSizeButton_Click;
            // 
            // addFontSizeButton
            // 
            addFontSizeButton.ForeColor = SystemColors.ControlText;
            addFontSizeButton.Location = new Point(278, 35);
            addFontSizeButton.Name = "addFontSizeButton";
            addFontSizeButton.Size = new Size(29, 29);
            addFontSizeButton.TabIndex = 2;
            addFontSizeButton.Text = "+";
            addFontSizeButton.UseVisualStyleBackColor = true;
            addFontSizeButton.Click += addFontSizeButton_Click;
            // 
            // xOffset
            // 
            xOffset.Font = new Font("Segoe UI", 12F);
            xOffset.ForeColor = SystemColors.ControlText;
            xOffset.Location = new Point(12, 226);
            xOffset.Name = "xOffset";
            xOffset.Size = new Size(146, 29);
            xOffset.TabIndex = 9;
            xOffset.ValueChanged += xOffset_ValueChanged;
            // 
            // xText
            // 
            xText.AutoSize = true;
            xText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            xText.ForeColor = SystemColors.ControlLight;
            xText.Location = new Point(9, 204);
            xText.Name = "xText";
            xText.Size = new Size(102, 21);
            xText.TabIndex = 10;
            xText.Text = "X axis offset";
            xText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // yText
            // 
            yText.AutoSize = true;
            yText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            yText.ForeColor = SystemColors.ControlLight;
            yText.Location = new Point(159, 204);
            yText.Name = "yText";
            yText.RightToLeft = RightToLeft.Yes;
            yText.Size = new Size(102, 21);
            yText.TabIndex = 12;
            yText.Text = "Y axis offset";
            yText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // yOffset
            // 
            yOffset.Font = new Font("Segoe UI", 12F);
            yOffset.ForeColor = SystemColors.ControlText;
            yOffset.Location = new Point(164, 226);
            yOffset.Name = "yOffset";
            yOffset.Size = new Size(143, 29);
            yOffset.TabIndex = 11;
            yOffset.ValueChanged += yOffset_ValueChanged;
            // 
            // apiKeyText
            // 
            apiKeyText.AutoSize = true;
            apiKeyText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            apiKeyText.ForeColor = SystemColors.ControlLight;
            apiKeyText.Location = new Point(8, 422);
            apiKeyText.Name = "apiKeyText";
            apiKeyText.Size = new Size(133, 21);
            apiKeyText.TabIndex = 13;
            apiKeyText.Text = "Spotify Client Id";
            apiKeyText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // apiKeyBox
            // 
            apiKeyBox.Font = new Font("Segoe UI", 12F);
            apiKeyBox.ForeColor = SystemColors.ControlText;
            apiKeyBox.Location = new Point(12, 444);
            apiKeyBox.Name = "apiKeyBox";
            apiKeyBox.PasswordChar = '•';
            apiKeyBox.Size = new Size(295, 29);
            apiKeyBox.TabIndex = 14;
            apiKeyBox.UseSystemPasswordChar = true;
            apiKeyBox.TextChanged += apiKeyBox_TextChanged;
            // 
            // apiRememberCheck
            // 
            apiRememberCheck.AutoSize = true;
            apiRememberCheck.Font = new Font("Segoe UI", 12F);
            apiRememberCheck.ForeColor = SystemColors.ControlLight;
            apiRememberCheck.Location = new Point(7, 471);
            apiRememberCheck.Name = "apiRememberCheck";
            apiRememberCheck.RightToLeft = RightToLeft.Yes;
            apiRememberCheck.Size = new Size(167, 25);
            apiRememberCheck.TabIndex = 15;
            apiRememberCheck.Text = "Remember Client Id";
            apiRememberCheck.UseVisualStyleBackColor = true;
            apiRememberCheck.CheckedChanged += apiRememberCheck_CheckedChanged;
            // 
            // monitorComboBox
            // 
            monitorComboBox.BackColor = Color.White;
            monitorComboBox.DrawMode = DrawMode.OwnerDrawFixed;
            monitorComboBox.Font = new Font("Segoe UI", 12F);
            monitorComboBox.ForeColor = SystemColors.ControlText;
            monitorComboBox.FormattingEnabled = true;
            monitorComboBox.Location = new Point(12, 167);
            monitorComboBox.Name = "monitorComboBox";
            monitorComboBox.Size = new Size(295, 30);
            monitorComboBox.TabIndex = 17;
            monitorComboBox.DrawItem += monitorComboBox_DrawItem;
            monitorComboBox.SelectedIndexChanged += monitorComboBox_SelectedIndexChanged;
            // 
            // monitorText
            // 
            monitorText.AutoSize = true;
            monitorText.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            monitorText.ForeColor = SystemColors.ControlLight;
            monitorText.Location = new Point(9, 147);
            monitorText.Name = "monitorText";
            monitorText.Size = new Size(72, 21);
            monitorText.TabIndex = 16;
            monitorText.Text = "Monitor";
            monitorText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // runButton
            // 
            runButton.BackColor = Color.Green;
            runButton.FlatStyle = FlatStyle.Popup;
            runButton.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point, 0);
            runButton.ForeColor = SystemColors.ControlLight;
            runButton.Location = new Point(12, 507);
            runButton.Name = "runButton";
            runButton.Size = new Size(295, 69);
            runButton.TabIndex = 18;
            runButton.Text = "Start";
            runButton.UseVisualStyleBackColor = false;
            runButton.Click += runButton_Click;
            // 
            // colorPictureBox
            // 
            colorPictureBox.BackColor = Color.FromArgb(243, 206, 50);
            colorPictureBox.Location = new Point(12, 115);
            colorPictureBox.Name = "colorPictureBox";
            colorPictureBox.Size = new Size(29, 29);
            colorPictureBox.TabIndex = 19;
            colorPictureBox.TabStop = false;
            colorPictureBox.BackColorChanged += colorPictureBox_BackColorChanged;
            // 
            // colorText
            // 
            colorText.AutoSize = true;
            colorText.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            colorText.ForeColor = SystemColors.ControlLight;
            colorText.Location = new Point(8, 95);
            colorText.Name = "colorText";
            colorText.Size = new Size(51, 21);
            colorText.TabIndex = 20;
            colorText.Text = "Color";
            colorText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // colorHexBox
            // 
            colorHexBox.Font = new Font("Segoe UI", 12F);
            colorHexBox.ForeColor = SystemColors.ControlText;
            colorHexBox.Location = new Point(47, 115);
            colorHexBox.Name = "colorHexBox";
            colorHexBox.Size = new Size(225, 29);
            colorHexBox.TabIndex = 21;
            colorHexBox.Leave += colorHexBox_stopFocus;
            // 
            // colorPickerButton
            // 
            colorPickerButton.ForeColor = SystemColors.ControlText;
            colorPickerButton.Location = new Point(278, 116);
            colorPickerButton.Name = "colorPickerButton";
            colorPickerButton.Size = new Size(29, 29);
            colorPickerButton.TabIndex = 22;
            colorPickerButton.UseVisualStyleBackColor = true;
            colorPickerButton.Click += colorPickerButton_Click;
            // 
            // dropShadowCheckBox
            // 
            dropShadowCheckBox.AutoSize = true;
            dropShadowCheckBox.Font = new Font("Candara", 12F);
            dropShadowCheckBox.ForeColor = SystemColors.ControlLight;
            dropShadowCheckBox.Location = new Point(153, 65);
            dropShadowCheckBox.Name = "dropShadowCheckBox";
            dropShadowCheckBox.RightToLeft = RightToLeft.Yes;
            dropShadowCheckBox.Size = new Size(118, 23);
            dropShadowCheckBox.TabIndex = 23;
            dropShadowCheckBox.Text = "drop shadow";
            dropShadowCheckBox.UseVisualStyleBackColor = true;
            dropShadowCheckBox.CheckedChanged += dropShadowCheckBox_CheckedChanged;
            // 
            // backgroundCheckBox
            // 
            backgroundCheckBox.AutoSize = true;
            backgroundCheckBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            backgroundCheckBox.ForeColor = SystemColors.ControlLight;
            backgroundCheckBox.Location = new Point(7, 261);
            backgroundCheckBox.Name = "backgroundCheckBox";
            backgroundCheckBox.RightToLeft = RightToLeft.Yes;
            backgroundCheckBox.Size = new Size(155, 25);
            backgroundCheckBox.TabIndex = 24;
            backgroundCheckBox.Text = "Background box";
            backgroundCheckBox.UseVisualStyleBackColor = true;
            backgroundCheckBox.CheckedChanged += backgroundCheckBox_CheckedChanged;
            // 
            // backgroundColorPictureBox
            // 
            backgroundColorPictureBox.BackColor = Color.Black;
            backgroundColorPictureBox.Location = new Point(12, 288);
            backgroundColorPictureBox.Name = "backgroundColorPictureBox";
            backgroundColorPictureBox.Size = new Size(29, 29);
            backgroundColorPictureBox.TabIndex = 25;
            backgroundColorPictureBox.TabStop = false;
            // 
            // backgroundColorHexBox
            // 
            backgroundColorHexBox.Font = new Font("Segoe UI", 12F);
            backgroundColorHexBox.ForeColor = SystemColors.ControlText;
            backgroundColorHexBox.Location = new Point(47, 288);
            backgroundColorHexBox.Name = "backgroundColorHexBox";
            backgroundColorHexBox.PlaceholderText = "#RRGGBB";
            backgroundColorHexBox.Size = new Size(120, 29);
            backgroundColorHexBox.TabIndex = 26;
            backgroundColorHexBox.Leave += backgroundColorHexBox_stopFocus;
            // 
            // backgroundColorPickerButton
            // 
            backgroundColorPickerButton.ForeColor = SystemColors.ControlText;
            backgroundColorPickerButton.Location = new Point(173, 289);
            backgroundColorPickerButton.Name = "backgroundColorPickerButton";
            backgroundColorPickerButton.Size = new Size(29, 29);
            backgroundColorPickerButton.TabIndex = 27;
            backgroundColorPickerButton.UseVisualStyleBackColor = true;
            backgroundColorPickerButton.Click += backgroundColorPickerButton_Click;
            // 
            // backgroundSpreadText
            // 
            backgroundSpreadText.AutoSize = true;
            backgroundSpreadText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            backgroundSpreadText.ForeColor = SystemColors.ControlLight;
            backgroundSpreadText.Location = new Point(208, 266);
            backgroundSpreadText.Name = "backgroundSpreadText";
            backgroundSpreadText.Size = new Size(59, 21);
            backgroundSpreadText.TabIndex = 28;
            backgroundSpreadText.Text = "Spread";
            backgroundSpreadText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // backgroundSpread
            // 
            backgroundSpread.Font = new Font("Segoe UI", 12F);
            backgroundSpread.ForeColor = SystemColors.ControlText;
            backgroundSpread.Location = new Point(212, 288);
            backgroundSpread.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            backgroundSpread.Name = "backgroundSpread";
            backgroundSpread.Size = new Size(95, 29);
            backgroundSpread.TabIndex = 29;
            backgroundSpread.ValueChanged += backgroundSpread_ValueChanged;
            //
            // backgroundOpacityText
            //
            backgroundOpacityText.AutoSize = true;
            backgroundOpacityText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            backgroundOpacityText.ForeColor = SystemColors.ControlLight;
            backgroundOpacityText.Location = new Point(9, 326);
            backgroundOpacityText.Name = "backgroundOpacityText";
            backgroundOpacityText.Size = new Size(67, 21);
            backgroundOpacityText.TabIndex = 30;
            backgroundOpacityText.Text = "Opacity";
            backgroundOpacityText.TextAlign = ContentAlignment.MiddleLeft;
            //
            // backgroundOpacity
            //
            backgroundOpacity.AutoSize = false;
            backgroundOpacity.Location = new Point(82, 324);
            backgroundOpacity.Maximum = 100;
            backgroundOpacity.Name = "backgroundOpacity";
            backgroundOpacity.Size = new Size(170, 30);
            backgroundOpacity.TabIndex = 31;
            backgroundOpacity.TickStyle = TickStyle.None;
            backgroundOpacity.Value = 70;
            backgroundOpacity.ValueChanged += backgroundOpacity_ValueChanged;
            //
            // backgroundOpacityValue
            //
            backgroundOpacityValue.Font = new Font("Segoe UI", 12F);
            backgroundOpacityValue.ForeColor = SystemColors.ControlLight;
            backgroundOpacityValue.Location = new Point(255, 326);
            backgroundOpacityValue.Name = "backgroundOpacityValue";
            backgroundOpacityValue.Size = new Size(52, 21);
            backgroundOpacityValue.TabIndex = 32;
            backgroundOpacityValue.Text = "70%";
            backgroundOpacityValue.TextAlign = ContentAlignment.MiddleRight;
            //
            // transitionText
            //
            transitionText.AutoSize = true;
            transitionText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            transitionText.ForeColor = SystemColors.ControlLight;
            transitionText.Location = new Point(9, 360);
            transitionText.Name = "transitionText";
            transitionText.Size = new Size(117, 21);
            transitionText.TabIndex = 33;
            transitionText.Text = "Line transition";
            transitionText.TextAlign = ContentAlignment.MiddleLeft;
            //
            // transitionComboBox
            //
            transitionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            transitionComboBox.Font = new Font("Segoe UI", 12F);
            transitionComboBox.FormattingEnabled = true;
            transitionComboBox.Location = new Point(12, 382);
            transitionComboBox.Name = "transitionComboBox";
            transitionComboBox.Size = new Size(295, 29);
            transitionComboBox.TabIndex = 34;
            transitionComboBox.SelectedIndexChanged += transitionComboBox_SelectedIndexChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(27, 43, 52);
            ClientSize = new Size(319, 590);
            Controls.Add(transitionComboBox);
            Controls.Add(transitionText);
            Controls.Add(backgroundOpacityValue);
            Controls.Add(backgroundOpacity);
            Controls.Add(backgroundOpacityText);
            Controls.Add(backgroundSpread);
            Controls.Add(backgroundSpreadText);
            Controls.Add(backgroundColorPickerButton);
            Controls.Add(backgroundColorHexBox);
            Controls.Add(backgroundColorPictureBox);
            Controls.Add(backgroundCheckBox);
            Controls.Add(dropShadowCheckBox);
            Controls.Add(colorPickerButton);
            Controls.Add(colorHexBox);
            Controls.Add(colorText);
            Controls.Add(colorPictureBox);
            Controls.Add(runButton);
            Controls.Add(monitorComboBox);
            Controls.Add(monitorText);
            Controls.Add(apiKeyBox);
            Controls.Add(apiKeyText);
            Controls.Add(yText);
            Controls.Add(yOffset);
            Controls.Add(xText);
            Controls.Add(xOffset);
            Controls.Add(italicCheckBox);
            Controls.Add(boldCheckBox);
            Controls.Add(decreaseFontSizeButton);
            Controls.Add(fontSizeBox);
            Controls.Add(addFontSizeButton);
            Controls.Add(comboBoxFont);
            Controls.Add(fontText);
            Controls.Add(apiRememberCheck);
            MaximizeBox = false;
            MaximumSize = new Size(335, 629);
            MinimumSize = new Size(335, 629);
            Name = "Form1";
            Text = "Spotify-Lyrics-Overlay";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)xOffset).EndInit();
            ((System.ComponentModel.ISupportInitialize)yOffset).EndInit();
            ((System.ComponentModel.ISupportInitialize)colorPictureBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)backgroundColorPictureBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)backgroundSpread).EndInit();
            ((System.ComponentModel.ISupportInitialize)backgroundOpacity).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label fontText;
        public ComboBox comboBoxFont;
        private CheckBox boldCheckBox;
        private CheckBox italicCheckBox;
        private TextBox fontSizeBox;
        private Button decreaseFontSizeButton;
        private Button addFontSizeButton;
        private NumericUpDown xOffset;
        private Label xText;
        private Label yText;
        private NumericUpDown yOffset;
        private Label apiKeyText;
        private TextBox apiKeyBox;
        private CheckBox apiRememberCheck;
        public ComboBox monitorComboBox;
        private Label monitorText;
        private Button runButton;
        private ColorDialog colorDialog;
        private PictureBox colorPictureBox;
        private Label colorText;
        private TextBox colorHexBox;
        private Button colorPickerButton;
        private CheckBox dropShadowCheckBox;
        private CheckBox backgroundCheckBox;
        private PictureBox backgroundColorPictureBox;
        private TextBox backgroundColorHexBox;
        private Button backgroundColorPickerButton;
        private Label backgroundSpreadText;
        private NumericUpDown backgroundSpread;
        private Label backgroundOpacityText;
        private TrackBar backgroundOpacity;
        private Label backgroundOpacityValue;
        private Label transitionText;
        private ComboBox transitionComboBox;
    }
}
