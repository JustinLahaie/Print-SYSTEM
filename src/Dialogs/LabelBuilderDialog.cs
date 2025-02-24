using System;
using System.Windows.Forms;
using System.Drawing;
using PrintSystem.Models;
using PrintSystem.Managers;

namespace PrintSystem.Dialogs
{
    public class LabelBuilderDialog : Form
    {
        private Panel previewPanel;
        private ComboBox templateComboBox;
        private Button saveTemplateButton;
        private Button loadTemplateButton;
        private Button printButton;
        private PictureBox labelPreview;

        public LabelBuilderDialog()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Label Builder";
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

            // Template controls
            GroupBox templateGroup = new GroupBox
            {
                Text = "Template",
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(5)
            };

            templateComboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            templateComboBox.Items.AddRange(new object[] { "Basic Label", "Product Label", "Price Label" });
            templateComboBox.SelectedIndex = 0;

            FlowLayoutPanel templateButtonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 35,
                FlowDirection = FlowDirection.LeftToRight
            };

            loadTemplateButton = new Button
            {
                Text = "Load",
                Width = 70
            };
            loadTemplateButton.Click += LoadTemplateButton_Click;

            saveTemplateButton = new Button
            {
                Text = "Save",
                Width = 70
            };
            saveTemplateButton.Click += SaveTemplateButton_Click;

            templateButtonsPanel.Controls.AddRange(new Control[] { loadTemplateButton, saveTemplateButton });
            templateGroup.Controls.AddRange(new Control[] { templateComboBox, templateButtonsPanel });
            controlsPanel.Controls.Add(templateGroup);

            // Right panel for preview
            previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5),
                BorderStyle = BorderStyle.FixedSingle
            };

            labelPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };
            previewPanel.Controls.Add(labelPreview);

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

            bottomPanel.Controls.Add(printButton);

            // Add all panels to the main layout
            mainLayout.Controls.Add(controlsPanel, 0, 0);
            mainLayout.Controls.Add(previewPanel, 1, 0);
            mainLayout.Controls.Add(bottomPanel, 0, 1);
            mainLayout.SetColumnSpan(bottomPanel, 2);

            this.Controls.Add(mainLayout);

            // Initial preview update
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            // Create a new bitmap for the preview
            var settings = SettingsManager.GetSettings();
            int width = (int)(settings.LabelWidth * 3.779528); // Convert mm to pixels (96 DPI)
            int height = (int)(settings.LabelHeight * 3.779528);

            if (labelPreview.Image != null)
            {
                labelPreview.Image.Dispose();
            }

            var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.DrawRectangle(Pens.LightGray, 0, 0, width - 1, height - 1);

                // Draw template preview based on selected template
                switch (templateComboBox.SelectedItem?.ToString())
                {
                    case "Basic Label":
                        DrawBasicLabelPreview(g, width, height);
                        break;
                    case "Product Label":
                        DrawProductLabelPreview(g, width, height);
                        break;
                    case "Price Label":
                        DrawPriceLabelPreview(g, width, height);
                        break;
                }
            }

            labelPreview.Image = bitmap;
        }

        private void DrawBasicLabelPreview(Graphics g, int width, int height)
        {
            // Draw sample text
            using (var font = new Font("Arial", 10))
            {
                g.DrawString("Sample Basic Label", font, Brushes.Black, 10, 10);
            }
        }

        private void DrawProductLabelPreview(Graphics g, int width, int height)
        {
            // Draw product label layout
            using (var titleFont = new Font("Arial", 12, FontStyle.Bold))
            using (var detailsFont = new Font("Arial", 9))
            {
                g.DrawString("Product Name", titleFont, Brushes.Black, 10, 10);
                g.DrawString("SKU: 123456", detailsFont, Brushes.Black, 10, 30);
                g.DrawString("Category: Sample", detailsFont, Brushes.Black, 10, 45);
            }
        }

        private void DrawPriceLabelPreview(Graphics g, int width, int height)
        {
            // Draw price label layout
            using (var priceFont = new Font("Arial", 16, FontStyle.Bold))
            using (var detailsFont = new Font("Arial", 9))
            {
                g.DrawString("$99.99", priceFont, Brushes.Black, 10, height / 2 - 15);
                g.DrawString("Item: Sample Product", detailsFont, Brushes.Black, 10, 10);
            }
        }

        private void LoadTemplateButton_Click(object sender, EventArgs e)
        {
            // TODO: Implement template loading
            MessageBox.Show("Template loading will be implemented in a future update.", "Coming Soon");
        }

        private void SaveTemplateButton_Click(object sender, EventArgs e)
        {
            // TODO: Implement template saving
            MessageBox.Show("Template saving will be implemented in a future update.", "Coming Soon");
        }

        private void PrintButton_Click(object sender, EventArgs e)
        {
            // TODO: Implement printing
            MessageBox.Show("Printing will be implemented in a future update.", "Coming Soon");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (labelPreview.Image != null)
            {
                labelPreview.Image.Dispose();
            }
        }
    }
} 