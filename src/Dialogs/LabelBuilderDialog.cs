using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using PrintSystem.Models;
using PrintSystem.Managers;
using System.Text.Json;
using System.IO;
using ZXing;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace PrintSystem.Dialogs
{
    public class LabelTemplate
    {
        public string Name { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public List<LabelElementInfo> Elements { get; set; } = new List<LabelElementInfo>();
    }

    public class LabelElementInfo
    {
        public string Type { get; set; }  // "Text", "QR", "Image"
        public string Content { get; set; }
        public Rectangle Bounds { get; set; }
        public string FontFamily { get; set; }
        public int FontSize { get; set; }
    }

    public class LabelBuilderDialog : Form
    {
        private Panel designCanvas;
        private Panel toolbox;
        private ListBox availableFieldsListBox;
        private ComboBox templateComboBox;
        private Button saveTemplateButton;
        private Button loadTemplateButton;
        private Button printButton;
        private Button generateQRButton;
        private NumericUpDown labelWidthInput;
        private NumericUpDown labelHeightInput;
        private ComboBox fontSizeComboBox;
        private ComboBox fontFamilyComboBox;
        private ComboBox qrTemplateComboBox;

        // Store the current item if we're designing from an item context
        private Item currentItem;
        
        // Store label elements
        private List<LabelElement> labelElements;
        private LabelElement selectedElement;
        private Point dragStartPoint;
        private bool isDragging;

        // Store QR templates
        private Dictionary<string, string> qrTemplates = new Dictionary<string, string>();
        private const string QR_TEMPLATES_FILE = "qr_templates.json";

        // Store label templates
        private Dictionary<string, LabelTemplate> labelTemplates = new Dictionary<string, LabelTemplate>();
        private const string LABEL_TEMPLATES_FILE = "label_templates.json";

        // Constants for design
        private const int GRID_SIZE = 10;
        private const int MIN_ELEMENT_SIZE = 20;
        private const double DPI_SCALE = 3.779528;  // 96 DPI conversion factor
        private const int MARGIN = 40; // Margin for scale indicators

        public LabelBuilderDialog(Item item = null)
        {
            currentItem = item;
            labelElements = new List<LabelElement>();
            LoadQRTemplates();
            LoadLabelTemplates();
            InitializeComponents();
            if (currentItem != null)
            {
                PopulateFieldsWithItem();
            }
        }

        private void LoadQRTemplates()
        {
            try
            {
                if (File.Exists(QR_TEMPLATES_FILE))
                {
                    string json = File.ReadAllText(QR_TEMPLATES_FILE);
                    qrTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading QR templates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                qrTemplates = new Dictionary<string, string>();
            }
        }

        private void LoadLabelTemplates()
        {
            try
            {
                if (File.Exists(LABEL_TEMPLATES_FILE))
                {
                    string json = File.ReadAllText(LABEL_TEMPLATES_FILE);
                    var templates = JsonSerializer.Deserialize<Dictionary<string, LabelTemplate>>(json);
                    
                    // Initialize templates dictionary if null
                    labelTemplates = templates ?? new Dictionary<string, LabelTemplate>();

                    // Ensure MAIN template exists and has valid values
                    if (!labelTemplates.ContainsKey("MAIN") || labelTemplates["MAIN"] == null)
                    {
                        labelTemplates["MAIN"] = CreateDefaultMainTemplate();
                        SaveLabelTemplates();
                    }
                    else
                    {
                        // Validate MAIN template
                        var mainTemplate = labelTemplates["MAIN"];
                        if (mainTemplate.Elements == null)
                        {
                            mainTemplate.Elements = new List<LabelElementInfo>();
                        }
                        if (mainTemplate.Width <= 0)
                        {
                            mainTemplate.Width = 100;
                        }
                        if (mainTemplate.Height <= 0)
                        {
                            mainTemplate.Height = 50;
                        }
                    }
                }
                else
                {
                    // Create new templates dictionary with default MAIN template
                    labelTemplates = new Dictionary<string, LabelTemplate>
                    {
                        ["MAIN"] = CreateDefaultMainTemplate()
                    };
                    SaveLabelTemplates();
                }

                // Update template combo box
                if (templateComboBox != null)
                {
                    templateComboBox.Items.Clear();
                    templateComboBox.Items.Add("MAIN");
                    foreach (string templateName in labelTemplates.Keys.Where(k => k != "MAIN"))
                    {
                        templateComboBox.Items.Add(templateName);
                    }

                    // Select MAIN template by default
                    if (templateComboBox.Items.Count > 0)
                    {
                        templateComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading label templates: {ex.Message}\nDefault templates will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                labelTemplates = new Dictionary<string, LabelTemplate>
                {
                    ["MAIN"] = CreateDefaultMainTemplate()
                };
            }
        }

        private LabelTemplate CreateDefaultMainTemplate()
        {
            return new LabelTemplate
            {
                Name = "MAIN",
                Width = 100,
                Height = 50,
                Elements = new List<LabelElementInfo>()
            };
        }

        private void InitializeComponents()
        {
            this.Text = "Label Builder";
            this.Size = new Size(1200, 800);
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
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Left panel for toolbox
            toolbox = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            TableLayoutPanel toolboxLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 6,
                ColumnCount = 1,
                Padding = new Padding(0)
            };

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
            // Remove placeholder templates - only load saved templates
            foreach (string templateName in labelTemplates.Keys)
            {
                templateComboBox.Items.Add(templateName);
            }

            // Add template management buttons
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
            toolboxLayout.Controls.Add(templateGroup, 0, 0);

            // Label size section
            GroupBox sizeGroup = new GroupBox
            {
                Text = "Label Size (mm)",
                Dock = DockStyle.Fill,
                Height = 80,
                Padding = new Padding(5)
            };

            TableLayoutPanel sizeLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2
            };

            sizeLayout.Controls.Add(new Label { Text = "Width:", Anchor = AnchorStyles.Left }, 0, 0);
            labelWidthInput = new NumericUpDown
            {
                Minimum = 20,
                Maximum = 300,
                Value = 100,
                DecimalPlaces = 1
            };
            labelWidthInput.ValueChanged += (s, e) => UpdateCanvasSize();
            sizeLayout.Controls.Add(labelWidthInput, 1, 0);

            sizeLayout.Controls.Add(new Label { Text = "Height:", Anchor = AnchorStyles.Left }, 0, 1);
            labelHeightInput = new NumericUpDown
            {
                Minimum = 20,
                Maximum = 300,
                Value = 50,
                DecimalPlaces = 1
            };
            labelHeightInput.ValueChanged += (s, e) => UpdateCanvasSize();
            sizeLayout.Controls.Add(labelHeightInput, 1, 1);

            sizeGroup.Controls.Add(sizeLayout);
            toolboxLayout.Controls.Add(sizeGroup, 0, 1);

            // Font settings section
            GroupBox fontGroup = new GroupBox
            {
                Text = "Font Settings",
                Dock = DockStyle.Fill,
                Height = 80,
                Padding = new Padding(5)
            };

            TableLayoutPanel fontLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2
            };

            fontLayout.Controls.Add(new Label { Text = "Font:", Anchor = AnchorStyles.Left }, 0, 0);
            fontFamilyComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var family in FontFamily.Families)
            {
                fontFamilyComboBox.Items.Add(family.Name);
            }
            fontFamilyComboBox.SelectedItem = "Arial";
            fontLayout.Controls.Add(fontFamilyComboBox, 1, 0);

            fontLayout.Controls.Add(new Label { Text = "Size:", Anchor = AnchorStyles.Left }, 0, 1);
            fontSizeComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            fontSizeComboBox.Items.AddRange(new object[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 });
            fontSizeComboBox.SelectedItem = 12;
            fontLayout.Controls.Add(fontSizeComboBox, 1, 1);

            fontGroup.Controls.Add(fontLayout);
            toolboxLayout.Controls.Add(fontGroup, 0, 2);

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
            availableFieldsListBox.MouseDown += AvailableFieldsListBox_MouseDown;
            fieldsGroup.Controls.Add(availableFieldsListBox);
            toolboxLayout.Controls.Add(fieldsGroup, 0, 3);

            // QR Code section
            GroupBox qrGroup = new GroupBox
            {
                Text = "QR Code",
                Dock = DockStyle.Fill,
                Height = 100,
                Padding = new Padding(5)
            };

            TableLayoutPanel qrLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(0)
            };

            // QR Template dropdown
            qrTemplateComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, 5)
            };
            // Only add saved templates
            foreach (string templateName in qrTemplates.Keys)
            {
                qrTemplateComboBox.Items.Add(templateName);
            }
            if (qrTemplateComboBox.Items.Count > 0)
            {
                qrTemplateComboBox.SelectedIndex = 0;
            }

            generateQRButton = new Button
            {
                Text = "Add QR Code",
                Dock = DockStyle.Fill
            };
            generateQRButton.Click += GenerateQRButton_Click;
            qrLayout.Controls.Add(qrTemplateComboBox, 0, 0);

            qrLayout.Controls.Add(generateQRButton, 0, 1);

            qrGroup.Controls.Add(qrLayout);
            toolboxLayout.Controls.Add(qrGroup, 0, 4);

            toolbox.Controls.Add(toolboxLayout);

            // Right panel for design canvas
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            TableLayoutPanel canvasLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(0)
            };
            canvasLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            canvasLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // Design canvas
            designCanvas = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            designCanvas.Paint += DesignCanvas_Paint;
            designCanvas.MouseDown += DesignCanvas_MouseDown;
            designCanvas.MouseMove += DesignCanvas_MouseMove;
            designCanvas.MouseUp += DesignCanvas_MouseUp;
            designCanvas.AllowDrop = true;
            designCanvas.DragEnter += DesignCanvas_DragEnter;
            designCanvas.DragDrop += DesignCanvas_DragDrop;
            canvasLayout.Controls.Add(designCanvas, 0, 0);

            // Bottom panel for action buttons
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40
            };

            // OK Button
            Button okButton = new Button
            {
                Text = "OK",
                Width = 80,
                Height = 30,
                Location = new Point(bottomPanel.Width - 200, 5),
                Anchor = AnchorStyles.Right
            };
            okButton.Click += (s, e) => 
            {
                SaveLabelTemplates();
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            printButton = new Button
            {
                Text = "Print Preview",
                Width = 100,
                Height = 30,
                Location = new Point(bottomPanel.Width - 110, 5),
                Anchor = AnchorStyles.Right
            };
            printButton.Click += PrintButton_Click;

            bottomPanel.Controls.Add(okButton);
            bottomPanel.Controls.Add(printButton);
            canvasLayout.Controls.Add(bottomPanel, 0, 1);

            rightPanel.Controls.Add(canvasLayout);

            // Add panels to main layout
            mainLayout.Controls.Add(toolbox, 0, 0);
            mainLayout.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainLayout);

            // Only set initial template if there are items
            if (templateComboBox.Items.Count > 0)
            {
                templateComboBox.SelectedIndex = 0;
            }
            
            // Initialize canvas size
            UpdateCanvasSize();
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
                "Image"
            });
        }

        private void PopulateFieldsWithItem()
        {
            if (currentItem == null) return;

            // Load default label type from settings
            var settings = SettingsManager.GetSettings();
            string defaultType = settings.DefaultLabelType;
            
            // Find and select the template
            for (int i = 0; i < templateComboBox.Items.Count; i++)
            {
                if (templateComboBox.Items[i].ToString() == defaultType)
                {
                    templateComboBox.SelectedIndex = i;
                    return;
                }
            }
            
            // If not found, select the first template
            templateComboBox.SelectedIndex = 0;
        }

        private void UpdateCanvasSize()
        {
            // Convert mm to pixels (96 DPI) and add margins
            int width = (int)((double)labelWidthInput.Value * DPI_SCALE) + (MARGIN * 2);
            int height = (int)((double)labelHeightInput.Value * DPI_SCALE) + (MARGIN * 2);

            // Set minimum size
            width = Math.Max(width, 100);
            height = Math.Max(height, 50);

            // Update canvas size
            designCanvas.Size = new Size(width, height);
            designCanvas.Invalidate();
        }

        private void DesignCanvas_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Calculate actual label size in pixels
            int labelWidth = (int)((double)labelWidthInput.Value * DPI_SCALE);
            int labelHeight = (int)((double)labelHeightInput.Value * DPI_SCALE);
            
            // Calculate label area (excluding margins)
            Rectangle labelArea = new Rectangle(
                MARGIN, 
                MARGIN, 
                labelWidth,  // Use actual label width
                labelHeight  // Use actual label height
            );
            
            // Draw outer canvas background
            e.Graphics.FillRectangle(Brushes.WhiteSmoke, 0, 0, designCanvas.Width, designCanvas.Height);
            
            // Draw label area background
            e.Graphics.FillRectangle(Brushes.White, labelArea);

            // Draw scale indicators
            using (var scalePen = new Pen(Color.Black, 1))
            {
                // Draw tick marks every 10mm
                float mmToPixels = (float)DPI_SCALE;
                int tickLength = 5;
                int numberOffset = tickLength + 3; // Offset for numbers from ticks
                Font scaleFont = new Font("Arial", 7);

                // Draw horizontal scale (width)
                float maxWidth = (float)labelWidthInput.Value;
                for (float mm = 0; mm <= maxWidth; mm += 10)
                {
                    int x = MARGIN + (int)(mm * mmToPixels);
                    // Draw top ticks
                    e.Graphics.DrawLine(scalePen, x, 0, x, tickLength); // Top ticks
                    
                    if (mm > 0) // Draw mm labels
                    {
                        string label = mm.ToString();
                        SizeF labelSize = e.Graphics.MeasureString(label, scaleFont);
                        e.Graphics.DrawString(label, scaleFont, Brushes.Black, 
                            x - labelSize.Width/2, numberOffset); // Position numbers after ticks
                    }
                }

                // Draw vertical scale (height)
                float maxHeight = (float)labelHeightInput.Value;
                for (float mm = 0; mm <= maxHeight; mm += 10)
                {
                    int y = MARGIN + (int)(mm * mmToPixels);
                    // Draw left ticks
                    e.Graphics.DrawLine(scalePen, 0, y, tickLength, y); // Left ticks
                    
                    if (mm > 0) // Draw mm labels
                    {
                        string label = mm.ToString();
                        SizeF labelSize = e.Graphics.MeasureString(label, scaleFont);
                        e.Graphics.DrawString(label, scaleFont, Brushes.Black, 
                            numberOffset, y - labelSize.Height/2); // Position numbers after ticks
                    }
                }

                // Draw scale border lines
                e.Graphics.DrawLine(scalePen, MARGIN, 0, MARGIN, MARGIN); // Vertical scale border
                e.Graphics.DrawLine(scalePen, 0, MARGIN, MARGIN, MARGIN); // Horizontal scale border

                scaleFont.Dispose();
            }
            
            // Set clipping region to label area for grid and elements
            e.Graphics.SetClip(labelArea);
            
            // Draw grid
            using (var gridPen = new Pen(Color.LightGray, 1))
            {
                gridPen.DashStyle = DashStyle.Dot;
                
                // Draw vertical lines
                for (int x = MARGIN; x < labelArea.Right; x += GRID_SIZE)
                {
                    e.Graphics.DrawLine(gridPen, x, labelArea.Top, x, labelArea.Bottom);
                }
                
                // Draw horizontal lines
                for (int y = MARGIN; y < labelArea.Bottom; y += GRID_SIZE)
                {
                    e.Graphics.DrawLine(gridPen, labelArea.Left, y, labelArea.Right, y);
                }
            }

            // Draw all elements
            foreach (var element in labelElements)
            {
                element.Draw(e.Graphics);
                
                // Draw selection rectangle if selected
                if (element == selectedElement)
                {
                    using (var selectionPen = new Pen(Color.Blue, 1))
                    {
                        selectionPen.DashStyle = DashStyle.Dash;
                        e.Graphics.DrawRectangle(selectionPen, element.Bounds);
                        
                        // Draw resize handles
                        var handles = element.GetResizeHandles();
                        foreach (var handle in handles)
                        {
                            e.Graphics.FillRectangle(Brushes.White, handle);
                            e.Graphics.DrawRectangle(Pens.Blue, handle);
                        }
                    }
                }
            }
            
            // Reset clipping region
            e.Graphics.ResetClip();

            // Draw label boundary with a thick border
            using (var borderPen = new Pen(Color.Black, 2))
            {
                e.Graphics.DrawRectangle(borderPen, labelArea);
            }
        }

        private void DesignCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            dragStartPoint = e.Location;
            
            // Check if clicking on a resize handle of selected element
            if (selectedElement != null)
            {
                var handles = selectedElement.GetResizeHandles();
                for (int i = 0; i < handles.Length; i++)
                {
                    if (handles[i].Contains(e.Location))
                    {
                        selectedElement.StartResize(i, e.Location);
                        isDragging = true;
                        return;
                    }
                }
            }
            
            // Check if clicking on an element
            selectedElement = null;
            for (int i = labelElements.Count - 1; i >= 0; i--)
            {
                if (labelElements[i].Bounds.Contains(e.Location))
                {
                    selectedElement = labelElements[i];
                    isDragging = true;
                    selectedElement.StartDrag(e.Location);
                    labelElements.RemoveAt(i);
                    labelElements.Add(selectedElement); // Move to top
                    break;
                }
            }
            
            designCanvas.Invalidate();
        }

        private void DesignCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && selectedElement != null)
            {
                if (selectedElement.IsResizing)
                {
                    selectedElement.Resize(e.Location);
                }
                else
                {
                    selectedElement.Drag(e.Location);
                }
                designCanvas.Invalidate();
            }
        }

        private void DesignCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (selectedElement != null)
            {
                selectedElement.EndDragOrResize();
            }
            isDragging = false;
        }

        private void DesignCanvas_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void DesignCanvas_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string fieldName = (string)e.Data.GetData(DataFormats.StringFormat);
                Point clientPoint = designCanvas.PointToClient(new Point(e.X, e.Y));
                
                // Snap to grid
                clientPoint.X = (clientPoint.X / GRID_SIZE) * GRID_SIZE;
                clientPoint.Y = (clientPoint.Y / GRID_SIZE) * GRID_SIZE;

                AddLabelElement(fieldName, clientPoint);
            }
        }

        private void AvailableFieldsListBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = availableFieldsListBox.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    string fieldName = availableFieldsListBox.Items[index].ToString();
                    
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
                    
                    availableFieldsListBox.DoDragDrop(fieldName, DragDropEffects.Copy);
                }
            }
        }

        private void AddLabelElement(string fieldName, Point location)
        {
            LabelElement element;
            
            if (fieldName == "Image" && currentItem?.ImagePath != null)
            {
                element = new ImageElement(currentItem.ImagePath, location);
            }
            else
            {
                string text = GetFieldValue(fieldName);
                element = new TextElement(text, location, 
                    (string)fontFamilyComboBox.SelectedItem, 
                    (int)fontSizeComboBox.SelectedItem);
            }
            
            labelElements.Add(element);
            selectedElement = element;
            designCanvas.Invalidate();
        }

        private string GetFieldValue(string fieldName)
        {
            if (currentItem == null) return fieldName;

            switch (fieldName)
            {
                case "Model Number":
                    return currentItem.ModelNumber;
                case "Description":
                    return currentItem.Description;
                case "Supplier":
                    return currentItem.Supplier;
                case "Category":
                    return currentItem.CategoryPath;
                case "Default Order Quantity":
                    return currentItem.DefaultOrderQuantity.ToString();
                case "Product URL":
                    return currentItem.ProductUrl;
                default:
                    return fieldName;
            }
        }

        private void GenerateQRButton_Click(object sender, EventArgs e)
        {
            string template = qrTemplateComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(template)) return;

            string content;
            if (qrTemplates.ContainsKey(template))
            {
                content = qrTemplates[template];
                // Replace placeholders with actual values or keep placeholders if no item
                if (currentItem != null)
                {
                    content = content.Replace("{ModelNumber}", currentItem.ModelNumber ?? "")
                                   .Replace("{Description}", currentItem.Description ?? "")
                                   .Replace("{Supplier}", currentItem.Supplier ?? "")
                                   .Replace("{Category}", currentItem.CategoryPath ?? "Uncategorized")
                                   .Replace("{DefaultOrderQuantity}", currentItem.DefaultOrderQuantity.ToString())
                                   .Replace("{ProductURL}", currentItem.ProductUrl ?? "");
                }
                // If no item, keep the placeholders to show template structure
            }
            else
            {
                // Handle built-in templates
                switch (template)
                {
                    case "Basic Info":
                        content = currentItem != null
                            ? $"Model: {currentItem.ModelNumber ?? ""} | Description: {currentItem.Description ?? ""}"
                            : "Model: {ModelNumber} | Description: {Description}";
                        break;

                    case "Full Details":
                        content = currentItem != null
                            ? $"Model: {currentItem.ModelNumber ?? ""}"
                              + $" | Description: {currentItem.Description ?? ""}"
                              + $" | Supplier: {currentItem.Supplier ?? ""}"
                              + $" | Category: {currentItem.CategoryPath ?? "Uncategorized"}"
                              + $" | Order Qty: {currentItem.DefaultOrderQuantity}"
                              + $" | URL: {currentItem.ProductUrl ?? ""}"
                            : "Model: {ModelNumber}"
                              + " | Description: {Description}"
                              + " | Supplier: {Supplier}"
                              + " | Category: {Category}"
                              + " | Order Qty: {DefaultOrderQuantity}"
                              + " | URL: {ProductURL}";
                        break;

                    case "URL Only":
                        content = currentItem?.ProductUrl ?? "{ProductURL}";
                        break;

                    default:
                        MessageBox.Show("Please select a valid template.", "Invalid Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                }
            }

            // Ensure content is not empty
            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("Cannot generate QR code with empty content.", "Invalid Content", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Generate QR code
            try
            {
                var writer = new BarcodeWriter<Bitmap>
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new QrCodeEncodingOptions
                    {
                        Width = 300,  // Medium size
                        Height = 300,
                        Margin = 1,
                        ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                    },
                    Renderer = new BitmapRenderer()
                };

                Image qrImage = writer.Write(content);
                Point location = new Point(GRID_SIZE, GRID_SIZE);
                var qrElement = new QRElement(qrImage, location);
                labelElements.Add(qrElement);
                selectedElement = qrElement;
                designCanvas.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating QR code: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadTemplateButton_Click(object sender, EventArgs e)
        {
            string templateName = templateComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(templateName))
            {
                MessageBox.Show("Please select a template to load.", "No Template Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!labelTemplates.ContainsKey(templateName))
            {
                MessageBox.Show($"Template '{templateName}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var template = labelTemplates[templateName];

                // Clear existing elements
                foreach (var element in labelElements)
                {
                    element.Dispose();
                }
                labelElements.Clear();

                // Set dimensions
                labelWidthInput.Value = template.Width;
                labelHeightInput.Value = template.Height;

                // Load elements
                foreach (var elementInfo in template.Elements)
                {
                    LabelElement element = null;
                    switch (elementInfo.Type)
                    {
                        case "Text":
                            element = new TextElement(elementInfo.Content, 
                                new Point(elementInfo.Bounds.X, elementInfo.Bounds.Y),
                                elementInfo.FontFamily,
                                elementInfo.FontSize);
                            break;

                        case "QR":
                            try
                            {
                                var writer = new BarcodeWriter<Bitmap>
                                {
                                    Format = BarcodeFormat.QR_CODE,
                                    Options = new QrCodeEncodingOptions
                                    {
                                        Width = 300,
                                        Height = 300,
                                        Margin = 1,
                                        ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                                    },
                                    Renderer = new BitmapRenderer()
                                };

                                string qrContent = elementInfo.Content;
                                if (currentItem != null)
                                {
                                    qrContent = qrContent.Replace("{ModelNumber}", currentItem.ModelNumber ?? "")
                                                       .Replace("{Description}", currentItem.Description ?? "")
                                                       .Replace("{Supplier}", currentItem.Supplier ?? "")
                                                       .Replace("{Category}", currentItem.CategoryPath ?? "Uncategorized")
                                                       .Replace("{DefaultOrderQuantity}", currentItem.DefaultOrderQuantity.ToString())
                                                       .Replace("{ProductURL}", currentItem.ProductUrl ?? "");
                                }

                                Image qrImage = writer.Write(qrContent);
                                element = new QRElement(qrImage, new Point(elementInfo.Bounds.X, elementInfo.Bounds.Y));
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error regenerating QR code: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                continue;
                            }
                            break;

                        case "Image":
                            if (File.Exists(elementInfo.Content))
                            {
                                element = new ImageElement(elementInfo.Content, new Point(elementInfo.Bounds.X, elementInfo.Bounds.Y));
                            }
                            else
                            {
                                MessageBox.Show($"Image file not found: {elementInfo.Content}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                continue;
                            }
                            break;
                    }

                    if (element != null)
                    {
                        element.Bounds = elementInfo.Bounds;
                        labelElements.Add(element);
                    }
                }

                designCanvas.Invalidate();
                MessageBox.Show("Template loaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading template: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveTemplateButton_Click(object sender, EventArgs e)
        {
            if (labelElements.Count == 0)
            {
                MessageBox.Show("Please add some elements to the label before saving the template.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            if (labelTemplates.ContainsKey(templateName))
            {
                if (MessageBox.Show($"Template '{templateName}' already exists. Do you want to overwrite it?",
                    "Confirm Overwrite", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            // Create template
            var template = new LabelTemplate
            {
                Name = templateName,
                Width = labelWidthInput.Value,
                Height = labelHeightInput.Value,
                Elements = new List<LabelElementInfo>()
            };

            // Save elements
            foreach (var element in labelElements)
            {
                var elementInfo = new LabelElementInfo
                {
                    Bounds = element.Bounds
                };

                if (element is TextElement textElement)
                {
                    elementInfo.Type = "Text";
                    elementInfo.Content = textElement.GetText();
                    elementInfo.FontFamily = textElement.GetFontFamily();
                    elementInfo.FontSize = textElement.GetFontSize();
                }
                else if (element is QRElement)
                {
                    elementInfo.Type = "QR";
                    elementInfo.Content = qrTemplateComboBox.SelectedItem?.ToString() ?? "Basic Info";
                }
                else if (element is ImageElement imageElement)
                {
                    elementInfo.Type = "Image";
                    elementInfo.Content = imageElement.GetImagePath();
                }

                template.Elements.Add(elementInfo);
            }

            // Save template
            labelTemplates[templateName] = template;
            SaveLabelTemplates();

            // Add to combo box if not exists
            if (!templateComboBox.Items.Contains(templateName))
            {
                templateComboBox.Items.Add(templateName);
            }

            templateComboBox.SelectedItem = templateName;
            MessageBox.Show("Template saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveLabelTemplates()
        {
            try
            {
                // Ensure we have valid templates before saving
                if (labelTemplates == null)
                {
                    labelTemplates = new Dictionary<string, LabelTemplate>
                    {
                        ["MAIN"] = CreateDefaultMainTemplate()
                    };
                }

                // Validate all templates before saving
                foreach (var template in labelTemplates.Values)
                {
                    if (template.Elements == null)
                    {
                        template.Elements = new List<LabelElementInfo>();
                    }
                    if (template.Width <= 0)
                    {
                        template.Width = 100;
                    }
                    if (template.Height <= 0)
                    {
                        template.Height = 50;
                    }
                }

                string json = JsonSerializer.Serialize(labelTemplates, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                File.WriteAllText(LABEL_TEMPLATES_FILE, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving label templates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PrintButton_Click(object sender, EventArgs e)
        {
            // TODO: Implement print preview and printing
            MessageBox.Show("Print preview will be implemented in a future update.", "Coming Soon");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            foreach (var element in labelElements)
            {
                element.Dispose();
            }
            SaveLabelTemplates();
        }

        private void UpdateMainButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to update the MAIN template? This will overwrite the existing MAIN template.",
                "Confirm Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            // Create template
            var template = new LabelTemplate
            {
                Name = "MAIN",
                Width = labelWidthInput.Value,
                Height = labelHeightInput.Value,
                Elements = new List<LabelElementInfo>()
            };

            // Save elements
            foreach (var element in labelElements)
            {
                var elementInfo = new LabelElementInfo
                {
                    Bounds = element.Bounds
                };

                if (element is TextElement textElement)
                {
                    elementInfo.Type = "Text";
                    elementInfo.Content = textElement.GetText();
                    elementInfo.FontFamily = textElement.GetFontFamily();
                    elementInfo.FontSize = textElement.GetFontSize();
                }
                else if (element is QRElement)
                {
                    elementInfo.Type = "QR";
                    elementInfo.Content = qrTemplateComboBox.SelectedItem?.ToString() ?? "Basic Info";
                }
                else if (element is ImageElement imageElement)
                {
                    elementInfo.Type = "Image";
                    elementInfo.Content = imageElement.GetImagePath();
                }

                template.Elements.Add(elementInfo);
            }

            // Save template
            labelTemplates["MAIN"] = template;
            SaveLabelTemplates();

            // Add to combo box if not exists
            if (!templateComboBox.Items.Contains("MAIN"))
            {
                templateComboBox.Items.Insert(0, "MAIN");
            }

            MessageBox.Show("MAIN template updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                labelTemplates.Remove(templateName);
                SaveLabelTemplates();
                templateComboBox.Items.Remove(templateName);
                templateComboBox.SelectedIndex = 0;
                MessageBox.Show("Template deleted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    // Label element classes
    public abstract class LabelElement : IDisposable
    {
        protected Rectangle bounds;
        protected Point dragOffset;
        protected bool isResizing;
        protected int resizeHandle = -1;
        
        public Rectangle Bounds 
        { 
            get => bounds;
            set => bounds = value;
        }
        public bool IsResizing => isResizing;

        public abstract void Draw(Graphics g);
        
        public virtual void StartDrag(Point mouseLocation)
        {
            dragOffset = new Point(
                mouseLocation.X - bounds.X,
                mouseLocation.Y - bounds.Y
            );
        }
        
        public virtual void Drag(Point mouseLocation)
        {
            bounds.X = mouseLocation.X - dragOffset.X;
            bounds.Y = mouseLocation.Y - dragOffset.Y;
            
            // Snap to grid
            bounds.X = (bounds.X / 10) * 10;
            bounds.Y = (bounds.Y / 10) * 10;
        }
        
        public virtual void StartResize(int handle, Point mouseLocation)
        {
            isResizing = true;
            resizeHandle = handle;
            dragOffset = mouseLocation;
        }
        
        public virtual void Resize(Point mouseLocation)
        {
            if (!isResizing) return;

            int dx = mouseLocation.X - dragOffset.X;
            int dy = mouseLocation.Y - dragOffset.Y;
            
            switch (resizeHandle)
            {
                case 0: // Top-left
                    bounds.X += dx;
                    bounds.Y += dy;
                    bounds.Width -= dx;
                    bounds.Height -= dy;
                    break;
                case 1: // Top-right
                    bounds.Y += dy;
                    bounds.Width += dx;
                    bounds.Height -= dy;
                    break;
                case 2: // Bottom-right
                    bounds.Width += dx;
                    bounds.Height += dy;
                    break;
                case 3: // Bottom-left
                    bounds.X += dx;
                    bounds.Width -= dx;
                    bounds.Height += dy;
                    break;
            }
            
            // Ensure minimum size
            if (bounds.Width < 20) bounds.Width = 20;
            if (bounds.Height < 20) bounds.Height = 20;
            
            dragOffset = mouseLocation;
        }
        
        public virtual void EndDragOrResize()
        {
            isResizing = false;
            resizeHandle = -1;
        }
        
        public Rectangle[] GetResizeHandles()
        {
            const int handleSize = 6;
            return new Rectangle[]
            {
                new Rectangle(bounds.Left - handleSize/2, bounds.Top - handleSize/2, handleSize, handleSize),
                new Rectangle(bounds.Right - handleSize/2, bounds.Top - handleSize/2, handleSize, handleSize),
                new Rectangle(bounds.Right - handleSize/2, bounds.Bottom - handleSize/2, handleSize, handleSize),
                new Rectangle(bounds.Left - handleSize/2, bounds.Bottom - handleSize/2, handleSize, handleSize)
            };
        }

        public abstract void Dispose();
    }

    public class TextElement : LabelElement
    {
        private string text;
        private Font font;
        private bool autoSize = true;

        public TextElement(string text, Point location, string fontFamily, int fontSize)
        {
            this.text = text;
            this.font = new Font(fontFamily, fontSize);
            this.bounds = new Rectangle(location, new Size(100, 30));
            UpdateSize();
        }

        public string GetText() => text;
        public string GetFontFamily() => font.FontFamily.Name;
        public int GetFontSize() => (int)font.Size;

        private void UpdateSize()
        {
            if (autoSize)
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    var size = g.MeasureString(text, font);
                    bounds.Size = new Size((int)size.Width + 10, (int)size.Height + 5);
                }
            }
        }

        public override void Draw(Graphics g)
        {
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Calculate font size to fit if text is too large
            float fontSize = font.Size;
            SizeF textSize;
            do
            {
                using (var testFont = new Font(font.FontFamily, fontSize))
                {
                    textSize = g.MeasureString(text, testFont);
                }
                fontSize -= 0.5f;
            } while (textSize.Width > bounds.Width - 4 || textSize.Height > bounds.Height - 4);

            using (var scaledFont = new Font(font.FontFamily, Math.Max(6, fontSize + 0.5f)))
            {
                g.DrawString(text, scaledFont, Brushes.Black, bounds, format);
            }
        }

        public override void Dispose()
        {
            font?.Dispose();
        }
    }

    public class ImageElement : LabelElement
    {
        private Image image;
        private bool maintainAspectRatio = true;
        private string imagePath;

        public ImageElement(string imagePath, Point location)
        {
            this.imagePath = imagePath;
            image = Image.FromFile(imagePath);
            float ratio = (float)image.Width / image.Height;
            bounds = new Rectangle(location, new Size((int)(100 * ratio), 100));
        }

        public string GetImagePath() => imagePath;

        public override void Draw(Graphics g)
        {
            if (image != null)
            {
                g.DrawImage(image, bounds);
            }
        }

        public override void Resize(Point mouseLocation)
        {
            base.Resize(mouseLocation);
            
            if (maintainAspectRatio && image != null)
            {
                float ratio = (float)image.Width / image.Height;
                if (resizeHandle == 1 || resizeHandle == 2) // Right handles
                {
                    bounds.Height = (int)(bounds.Width / ratio);
                }
                else // Left handles
                {
                    bounds.Width = (int)(bounds.Height * ratio);
                }
            }
        }

        public override void Dispose()
        {
            image?.Dispose();
        }
    }

    public class QRElement : LabelElement
    {
        private Image qrImage;

        public QRElement(Image qrImage, Point location)
        {
            this.qrImage = qrImage;
            // QR codes should be square
            bounds = new Rectangle(location, new Size(100, 100));
        }

        public override void Draw(Graphics g)
        {
            if (qrImage != null)
            {
                g.DrawImage(qrImage, bounds);
            }
        }

        public override void Resize(Point mouseLocation)
        {
            base.Resize(mouseLocation);
            // Keep QR code square
            bounds.Height = bounds.Width;
        }

        public override void Dispose()
        {
            qrImage?.Dispose();
        }
    }
} 