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
        private Button saveButton;
        private Button cancelButton;
        private Button labelBuilderButton;
        private Button qrBuilderButton;

        public SettingsDialog()
        {
            InitializeComponents();
            LoadCurrentSettings();
        }

        private void InitializeComponents()
        {
            this.Text = "Settings";
            this.Size = new Size(400, 400);  // Increased height
            this.MinimumSize = new Size(350, 400);  // Increased minimum height
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 3,
                ColumnCount = 1
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Label Size group
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Label Tools group
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Buttons

            // Label Size group
            GroupBox labelSizeGroup = new GroupBox
            {
                Text = "Label Size",
                AutoSize = true,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 15)  // Increased bottom margin
            };

            TableLayoutPanel labelSizeLayout = new TableLayoutPanel
            {
                AutoSize = true,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0, 5, 0, 0)  // Added top margin
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

            labelSizeGroup.Controls.Add(labelSizeLayout);
            mainLayout.Controls.Add(labelSizeGroup, 0, 0);

            // Label Tools group
            GroupBox labelToolsGroup = new GroupBox
            {
                Text = "Label Tools",
                AutoSize = true,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 15)  // Increased bottom margin
            };

            TableLayoutPanel labelToolsLayout = new TableLayoutPanel
            {
                AutoSize = true,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0, 5, 0, 0)  // Added top margin
            };

            // Label Builder Button
            labelBuilderButton = new Button
            {
                Text = "Label Builder",
                Width = 200,
                Height = 30,
                Margin = new Padding(0, 0, 0, 10),  // Increased bottom margin
                Anchor = AnchorStyles.None
            };
            labelBuilderButton.Click += LabelBuilderButton_Click;
            labelToolsLayout.Controls.Add(labelBuilderButton, 0, 0);

            // QR Builder Button
            qrBuilderButton = new Button
            {
                Text = "QR Builder",
                Width = 200,
                Height = 30,
                Margin = new Padding(0),
                Anchor = AnchorStyles.None
            };
            qrBuilderButton.Click += QRBuilderButton_Click;
            labelToolsLayout.Controls.Add(qrBuilderButton, 0, 1);

            labelToolsGroup.Controls.Add(labelToolsLayout);
            mainLayout.Controls.Add(labelToolsGroup, 0, 1);

            // Buttons panel
            Panel buttonPanel = new Panel
            {
                Height = 40,
                Margin = new Padding(0, 5, 0, 0)  // Added top margin
            };

            saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Width = 80,
                Height = 30
            };
            saveButton.Click += SaveButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Height = 30
            };

            // Position buttons
            saveButton.Location = new Point(buttonPanel.Width - 170, 5);
            cancelButton.Location = new Point(buttonPanel.Width - 85, 5);
            buttonPanel.Controls.AddRange(new Control[] { saveButton, cancelButton });
            mainLayout.Controls.Add(buttonPanel, 0, 2);

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
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // Save settings to SettingsManager
            var settings = new Settings
            {
                LabelWidth = (double)labelWidthInput.Value,
                LabelHeight = (double)labelHeightInput.Value,
                LabelMargin = 2.0 // Use default margin value
            };

            SettingsManager.SaveSettings(settings);
        }

        private void LabelBuilderButton_Click(object sender, EventArgs e)
        {
            using (var labelBuilder = new LabelBuilderDialog())
            {
                labelBuilder.ShowDialog();
            }
        }

        private void QRBuilderButton_Click(object sender, EventArgs e)
        {
            using (var qrBuilder = new QRBuilderDialog())
            {
                qrBuilder.ShowDialog();
            }
        }
    }
} 