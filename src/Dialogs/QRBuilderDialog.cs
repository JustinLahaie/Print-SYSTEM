using System;
using System.Windows.Forms;
using System.Drawing;
using PrintSystem.Models;
using PrintSystem.Managers;

namespace PrintSystem.Dialogs
{
    public class QRBuilderDialog : Form
    {
        private Panel previewPanel;
        private TextBox contentTextBox;
        private ComboBox sizeComboBox;
        private ComboBox errorCorrectionComboBox;
        private Button generateButton;
        private Button saveButton;
        private Button printButton;
        private PictureBox qrPreview;

        public QRBuilderDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "QR Code Builder";
            this.Size = new Size(800, 600);
            this.MinimumSize = new Size(600, 400);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(10)
            };

            // Configure row and column styles
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

            // Left panel for controls
            Panel controlsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            // Content group
            GroupBox contentGroup = new GroupBox
            {
                Text = "QR Code Content",
                Dock = DockStyle.Top,
                Height = 120,
                Padding = new Padding(5)
            };

            contentTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true
            };
            contentGroup.Controls.Add(contentTextBox);
            controlsPanel.Controls.Add(contentGroup);

            // Settings group
            GroupBox settingsGroup = new GroupBox
            {
                Text = "Settings",
                Dock = DockStyle.Top,
                Height = 120,
                Padding = new Padding(5),
                Top = contentGroup.Bottom + 10
            };

            TableLayoutPanel settingsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 2
            };

            // Size setting
            settingsLayout.Controls.Add(new Label { Text = "Size:", Anchor = AnchorStyles.Left }, 0, 0);
            sizeComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120
            };
            sizeComboBox.Items.AddRange(new object[] { "Small", "Medium", "Large" });
            sizeComboBox.SelectedIndex = 1;
            settingsLayout.Controls.Add(sizeComboBox, 1, 0);

            // Error correction setting
            settingsLayout.Controls.Add(new Label { Text = "Error Correction:", Anchor = AnchorStyles.Left }, 0, 1);
            errorCorrectionComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120
            };
            errorCorrectionComboBox.Items.AddRange(new object[] { "Low", "Medium", "High" });
            errorCorrectionComboBox.SelectedIndex = 1;
            settingsLayout.Controls.Add(errorCorrectionComboBox, 1, 1);

            // Generate button
            generateButton = new Button
            {
                Text = "Generate QR Code",
                Width = 120,
                Height = 30
            };
            generateButton.Click += GenerateButton_Click;
            settingsLayout.Controls.Add(generateButton, 1, 2);

            settingsGroup.Controls.Add(settingsLayout);
            controlsPanel.Controls.Add(settingsGroup);

            // Right panel for preview
            previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                BorderStyle = BorderStyle.FixedSingle
            };

            qrPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };
            previewPanel.Controls.Add(qrPreview);

            // Bottom panel for action buttons
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40
            };

            printButton = new Button
            {
                Text = "Print",
                Width = 100,
                Height = 30,
                Location = new Point(bottomPanel.Width - 110, 5),
                Anchor = AnchorStyles.Right
            };
            printButton.Click += PrintButton_Click;

            saveButton = new Button
            {
                Text = "Save",
                Width = 100,
                Height = 30,
                Location = new Point(bottomPanel.Width - 220, 5),
                Anchor = AnchorStyles.Right
            };
            saveButton.Click += SaveButton_Click;

            bottomPanel.Controls.AddRange(new Control[] { saveButton, printButton });

            // Add all panels to the main layout
            mainLayout.Controls.Add(controlsPanel, 0, 0);
            mainLayout.Controls.Add(previewPanel, 1, 0);
            mainLayout.Controls.Add(bottomPanel, 0, 1);
            mainLayout.SetColumnSpan(bottomPanel, 2);

            this.Controls.Add(mainLayout);

            // Wire up events
            contentTextBox.TextChanged += (s, e) => UpdatePreview();
            sizeComboBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            errorCorrectionComboBox.SelectedIndexChanged += (s, e) => UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrWhiteSpace(contentTextBox.Text))
            {
                if (qrPreview.Image != null)
                {
                    qrPreview.Image.Dispose();
                    qrPreview.Image = null;
                }
                return;
            }

            // TODO: Implement actual QR code generation
            // For now, just show a placeholder
            var settings = SettingsManager.GetSettings();
            int size = 200; // Base size for QR code

            switch (sizeComboBox.SelectedItem?.ToString())
            {
                case "Small":
                    size = 150;
                    break;
                case "Large":
                    size = 300;
                    break;
            }

            if (qrPreview.Image != null)
            {
                qrPreview.Image.Dispose();
            }

            var bitmap = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.DrawRectangle(Pens.Black, 0, 0, size - 1, size - 1);
                
                // Draw placeholder text
                using (var font = new Font("Arial", 10))
                {
                    string text = "QR Code Preview\nContent: " + contentTextBox.Text;
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(text, font, Brushes.Black, new RectangleF(0, 0, size, size), format);
                }
            }

            qrPreview.Image = bitmap;
        }

        private void GenerateButton_Click(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (qrPreview.Image == null)
            {
                MessageBox.Show("Please generate a QR code first.", "No QR Code", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
                saveDialog.Title = "Save QR Code";
                saveDialog.DefaultExt = "png";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        qrPreview.Image.Save(saveDialog.FileName);
                        MessageBox.Show("QR code saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving QR code: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void PrintButton_Click(object sender, EventArgs e)
        {
            // TODO: Implement printing
            MessageBox.Show("Printing will be implemented in a future update.", "Coming Soon");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (qrPreview.Image != null)
            {
                qrPreview.Image.Dispose();
            }
        }
    }
} 