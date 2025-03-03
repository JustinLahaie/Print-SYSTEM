using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using PrintSystem.Models;
using PrintSystem.Managers;
using ZXing;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace PrintSystem.Dialogs
{
    public class QRBuilderDialog : Form
    {
        private Panel previewPanel;
        private ListBox availableFieldsListBox;
        private RichTextBox contentTextBox;
        private ComboBox sizeComboBox;
        private ComboBox errorCorrectionComboBox;
        private Button generateButton;
        private Button saveButton;
        private Button printButton;
        private PictureBox qrPreview;
        private ComboBox templateComboBox;
        private Label previewLabel;

        // Store the current item if we're generating from an item context
        private Item currentItem;

        private Dictionary<string, string> savedTemplates = new Dictionary<string, string>();
        private const string TEMPLATES_FILE = "qr_templates.json";

        // Property to expose the generated QR code image
        public Image QRImage => qrPreview?.Image;

        public QRBuilderDialog(Item item = null)
        {
            currentItem = item;
            LoadTemplates();
            InitializeComponents();
            if (currentItem != null)
            {
                PopulateFieldsWithItem();
            }
        }

        private void LoadTemplates()
        {
            try
            {
                if (File.Exists(TEMPLATES_FILE))
                {
                    string json = File.ReadAllText(TEMPLATES_FILE);
                    savedTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    
                    // Ensure MAIN template exists
                    if (!savedTemplates.ContainsKey("MAIN"))
                    {
                        savedTemplates["MAIN"] = "Model: {ModelNumber} | Supplier: {Supplier} | Qty: {DefaultOrderQuantity} | URL: {ProductURL}";
                        SaveTemplates();
                    }
                }
                else
                {
                    // Create default templates
                    savedTemplates = new Dictionary<string, string>
                    {
                        ["MAIN"] = "Model: {ModelNumber} | Supplier: {Supplier} | Qty: {DefaultOrderQuantity} | URL: {ProductURL}"
                    };
                    SaveTemplates();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading QR templates: {ex.Message}\nDefault templates will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                savedTemplates = new Dictionary<string, string>
                {
                    ["MAIN"] = "Model: {ModelNumber} | Supplier: {Supplier} | Qty: {DefaultOrderQuantity} | URL: {ProductURL}"
                };
            }
        }

        private void SaveTemplates()
        {
            try
            {
                string json = JsonSerializer.Serialize(savedTemplates, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TEMPLATES_FILE, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving QR templates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponents()
        {
            this.Text = "QR Code Builder";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
                Padding = new Padding(10)
            };

            // Configure column styles
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            // Left panel for controls
            Panel controlsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            TableLayoutPanel controlsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(0)
            };
            controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // Template section
            controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F)); // Available fields
            controlsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Content editor
            controlsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // Settings

            // Template section
            GroupBox templateGroup = new GroupBox
            {
                Text = "Templates",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            TableLayoutPanel templateLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2
            };

            templateComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Add MAIN template first if it exists
            if (savedTemplates.ContainsKey("MAIN"))
            {
                templateComboBox.Items.Add("MAIN");
                templateComboBox.Items.Add("-------------------");
            }

            // Add built-in templates
            templateComboBox.Items.AddRange(new object[] {
                "Basic Info",
                "Full Details",
                "URL Only",
                "Custom"
            });

            // Add other saved templates
            foreach (string templateName in savedTemplates.Keys)
            {
                if (templateName != "MAIN")
                {
                    templateComboBox.Items.Add(templateName);
                }
            }
            templateComboBox.SelectedIndexChanged += TemplateComboBox_SelectedIndexChanged;

            // Template management buttons
            TableLayoutPanel templateButtonsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Margin = new Padding(0)
            };

            Button updateMainButton = new Button
            {
                Text = "Update MAIN",
                Width = 85,
                Height = 25
            };
            updateMainButton.Click += UpdateMainButton_Click;

            Button saveAsNewButton = new Button
            {
                Text = "Save As New",
                Width = 85,
                Height = 25
            };
            saveAsNewButton.Click += SaveTemplateButton_Click;

            Button deleteButton = new Button
            {
                Text = "Delete",
                Width = 70,
                Height = 25
            };
            deleteButton.Click += DeleteTemplateButton_Click;

            templateButtonsLayout.Controls.Add(updateMainButton, 0, 0);
            templateButtonsLayout.Controls.Add(saveAsNewButton, 1, 0);
            templateButtonsLayout.Controls.Add(deleteButton, 1, 1);

            templateLayout.Controls.Add(templateComboBox, 0, 0);
            templateLayout.Controls.Add(templateButtonsLayout, 1, 0);
            templateLayout.SetRowSpan(templateButtonsLayout, 2);
            templateGroup.Controls.Add(templateLayout);
            controlsLayout.Controls.Add(templateGroup, 0, 0);

            // Available fields section
            GroupBox fieldsGroup = new GroupBox
            {
                Text = "Available Fields",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            availableFieldsListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                SelectionMode = SelectionMode.One,
                AllowDrop = true
            };
            PopulateAvailableFields();

            // Enable drag and drop
            availableFieldsListBox.MouseDown += AvailableFieldsListBox_MouseDown;
            fieldsGroup.Controls.Add(availableFieldsListBox);
            controlsLayout.Controls.Add(fieldsGroup, 0, 1);

            // Content editor section
            GroupBox contentGroup = new GroupBox
            {
                Text = "QR Code Content",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            contentTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                AcceptsTab = true,
                AllowDrop = true,
                EnableAutoDragDrop = false  // Disable default drag-drop to use our custom handling
            };

            // Set up drag and drop handlers
            contentTextBox.DragEnter += ContentTextBox_DragEnter;
            contentTextBox.DragOver += ContentTextBox_DragOver;  // Add DragOver handler
            contentTextBox.DragDrop += ContentTextBox_DragDrop;
            contentTextBox.TextChanged += (s, e) => UpdatePreview();

            contentGroup.Controls.Add(contentTextBox);
            controlsLayout.Controls.Add(contentGroup, 0, 2);

            // Settings section
            GroupBox settingsGroup = new GroupBox
            {
                Text = "Settings",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            TableLayoutPanel settingsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
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
            sizeComboBox.SelectedIndexChanged += (s, e) => UpdatePreview();
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
            errorCorrectionComboBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            settingsLayout.Controls.Add(errorCorrectionComboBox, 1, 1);

            settingsGroup.Controls.Add(settingsLayout);
            controlsLayout.Controls.Add(settingsGroup, 0, 3);

            controlsPanel.Controls.Add(controlsLayout);

            // Right panel for preview
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            TableLayoutPanel previewLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(0)
            };
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Preview
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));  // Preview label
            previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // Buttons

            // Preview panel
            previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };

            qrPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };
            previewPanel.Controls.Add(qrPreview);
            previewLayout.Controls.Add(previewPanel, 0, 0);

            // Preview label
            previewLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopCenter,
                AutoSize = false
            };
            previewLayout.Controls.Add(previewLabel, 0, 1);

            // Buttons panel
            Panel buttonsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40
            };

            printButton = new Button
            {
                Text = "Print",
                Width = 100,
                Height = 30,
                Location = new Point(buttonsPanel.Width - 110, 5),
                Anchor = AnchorStyles.Right
            };
            printButton.Click += PrintButton_Click;

            saveButton = new Button
            {
                Text = "Save",
                Width = 100,
                Height = 30,
                Location = new Point(buttonsPanel.Width - 220, 5),
                Anchor = AnchorStyles.Right
            };
            saveButton.Click += SaveButton_Click;

            generateButton = new Button
            {
                Text = "Generate QR Code",
                Width = 120,
                Height = 30,
                Location = new Point(buttonsPanel.Width - 330, 5),
                Anchor = AnchorStyles.Right
            };
            generateButton.Click += GenerateButton_Click;

            buttonsPanel.Controls.AddRange(new Control[] { generateButton, saveButton, printButton });
            previewLayout.Controls.Add(buttonsPanel, 0, 2);

            rightPanel.Controls.Add(previewLayout);

            // Add panels to main layout
            mainLayout.Controls.Add(controlsPanel, 0, 0);
            mainLayout.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainLayout);

            // Set initial template
            templateComboBox.SelectedIndex = 0;
        }

        private void PopulateAvailableFields()
        {
            availableFieldsListBox.Items.Clear();
            availableFieldsListBox.Items.AddRange(new string[] {
                "Model Number",
                "Description",
                "Supplier",
                "Category",
                "Default Order Quantity",
                "Product URL",
                "Custom Text...",
                "New Line"
            });
        }

        private void PopulateFieldsWithItem()
        {
            if (currentItem == null) return;

            // Select the Basic Info template by default
            templateComboBox.SelectedIndex = 0;
        }

        private void TemplateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string template = templateComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(template)) return;

            StringBuilder content = new StringBuilder();

            // Check if it's a saved template first
            if (savedTemplates.ContainsKey(template))
            {
                contentTextBox.Text = savedTemplates[template];
                UpdatePreview();
                return;
            }

            // Handle built-in templates
            switch (template)
            {
                case "Basic Info":
                    content.Append($"Model: {"{ModelNumber}"} | Description: {"{Description}"}");
                    break;

                case "Full Details":
                    content.Append($"Model: {"{ModelNumber}"}")
                          .Append($" | Description: {"{Description}"}")
                          .Append($" | Supplier: {"{Supplier}"}")
                          .Append($" | Category: {"{Category}"}")
                          .Append($" | Order Qty: {"{DefaultOrderQuantity}"}")
                          .Append($" | URL: {"{ProductURL}"}");
                    break;

                case "URL Only":
                    content.Append("{ProductURL}");
                    break;

                case "Custom":
                    // Keep existing content
                    return;
            }

            contentTextBox.Text = content.ToString();
            UpdatePreview();
        }

        private void ContentTextBox_DragEnter(object sender, DragEventArgs e)
        {
            // Accept the drag if it's our expected format
            if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void ContentTextBox_DragOver(object sender, DragEventArgs e)
        {
            // Maintain the drag effect during the drag operation
            if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void ContentTextBox_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                {
                    // Get the dropped field name
                    string fieldName = (string)e.Data.GetData(DataFormats.UnicodeText);

                    // Convert to placeholder or text
                    string fieldValue = GetFieldValue(fieldName);

                    // Determine drop location in the RichTextBox
                    Point clientPoint = contentTextBox.PointToClient(new Point(e.X, e.Y));
                    int charIndex = contentTextBox.GetCharIndexFromPosition(clientPoint);

                    // Adjust position if we're inside a placeholder
                    string text = contentTextBox.Text;
                    int adjustedIndex = charIndex;

                    // Check if we're inside any placeholder and adjust position
                    int currentPos = 0;
                    while (currentPos < text.Length)
                    {
                        int nextOpen = text.IndexOf('{', currentPos);
                        if (nextOpen == -1) break;

                        int nextClose = text.IndexOf('}', nextOpen);
                        if (nextClose == -1) break;

                        // If our drop position is inside this placeholder, move to after it
                        if (charIndex > nextOpen && charIndex <= nextClose)
                        {
                            adjustedIndex = nextClose + 1;
                            break;
                        }

                        currentPos = nextClose + 1;
                    }

                    // Add pipe separator if needed
                    if (adjustedIndex > 0 && !string.IsNullOrWhiteSpace(text))
                    {
                        // Look for existing pipes, accounting for spaces
                        string beforeText = adjustedIndex > 0 ? text.Substring(Math.Max(0, adjustedIndex - 3), Math.Min(3, adjustedIndex)) : "";
                        string afterText = adjustedIndex < text.Length ? text.Substring(adjustedIndex, Math.Min(3, text.Length - adjustedIndex)) : "";

                        bool hasPipeBefore = beforeText.Contains("|");
                        bool hasPipeAfter = afterText.Contains("|");

                        if (!hasPipeBefore && !hasPipeAfter)
                        {
                            fieldValue = " | " + fieldValue;
                        }
                        else if (hasPipeAfter && !beforeText.EndsWith(" "))
                        {
                            fieldValue = " " + fieldValue;
                        }
                    }

                    // Insert the text at adjusted cursor position
                    contentTextBox.SelectionStart = adjustedIndex;
                    contentTextBox.SelectedText = fieldValue;

                    // Update the QR preview
                    UpdatePreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during drag and drop: {ex.Message}", 
                               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetFieldValue(string fieldName)
        {
            // Always return template placeholders regardless of whether we have an item
            switch (fieldName)
            {
                case "Model Number":
                    return "{ModelNumber}";
                case "Description":
                    return "{Description}";
                case "Supplier":
                    return "{Supplier}";
                case "Category":
                    return "{Category}";
                case "Default Order Quantity":
                    return "{DefaultOrderQuantity}";
                case "Product URL":
                    return "{ProductURL}";
                case "New Line":
                    return Environment.NewLine;
                default:
                    return fieldName; // For custom text
            }
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
                previewLabel.Text = "Enter content to generate QR code";
                return;
            }

            try
            {
                // Replace placeholders with actual values if we have an item
                string content = contentTextBox.Text;
                if (currentItem != null)
                {
                    content = content.Replace("{ModelNumber}", currentItem.ModelNumber ?? "")
                                   .Replace("{Description}", currentItem.Description ?? "")
                                   .Replace("{Supplier}", currentItem.Supplier ?? "")
                                   .Replace("{Category}", currentItem.CategoryPath ?? "Uncategorized")
                                   .Replace("{DefaultOrderQuantity}", currentItem.DefaultOrderQuantity.ToString())
                                   .Replace("{ProductURL}", currentItem.ProductUrl ?? "");
                }

                // Check if content is empty after placeholder replacement
                if (string.IsNullOrWhiteSpace(content))
                {
                    MessageBox.Show("QR code content is empty after replacing placeholders. Please check your template.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions
                    {
                        Width = GetQRCodeSize(),
                        Height = GetQRCodeSize(),
                        Margin = 1,
                        ErrorCorrection = GetErrorCorrectionLevel()
                    }
                };

                if (qrPreview.Image != null)
                {
                    qrPreview.Image.Dispose();
                }

                qrPreview.Image = writer.Write(content);
                previewLabel.Text = $"Size: {content.Length} characters";
            }
            catch (Exception ex)
            {
                previewLabel.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error generating QR code: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int GetQRCodeSize()
        {
            switch (sizeComboBox.SelectedItem?.ToString())
            {
                case "Small": return 200;
                case "Large": return 400;
                default: return 300; // Medium
            }
        }

        private ZXing.QrCode.Internal.ErrorCorrectionLevel GetErrorCorrectionLevel()
        {
            switch (errorCorrectionComboBox.SelectedItem?.ToString())
            {
                case "Low": return ZXing.QrCode.Internal.ErrorCorrectionLevel.L;
                case "High": return ZXing.QrCode.Internal.ErrorCorrectionLevel.H;
                default: return ZXing.QrCode.Internal.ErrorCorrectionLevel.M;
            }
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

        private void SaveTemplateButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(contentTextBox.Text))
            {
                MessageBox.Show("Please enter template content first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string templateName = Microsoft.VisualBasic.Interaction.InputBox("Enter template name:", "Save Template", "");
            if (string.IsNullOrWhiteSpace(templateName))
                return;

            if (templateName.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot save as 'MAIN'. Use 'Update MAIN' button instead.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (savedTemplates.ContainsKey(templateName))
            {
                if (MessageBox.Show($"Template '{templateName}' already exists. Do you want to overwrite it?",
                    "Confirm Overwrite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            savedTemplates[templateName] = contentTextBox.Text;
            SaveTemplates();

            if (!templateComboBox.Items.Contains(templateName))
            {
                templateComboBox.Items.Add(templateName);
            }

            templateComboBox.SelectedItem = templateName;
            MessageBox.Show("Template saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PrintButton_Click(object sender, EventArgs e)
        {
            if (qrPreview.Image == null)
            {
                MessageBox.Show("Please generate a QR code first.", "No QR Code", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var printDialog = new PrintDialog())
            {
                try
                {
                    // Create a PrintDocument for printing
                    var printDocument = new PrintDocument();
                    printDocument.DocumentName = $"QR Code - {currentItem?.ModelNumber ?? "Custom"}";
                    
                    // Set the PrintDocument to the PrintDialog
                    printDialog.Document = printDocument;
                    
                    // Store a local reference to the QR image to avoid threading issues
                    var qrImageToPrint = qrPreview.Image;
                    
                    // Handle the printing
                    printDocument.PrintPage += (sender, e) => 
                    {
                        // Calculate print area
                        float pageWidth = e.PageSettings.PrintableArea.Width;
                        float pageHeight = e.PageSettings.PrintableArea.Height;
                        
                        // Set high quality rendering
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        
                        // Calculate centering positions
                        float xPos = (pageWidth - qrImageToPrint.Width) / 2;
                        float yPos = (pageHeight - qrImageToPrint.Height) / 2;
                        
                        // Ensure the QR code is at least 1 inch in size
                        float minSize = 100f; // 1 inch = 100 units at 100 DPI
                        float maxSize = Math.Min(pageWidth * 0.8f, pageHeight * 0.8f); // 80% of the page
                        
                        float qrSize = Math.Max(minSize, Math.Min(maxSize, 300f)); // Default to 3 inches, but within bounds
                        
                        // Create a destination rectangle that centers the QR code on the page
                        RectangleF destRect = new RectangleF(
                            (pageWidth - qrSize) / 2,
                            (pageHeight - qrSize) / 2,
                            qrSize,
                            qrSize
                        );
                        
                        // Draw the QR code
                        e.Graphics.DrawImage(qrImageToPrint, destRect);
                        
                        // Add content info below the QR code
                        if (contentTextBox.Text.Length > 0)
                        {
                            string displayText = contentTextBox.Text;
                            if (displayText.Length > 50)
                            {
                                displayText = displayText.Substring(0, 47) + "...";
                            }
                            
                            using (Font font = new Font("Arial", 8))
                            {
                                RectangleF textRect = new RectangleF(
                                    (pageWidth - qrSize) / 2,
                                    destRect.Bottom + 10,
                                    qrSize,
                                    40
                                );
                                
                                StringFormat format = new StringFormat
                                {
                                    Alignment = StringAlignment.Center,
                                    LineAlignment = StringAlignment.Near
                                };
                                
                                e.Graphics.DrawString(displayText, font, Brushes.Black, textRect, format);
                            }
                        }
                        
                        // No more pages
                        e.HasMorePages = false;
                    };
                    
                    // Show the print dialog
                    if (printDialog.ShowDialog() == DialogResult.OK)
                    {
                        printDocument.Print();
                        MessageBox.Show("QR code sent to printer!", "Print Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during print operation: {ex.Message}", "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void GenerateButton_Click(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void AvailableFieldsListBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = availableFieldsListBox.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    string fieldName = availableFieldsListBox.Items[index].ToString();

                    // If "Custom Text..." is selected, get user text; otherwise use the fieldName directly.
                    if (fieldName == "Custom Text...")
                    {
                        using (var dialog = new TextInputDialog("Enter Custom Text", "Enter the text you want to add:"))
                        {
                            if (dialog.ShowDialog() == DialogResult.OK)
                            {
                                fieldName = dialog.InputText;
                            }
                            else return;
                        }
                    }

                    // Wrap fieldName as DataObject with UnicodeText
                    DataObject dataObject = new DataObject(DataFormats.UnicodeText, fieldName);
                    availableFieldsListBox.DoDragDrop(dataObject, DragDropEffects.Copy);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (qrPreview.Image != null)
            {
                qrPreview.Image.Dispose();
            }
            SaveTemplates(); // Save templates when closing
        }

        private void UpdateMainButton_Click(object sender, EventArgs e)
        {
            string selectedTemplate = templateComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedTemplate))
            {
                MessageBox.Show("Please select a template to update.", "No Template Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (selectedTemplate.Equals("-------------------"))
            {
                MessageBox.Show("Please select a valid template to update.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string confirmMessage = selectedTemplate.Equals("MAIN", StringComparison.OrdinalIgnoreCase)
                ? "Are you sure you want to update the MAIN template? This will overwrite the existing MAIN template."
                : $"Are you sure you want to update the '{selectedTemplate}' template? This will overwrite the existing template.";

            if (MessageBox.Show(confirmMessage, "Confirm Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            // Save the current content to the selected template
            savedTemplates[selectedTemplate] = contentTextBox.Text;
            SaveTemplates();

            MessageBox.Show($"Template '{selectedTemplate}' updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
        }

        private void DeleteTemplateButton_Click(object sender, EventArgs e)
        {
            string templateName = templateComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(templateName))
                return;

            if (templateName.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot delete the MAIN template.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete the template '{templateName}'?",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                savedTemplates.Remove(templateName);
                SaveTemplates();
                templateComboBox.Items.Remove(templateName);
                templateComboBox.SelectedIndex = 0;
                MessageBox.Show("Template deleted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    public class TextInputDialog : Form
    {
        private TextBox inputTextBox;
        public string InputText => inputTextBox.Text;

        public TextInputDialog(string title, string prompt)
        {
            this.Text = title;
            this.Size = new Size(300, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(10)
            };

            Label promptLabel = new Label
            {
                Text = prompt,
                AutoSize = true
            };
            layout.Controls.Add(promptLabel, 0, 0);

            inputTextBox = new TextBox
            {
                Dock = DockStyle.Fill
            };
            layout.Controls.Add(inputTextBox, 0, 1);

            Panel buttonPanel = new Panel
            {
                Height = 40
            };

            Button okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 75,
                Location = new Point(buttonPanel.Width - 160, 5)
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 75,
                Location = new Point(buttonPanel.Width - 80, 5)
            };

            buttonPanel.Controls.AddRange(new Control[] { okButton, cancelButton });
            layout.Controls.Add(buttonPanel, 0, 2);

            this.Controls.Add(layout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
} 