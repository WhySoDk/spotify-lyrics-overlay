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
            textBox1 = new TextBox();
            apiRememberCheck = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)xOffset).BeginInit();
            ((System.ComponentModel.ISupportInitialize)yOffset).BeginInit();
            SuspendLayout();
            // 
            // fontText
            // 
            fontText.AutoSize = true;
            fontText.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            fontText.ForeColor = SystemColors.ControlLight;
            fontText.Location = new Point(12, 11);
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
            boldCheckBox.Font = new Font("Candara", 12F, FontStyle.Bold);
            boldCheckBox.ForeColor = SystemColors.ControlLight;
            boldCheckBox.Location = new Point(12, 65);
            boldCheckBox.Name = "boldCheckBox";
            boldCheckBox.RightToLeft = RightToLeft.Yes;
            boldCheckBox.Size = new Size(60, 23);
            boldCheckBox.TabIndex = 7;
            boldCheckBox.Text = "Bold";
            boldCheckBox.UseVisualStyleBackColor = true;
            boldCheckBox.CheckedChanged += boldCheckBox_CheckedChanged;
            // 
            // italicCheckBox
            // 
            italicCheckBox.AutoSize = true;
            italicCheckBox.Font = new Font("Candara", 12F, FontStyle.Bold);
            italicCheckBox.ForeColor = SystemColors.ControlLight;
            italicCheckBox.Location = new Point(78, 65);
            italicCheckBox.Name = "italicCheckBox";
            italicCheckBox.RightToLeft = RightToLeft.Yes;
            italicCheckBox.Size = new Size(61, 23);
            italicCheckBox.TabIndex = 8;
            italicCheckBox.Text = "Italic";
            italicCheckBox.UseVisualStyleBackColor = true;
            italicCheckBox.CheckedChanged += italicCheckBox_CheckedChanged;
            // 
            // fontSizeBox
            // 
            fontSizeBox.Font = new Font("Segoe UI", 12F);
            fontSizeBox.ForeColor = SystemColors.ControlLight;
            fontSizeBox.Location = new Point(215, 36);
            fontSizeBox.Name = "fontSizeBox";
            fontSizeBox.Size = new Size(49, 29);
            fontSizeBox.TabIndex = 3;
            fontSizeBox.TextAlign = HorizontalAlignment.Center;
            fontSizeBox.TextChanged += fontSizeBox_TextChanged;
            fontSizeBox.KeyPress += fontSizeBox_KeyPress;
            // 
            // decreaseFontSizeButton
            // 
            decreaseFontSizeButton.ForeColor = SystemColors.ControlLight;
            decreaseFontSizeButton.Location = new Point(270, 36);
            decreaseFontSizeButton.Name = "decreaseFontSizeButton";
            decreaseFontSizeButton.Size = new Size(25, 23);
            decreaseFontSizeButton.TabIndex = 4;
            decreaseFontSizeButton.Text = "-";
            decreaseFontSizeButton.UseVisualStyleBackColor = true;
            decreaseFontSizeButton.Click += decreaseFontSizeButton_Click;
            // 
            // addFontSizeButton
            // 
            addFontSizeButton.ForeColor = SystemColors.ControlLight;
            addFontSizeButton.Location = new Point(184, 36);
            addFontSizeButton.Name = "addFontSizeButton";
            addFontSizeButton.Size = new Size(25, 23);
            addFontSizeButton.TabIndex = 2;
            addFontSizeButton.Text = "+";
            addFontSizeButton.UseVisualStyleBackColor = true;
            addFontSizeButton.Click += addFontSizeButton_Click;
            // 
            // xOffset
            // 
            xOffset.Font = new Font("Segoe UI", 12F);
            xOffset.ForeColor = SystemColors.ControlLight;
            xOffset.Location = new Point(12, 128);
            xOffset.Name = "xOffset";
            xOffset.Size = new Size(120, 29);
            xOffset.TabIndex = 9;
            xOffset.ValueChanged += xOffset_ValueChanged;
            // 
            // xText
            // 
            xText.AutoSize = true;
            xText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            xText.ForeColor = SystemColors.ControlLight;
            xText.Location = new Point(12, 106);
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
            yText.Location = new Point(144, 106);
            yText.Name = "yText";
            yText.RightToLeft = RightToLeft.Yes;
            yText.Size = new Size(102, 21);
            yText.TabIndex = 12;
            yText.Text = "Y axis offset";
            yText.TextAlign = ContentAlignment.MiddleLeft;
            yText.Click += yText_Click;
            // 
            // yOffset
            // 
            yOffset.Font = new Font("Segoe UI", 12F);
            yOffset.ForeColor = SystemColors.ControlLight;
            yOffset.Location = new Point(144, 128);
            yOffset.Name = "yOffset";
            yOffset.Size = new Size(120, 29);
            yOffset.TabIndex = 11;
            yOffset.ValueChanged += yOffset_ValueChanged;
            // 
            // apiKeyText
            // 
            apiKeyText.AutoSize = true;
            apiKeyText.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            apiKeyText.ForeColor = SystemColors.ControlLight;
            apiKeyText.Location = new Point(12, 172);
            apiKeyText.Name = "apiKeyText";
            apiKeyText.Size = new Size(150, 21);
            apiKeyText.TabIndex = 13;
            apiKeyText.Text = "Developer API key";
            apiKeyText.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // textBox1
            // 
            textBox1.Font = new Font("Segoe UI", 12F);
            textBox1.ForeColor = SystemColors.ControlLight;
            textBox1.Location = new Point(12, 194);
            textBox1.Name = "textBox1";
            textBox1.PasswordChar = '•';
            textBox1.Size = new Size(283, 29);
            textBox1.TabIndex = 14;
            textBox1.UseSystemPasswordChar = true;
            // 
            // apiRememberCheck
            // 
            apiRememberCheck.AutoSize = true;
            apiRememberCheck.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            apiRememberCheck.ForeColor = SystemColors.ControlLight;
            apiRememberCheck.Location = new Point(12, 229);
            apiRememberCheck.Name = "apiRememberCheck";
            apiRememberCheck.RightToLeft = RightToLeft.Yes;
            apiRememberCheck.Size = new Size(173, 25);
            apiRememberCheck.TabIndex = 15;
            apiRememberCheck.Text = "Remember API key";
            apiRememberCheck.UseVisualStyleBackColor = true;
            apiRememberCheck.CheckedChanged += apiRememberCheck_CheckedChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(31, 31, 31);
            ClientSize = new Size(314, 391);
            Controls.Add(apiRememberCheck);
            Controls.Add(textBox1);
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
            Name = "Form1";
            Text = "Spotify-Lyrics-Overlay";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)xOffset).EndInit();
            ((System.ComponentModel.ISupportInitialize)yOffset).EndInit();
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
        private TextBox textBox1;
        private CheckBox apiRememberCheck;
    }
}
