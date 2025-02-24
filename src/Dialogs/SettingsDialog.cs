using System;
using System.Windows.Forms;
using System.Drawing;
using PrintSystem.Models;
using PrintSystem.Managers;

namespace PrintSystem.Dialogs
{
    public class SettingsDialog : Form
    {
        private NumericUpDown labelWidthInput;
        private NumericUpDown labelHeightInput;
        private NumericUpDown labelMarginInput;
        private Button saveButton;
        private Button cancelButton;

        public SettingsDialog()
        {
            InitializeComponents();
            LoadCurrentSettings();
        }

        private void InitializeComponents()
        {
            this.Text = "Settings";
            this.Size = new Size(400, 300);
            this.MinimumSize = new Size(350, 250);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 5,
                ColumnCount = 2
            };

            // Make columns scale properly
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // Labels
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Inputs

            // Label Size group
            GroupBox labelSizeGroup = new GroupBox
            {
                Text = "Label Size",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            TableLayoutPanel labelSizeLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 2
            };

            // Width
            labelSizeLayout.Controls.Add(new Label { Text = "Width (mm):", Anchor = AnchorStyles.Left }, 0, 0);
            labelWidthInput = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 500,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Width = 100
            };
            labelSizeLayout.Controls.Add(labelWidthInput, 1, 0);

            // Height
            labelSizeLayout.Controls.Add(new Label { Text = "Height (mm):", Anchor = AnchorStyles.Left }, 0, 1);
            labelHeightInput = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 500,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Width = 100
            };
            labelSizeLayout.Controls.Add(labelHeightInput, 1, 1);

            // Margin
            labelSizeLayout.Controls.Add(new Label { Text = "Margin (mm):", Anchor = AnchorStyles.Left }, 0, 2);
            labelMarginInput = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 50,
                DecimalPlaces = 1,
                Increment = 0.5M,
                Width = 100
            };
            labelSizeLayout.Controls.Add(labelMarginInput, 1, 2);

            labelSizeGroup.Controls.Add(labelSizeLayout);
            mainLayout.Controls.Add(labelSizeGroup, 0, 0);
            mainLayout.SetColumnSpan(labelSizeGroup, 2);

            // Buttons
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };

            saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Width = 80,
                Location = new Point(buttonPanel.Width - 170, 10)
            };
            saveButton.Click += SaveButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Location = new Point(buttonPanel.Width - 85, 10)
            };

            buttonPanel.Controls.AddRange(new Control[] { saveButton, cancelButton });
            mainLayout.Controls.Add(buttonPanel, 0, 4);
            mainLayout.SetColumnSpan(buttonPanel, 2);

            this.Controls.Add(mainLayout);
            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
        }

        private void LoadCurrentSettings()
        {
            // Load settings from SettingsManager
            var settings = SettingsManager.GetSettings();
            labelWidthInput.Value = (decimal)settings.LabelWidth;
            labelHeightInput.Value = (decimal)settings.LabelHeight;
            labelMarginInput.Value = (decimal)settings.LabelMargin;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // Save settings to SettingsManager
            var settings = new Settings
            {
                LabelWidth = (double)labelWidthInput.Value,
                LabelHeight = (double)labelHeightInput.Value,
                LabelMargin = (double)labelMarginInput.Value
            };

            SettingsManager.SaveSettings(settings);
        }
    }
} 