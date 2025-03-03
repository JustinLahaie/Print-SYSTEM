using System;
using System.Windows.Forms;
using System.Drawing;
using PrintSystem.Models;
using PrintSystem.Managers;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace PrintSystem.Dialogs
{
    public class SettingsDialog : Form
    {
        private ComboBox defaultLabelComboBox;
        private ComboBox defaultLabelTemplateComboBox;
        private ComboBox printOrientationComboBox;
        private Button saveButton;
        private Button cancelButton;
        private Button labelBuilderButton;
        private Button qrBuilderButton;
        private Dictionary<string, LabelTemplate> labelTemplates;
        private const string LABEL_TEMPLATES_FILE = "label_templates.json";

        public SettingsDialog()
        {
            LoadLabelTemplates();
            InitializeComponents();
            LoadCurrentSettings();
        }

        private void LoadLabelTemplates()
        {
            try
            {
                if (File.Exists(LABEL_TEMPLATES_FILE))
                {
                    string json = File.ReadAllText(LABEL_TEMPLATES_FILE);
                    labelTemplates = JsonSerializer.Deserialize<Dictionary<string, LabelTemplate>>(json);
                }
                else
                {
                    labelTemplates = new Dictionary<string, LabelTemplate>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading label templates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                labelTemplates = new Dictionary<string, LabelTemplate>();
            }
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
                RowCount = 3,
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
            defaultLabelComboBox.Items.Clear();
            
            // Add MAIN template first if it exists
            if (labelTemplates.ContainsKey("MAIN"))
            {
                defaultLabelComboBox.Items.Add("MAIN");
            }
            
            // Add other templates alphabetically
            foreach (var template in labelTemplates.Keys)
            {
                if (template != "MAIN")
                {
                    defaultLabelComboBox.Items.Add(template);
                }
            }
            
            // Add available template types
            if (defaultLabelComboBox.Items.Count == 0)
            {
                defaultLabelComboBox.Items.Add("Basic Label");
            }
            
            labelSettingsLayout.Controls.Add(defaultLabelComboBox, 1, 0);
            
            // Default Label Template
            labelSettingsLayout.Controls.Add(new Label { Text = "Default Template:", Anchor = AnchorStyles.Left }, 0, 1);
            defaultLabelTemplateComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150
            };
            
            // Add MAIN template first if it exists
            if (labelTemplates.ContainsKey("MAIN"))
            {
                defaultLabelTemplateComboBox.Items.Add("MAIN");
            }
            
            // Add other templates alphabetically
            foreach (var template in labelTemplates.Keys)
            {
                if (template != "MAIN")
                {
                    defaultLabelTemplateComboBox.Items.Add(template);
                }
            }
            
            // Add at least one default option
            if (defaultLabelTemplateComboBox.Items.Count == 0)
            {
                defaultLabelTemplateComboBox.Items.Add("MAIN");
            }
            
            labelSettingsLayout.Controls.Add(defaultLabelTemplateComboBox, 1, 1);

            // Print Orientation
            labelSettingsLayout.Controls.Add(new Label { Text = "Print Orientation:", Anchor = AnchorStyles.Left }, 0, 2);
            printOrientationComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150
            };
            printOrientationComboBox.Items.Add("Landscape");
            printOrientationComboBox.Items.Add("Portrait");
            labelSettingsLayout.Controls.Add(printOrientationComboBox, 1, 2);

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
            
            // Only try to select a template if there are items in the combo box
            if (defaultLabelComboBox.Items.Count > 0)
            {
                // Try to select the saved template
                defaultLabelComboBox.SelectedItem = settings.DefaultLabelType;
                
                // If the saved label type isn't in the list, select the first template
                if (defaultLabelComboBox.SelectedItem == null)
                {
                    defaultLabelComboBox.SelectedIndex = 0;
                }
            }
            
            // Set the default label template
            if (defaultLabelTemplateComboBox.Items.Count > 0)
            {
                // Try to select the saved template
                defaultLabelTemplateComboBox.SelectedItem = settings.DefaultLabelTemplate;
                
                // If the saved template isn't in the list, select the first template
                if (defaultLabelTemplateComboBox.SelectedItem == null)
                {
                    defaultLabelTemplateComboBox.SelectedIndex = 0;
                }
            }

            // Select the print orientation
            if (printOrientationComboBox.Items.Contains(settings.PrintOrientation))
            {
                printOrientationComboBox.SelectedItem = settings.PrintOrientation;
            }
            else
            {
                printOrientationComboBox.SelectedItem = "Landscape";
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Get current settings
                var settings = SettingsManager.GetSettings();
                
                // Update settings with dialog values
                settings.DefaultLabelType = defaultLabelComboBox.SelectedItem?.ToString() ?? "Basic Label";
                settings.DefaultLabelTemplate = defaultLabelTemplateComboBox.SelectedItem?.ToString() ?? "MAIN";
                settings.PrintOrientation = printOrientationComboBox.SelectedItem?.ToString() ?? "Landscape";
                
                // Save settings
                SettingsManager.SaveSettings(settings);
                
                // Close dialog
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LabelBuilderButton_Click(object sender, EventArgs e)
        {
            using (var labelBuilder = new LabelBuilderDialog())
            {
                if (labelBuilder.ShowDialog() == DialogResult.OK)
                {
                    // Reload templates and update combo box
                    LoadLabelTemplates();
                    string currentSelection = defaultLabelComboBox.SelectedItem?.ToString();
                    
                    defaultLabelComboBox.Items.Clear();
                    defaultLabelComboBox.Items.AddRange(new object[] {
                        "Basic Label",
                        "Product Label",
                        "Price Label"
                    });

                    foreach (string templateName in labelTemplates.Keys)
                    {
                        defaultLabelComboBox.Items.Add(templateName);
                    }

                    defaultLabelComboBox.Items.Add("Custom");

                    // Restore previous selection if possible
                    if (!string.IsNullOrEmpty(currentSelection))
                    {
                        defaultLabelComboBox.SelectedItem = currentSelection;
                    }
                }
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