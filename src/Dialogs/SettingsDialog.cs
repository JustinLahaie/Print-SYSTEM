using System;
using System.Windows.Forms;
using System.Drawing;
using PrintSystem.Models;
using PrintSystem.Managers;

namespace PrintSystem.Dialogs
{
    public class SettingsDialog : Form
    {
        private ComboBox defaultLabelComboBox;
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
            this.Size = new Size(400, 400);
            this.MinimumSize = new Size(350, 400);
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

            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Label Settings group
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Label Tools group
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Buttons

            // Label Settings group
            GroupBox labelSettingsGroup = new GroupBox
            {
                Text = "Label Settings",
                AutoSize = true,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 15)
            };

            TableLayoutPanel labelSettingsLayout = new TableLayoutPanel
            {
                AutoSize = true,
                RowCount = 1,
                ColumnCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0, 5, 0, 0)
            };

            // Default Label Type
            labelSettingsLayout.Controls.Add(new Label { Text = "Default Label:", Anchor = AnchorStyles.Left }, 0, 0);
            defaultLabelComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150
            };
            defaultLabelComboBox.Items.AddRange(new object[] {
                "Basic Label",
                "Product Label",
                "Price Label",
                "Custom"
            });
            labelSettingsLayout.Controls.Add(defaultLabelComboBox, 1, 0);

            labelSettingsGroup.Controls.Add(labelSettingsLayout);
            mainLayout.Controls.Add(labelSettingsGroup, 0, 0);

            // Label Tools group
            GroupBox labelToolsGroup = new GroupBox
            {
                Text = "Label Tools",
                AutoSize = true,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 15)
            };

            TableLayoutPanel labelToolsLayout = new TableLayoutPanel
            {
                AutoSize = true,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0, 5, 0, 0)
            };

            // Label Builder Button
            labelBuilderButton = new Button
            {
                Text = "Label Builder",
                Width = 200,
                Height = 30,
                Margin = new Padding(0, 0, 0, 10),
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
                Margin = new Padding(0, 5, 0, 0)
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
            defaultLabelComboBox.SelectedItem = settings.DefaultLabelType;
            
            // If the saved label type isn't in the list, select Basic Label
            if (defaultLabelComboBox.SelectedItem == null)
            {
                defaultLabelComboBox.SelectedItem = "Basic Label";
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // Save settings to SettingsManager
            var settings = new Settings
            {
                DefaultLabelType = defaultLabelComboBox.SelectedItem.ToString(),
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