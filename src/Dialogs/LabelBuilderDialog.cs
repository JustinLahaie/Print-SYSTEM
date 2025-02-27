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
using System.Linq;

namespace PrintSystem.Dialogs
{
    public class LabelTemplate
    {
        public string Name { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public List<LabelElementInfo> Elements { get; set; } = new List<LabelElementInfo>();

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) && 
                   Width > 0 && 
                   Height > 0 && 
                   Elements != null;
        }
    }

    public class LabelElementInfo
    {
        public string Type { get; set; }  // "Text", "QR", "Image"
        public string Content { get; set; }
        public Rectangle Bounds { get; set; }
        public string FontFamily { get; set; }
        public int FontSize { get; set; }
        public string QRTemplateKey { get; set; }  // Store the QR template key separately
    }

    public class LabelBuilderDialog : Form
    {
        private Panel designCanvas;
        private Panel toolbox;
        private ListBox availableFieldsListBox;
        private ComboBox templateComboBox;
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
            InitializeComponents();
            LoadQRTemplates();
            LoadLabelTemplates();
            if (currentItem != null)
            {
                PopulateFieldsWithItem();
            }
            
            // Add KeyDown event handler for keyboard shortcuts
            this.KeyPreview = true;
            this.KeyDown += LabelBuilderDialog_KeyDown;
        }

        private void LoadQRTemplates()
        {
            try
            {
                bool templatesLoaded = false;
                
                if (File.Exists(QR_TEMPLATES_FILE))
                {
                    try
                {
                    string json = File.ReadAllText(QR_TEMPLATES_FILE);
                    qrTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        templatesLoaded = qrTemplates != null && qrTemplates.Count > 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing QR templates: {ex.Message}");
                        templatesLoaded = false;
                    }
                }
                
                // If no templates were loaded, create default templates
                if (!templatesLoaded)
                {
                    qrTemplates = CreateDefaultQRTemplates();
                    SaveQRTemplates(); // Save the default templates
                }
                
                // Update QR template combo box
                UpdateQRTemplateComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading QR templates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                qrTemplates = CreateDefaultQRTemplates();
                // Try to save the default templates, but ignore any errors
                try { SaveQRTemplates(); } catch { }
                // Update QR template combo box even after error
                UpdateQRTemplateComboBox();
            }
        }
        
        private Dictionary<string, string> CreateDefaultQRTemplates()
        {
            return new Dictionary<string, string>
            {
                ["QR_BASIC"] = "Model: {ModelNumber} | Description: {Description}",
                ["QR_FULL"] = "Model: {ModelNumber} | Description: {Description} | Supplier: {Supplier} | Category: {Category} | Order Qty: {DefaultOrderQuantity} | URL: {ProductURL}",
                ["QR_MODEL"] = "{ModelNumber}",
                ["QR_URL"] = "{ProductURL}"
            };
        }
        
        private void SaveQRTemplates()
        {
            try
            {
                // Use a file lock with retry logic
                int retries = 3;
                bool saved = false;
                
                while (retries > 0 && !saved)
                {
                    try
                    {
                        using (var fileStream = new FileStream(QR_TEMPLATES_FILE, FileMode.Create, FileAccess.Write, FileShare.Read))
                        using (var streamWriter = new StreamWriter(fileStream))
                        {
                            string json = JsonSerializer.Serialize(qrTemplates, new JsonSerializerOptions 
                            { 
                                WriteIndented = true,
                                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                            });
                            streamWriter.Write(json);
                            streamWriter.Flush();
                            saved = true;
                        }
                    }
                    catch (IOException)
                    {
                        retries--;
                        if (retries > 0)
                            System.Threading.Thread.Sleep(200); // Wait before retry
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving QR templates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void UpdateQRTemplateComboBox()
        {
            if (qrTemplateComboBox != null && !IsDisposed && !Disposing)
            {
                string previouslySelected = qrTemplateComboBox.SelectedItem?.ToString();
                
                qrTemplateComboBox.BeginUpdate();
                try
                {
                    qrTemplateComboBox.Items.Clear();
                    
                    // Add built-in templates first (these will be available even if no templates are loaded)
                    qrTemplateComboBox.Items.Add("Basic Info");
                    qrTemplateComboBox.Items.Add("Full Details");
                    qrTemplateComboBox.Items.Add("URL Only");
                    
                    if (qrTemplateComboBox.Items.Count > 0 && qrTemplates.Count > 0)
                    {
                        qrTemplateComboBox.Items.Add("-------------------");
                    }
                    
                    // Add custom templates
                    foreach (string templateName in qrTemplates.Keys.OrderBy(k => k))
                    {
                        qrTemplateComboBox.Items.Add(templateName);
                    }
                    
                    // Try to restore previous selection
                    if (!string.IsNullOrEmpty(previouslySelected) && qrTemplateComboBox.Items.Contains(previouslySelected))
                    {
                        qrTemplateComboBox.SelectedItem = previouslySelected;
                    }
                    else if (qrTemplateComboBox.Items.Count > 0)
                    {
                        qrTemplateComboBox.SelectedIndex = 0; // Select first template
                    }
                }
                finally
                {
                    qrTemplateComboBox.EndUpdate();
                }
                
                // Debug check
                Console.WriteLine($"QR Template ComboBox updated: {qrTemplateComboBox.Items.Count} items, Selected: {qrTemplateComboBox.SelectedItem}");
            }
        }

        private void LoadLabelTemplates()
        {
            try
            {
                Dictionary<string, LabelTemplate> loadedTemplates = null;
                
                // Use a file lock to prevent concurrent access
                try
                {
                    using (var fileStream = new FileStream(LABEL_TEMPLATES_FILE, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                using (var streamReader = new StreamReader(fileStream))
                {
                    if (fileStream.Length > 0)
                    {
                        string json = streamReader.ReadToEnd();
                        loadedTemplates = JsonSerializer.Deserialize<Dictionary<string, LabelTemplate>>(json);
                    }
                    }
                }
                catch (IOException ioEx)
                {
                    // Log file access error but continue with defaults
                    Console.WriteLine($"IO Exception: {ioEx.Message}");
                    // Don't rethrow - we'll create a default template
                }

                // Initialize templates dictionary if null
                labelTemplates = loadedTemplates ?? new Dictionary<string, LabelTemplate>();

                // Validate all templates and remove invalid ones
                var invalidTemplates = labelTemplates.Where(kvp => !kvp.Value.IsValid()).Select(kvp => kvp.Key).ToList();
                foreach (var key in invalidTemplates)
                {
                    labelTemplates.Remove(key);
                }

                // Ensure MAIN template exists and is valid
                if (!labelTemplates.ContainsKey("MAIN") || !labelTemplates["MAIN"].IsValid())
                {
                    labelTemplates["MAIN"] = CreateDefaultMainTemplate();
                    SaveLabelTemplates();
                }

                // Update template combo box if it exists
                UpdateTemplateComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading label templates: {ex.Message}\nDefault templates will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                labelTemplates = new Dictionary<string, LabelTemplate>
                {
                    ["MAIN"] = CreateDefaultMainTemplate()
                };
                SaveLabelTemplates();
                
                // Make sure to update the combo box even after an exception
                UpdateTemplateComboBox();
            }
        }

        private void UpdateTemplateComboBox()
        {
            if (templateComboBox != null && !IsDisposed && !Disposing)
            {
                string previouslySelected = templateComboBox.SelectedItem?.ToString();
                
                templateComboBox.BeginUpdate();
                try
                {
                    templateComboBox.Items.Clear();
                    templateComboBox.Items.Add("ADD NEW");
                    templateComboBox.Items.Add("-------------------");
                    
                    // Add MAIN first if it exists
                    if (labelTemplates.ContainsKey("MAIN"))
                    {
                    templateComboBox.Items.Add("MAIN");
                    }
                    
                    // Add all other templates sorted alphabetically
                    foreach (string templateName in labelTemplates.Keys.Where(k => k != "MAIN").OrderBy(k => k))
                    {
                        templateComboBox.Items.Add(templateName);
                    }

                    // Try to restore previous selection or default to MAIN
                    if (!string.IsNullOrEmpty(previouslySelected) && templateComboBox.Items.Contains(previouslySelected))
                    {
                        templateComboBox.SelectedItem = previouslySelected;
                    }
                    else if (templateComboBox.Items.Contains("MAIN"))
                    {
                        // Find the index of MAIN (should be 2 but let's be safe)
                        int mainIndex = templateComboBox.Items.IndexOf("MAIN");
                        if (mainIndex >= 0)
                        {
                            templateComboBox.SelectedIndex = mainIndex;
                        }
                    }
                    else if (templateComboBox.Items.Count > 2) // Skip ADD NEW and separator
                    {
                        templateComboBox.SelectedIndex = 2; // Select first template after separator
                    }
                    
                    // Ensure something is selected
                    if (templateComboBox.SelectedIndex == -1 && templateComboBox.Items.Count > 0)
                    {
                        templateComboBox.SelectedIndex = 0;
                    }
                }
                finally
                {
                    templateComboBox.EndUpdate();
                }
                
                // Debug check - remove in production
                Console.WriteLine($"Template ComboBox updated: {templateComboBox.Items.Count} items, Selected: {templateComboBox.SelectedItem}");
            }
            else
            {
                // Debug logging - remove in production
                Console.WriteLine("UpdateTemplateComboBox called but combo box is null or form is disposing");
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

        private void SaveDebugInfo(string message)
        {
            try
            {
                string debugPath = Path.Combine(Application.StartupPath, "label_debug.log");
                using (StreamWriter writer = new StreamWriter(debugPath, true))
                {
                    writer.WriteLine($"[{DateTime.Now}] {message}");
                }
            }
            catch
            {
                // Silently fail for debug logging
            }
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
            templateComboBox.SelectedIndexChanged += TemplateComboBox_SelectedIndexChanged;

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
            // No need to populate here - UpdateQRTemplateComboBox will handle it

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

            // Element operations group
            GroupBox operationsGroup = new GroupBox
            {
                Text = "Element Operations",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            TableLayoutPanel operationsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 1
            };

            Button deleteElementButton = new Button
            {
                Text = "Delete Selected Element",
                Dock = DockStyle.Fill,
                Height = 30
            };
            deleteElementButton.Click += DeleteElementButton_Click;

            operationsLayout.Controls.Add(deleteElementButton, 0, 0);
            operationsGroup.Controls.Add(operationsLayout);
            toolboxLayout.Controls.Add(operationsGroup, 5, 0);

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
            
            // Add context menu for the canvas
            ContextMenuStrip canvasContextMenu = new ContextMenuStrip();
            ToolStripMenuItem deleteMenuItem = new ToolStripMenuItem("Delete Selected Element");
            deleteMenuItem.Click += (s, e) => DeleteSelectedElement();
            canvasContextMenu.Items.Add(deleteMenuItem);
            designCanvas.ContextMenuStrip = canvasContextMenu;
            
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
                // Create template from current state
                var template = new LabelTemplate
                {
                    Name = "MAIN",
                    Width = labelWidthInput.Value,
                    Height = labelHeightInput.Value,
                    Elements = new List<LabelElementInfo>()
                };

                // Save all current elements
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
                        
                        // Save some debug info about what's being saved
                        SaveDebugInfo($"Saving TextElement to template: Content='{elementInfo.Content}'");
                    }
                    else if (element is QRElement qrElement)
                    {
                        elementInfo.Type = "QR";
                        elementInfo.QRTemplateKey = qrElement.GetTemplateKey() ?? 
                            qrTemplateComboBox.SelectedItem?.ToString() ?? "Basic Info";
                        elementInfo.Content = qrElement.GetTemplateContent() ?? "";
                    }
                    else if (element is ImageElement imageElement)
                    {
                        elementInfo.Type = "Image";
                        elementInfo.Content = imageElement.GetImagePath();
                    }

                    template.Elements.Add(elementInfo);
                }

                // Save to MAIN template
                labelTemplates["MAIN"] = template;
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
            // Only proceed with left button clicks for dragging/selection
            if (e.Button != MouseButtons.Left) return;
            
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
            
            if (fieldName == "Image")
            {
                try {
                    string imagePath = null;
                    
                    // If we have a current item with an image, use that
                    if (currentItem != null && !string.IsNullOrEmpty(currentItem.ImagePath) && File.Exists(currentItem.ImagePath))
                    {
                        imagePath = currentItem.ImagePath;
                        SaveDebugInfo($"Using current item image: {imagePath}");
                    }
                    
                    // Check if we have a valid image path
                    if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    {
                        element = new ImageElement(imagePath, location);
                        SaveDebugInfo($"Created image element with path: {imagePath}");
                    }
                    else
                    {
                        // Create and use a placeholder image
                        string placeholderPath = Path.Combine(Application.StartupPath, "placeholder.png");
                        if (!File.Exists(placeholderPath))
                        {
                            // Create a simple placeholder image if it doesn't exist
                            using (var placeholderImage = new Bitmap(100, 100))
                            using (var g = Graphics.FromImage(placeholderImage))
                            {
                                g.Clear(Color.White);
                                g.DrawRectangle(Pens.Gray, 0, 0, 99, 99);
                                g.DrawString("Image", new Font("Arial", 12), Brushes.Gray, 
                                    new Rectangle(0, 0, 100, 100), 
                                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                                
                                placeholderImage.Save(placeholderPath, ImageFormat.Png);
                                SaveDebugInfo($"Created placeholder image at: {placeholderPath}");
                            }
                        }
                        element = new ImageElement(placeholderPath, location);
                        SaveDebugInfo($"Created image element with placeholder: {placeholderPath}");
                    }
                }
                catch (Exception ex)
                {
                    SaveDebugInfo($"Error creating image element: {ex.Message}");
                    return; // Exit the method instead of using continue
                }
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
            string templateName = qrTemplateComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(templateName))
            {
                MessageBox.Show("Please select a QR template first.", "No Template Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string content;
            string templateKey = templateName; // Store the template key/name for the QR element
            
            // Handle custom templates from qrTemplates dictionary
            if (qrTemplates.ContainsKey(templateName))
            {
                content = qrTemplates[templateName];
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
                switch (templateName)
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
                var qrElement = new QRElement(qrImage, location, templateKey, content);
                labelElements.Add(qrElement);
                selectedElement = qrElement;
                designCanvas.Invalidate();
                
                // Debug message
                Console.WriteLine($"QR code generated using template: {templateName}");
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
                                element = new QRElement(qrImage, 
                                                      new Point(elementInfo.Bounds.X, elementInfo.Bounds.Y),
                                                      elementInfo.QRTemplateKey,
                                                      elementInfo.Content);
                                
                                // Set the QR template in the combo box if it exists
                                if (!string.IsNullOrEmpty(elementInfo.QRTemplateKey))
                                {
                                    qrTemplateComboBox.SelectedItem = elementInfo.QRTemplateKey;
                                }
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
                                Console.WriteLine($"Image file not found: {elementInfo.Content}");
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

            string templateName;
            string selectedTemplate = templateComboBox.SelectedItem?.ToString();

            // If "ADD NEW" is selected or no template is selected, prompt for new name
            if (selectedTemplate == null || selectedTemplate == "ADD NEW" || selectedTemplate == "-------------------")
            {
                using (var dialog = new TextInputDialog("Save Template As", "Enter a name for your template:"))
                {
                    if (dialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.InputText))
                    return;
                        
                    templateName = dialog.InputText.Trim();
                }
            }
            else
            {
                // Confirm before overwriting existing template
                if (MessageBox.Show($"Do you want to update the existing '{selectedTemplate}' template?", 
                    "Confirm Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    // If they don't want to overwrite, prompt for a new name
                    using (var dialog = new TextInputDialog("Save Template As", "Enter a name for your template:"))
                    {
                        if (dialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.InputText))
                            return;
                            
                        templateName = dialog.InputText.Trim();
                    }
                }
                else
                {
                    templateName = selectedTemplate;
                }
            }

            if (templateName.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
            {
                // Redirect to UpdateMainButton_Click for MAIN template updates
                UpdateMainButton_Click(sender, e);
                return;
            }

            if (templateName.Equals("ADD NEW", StringComparison.OrdinalIgnoreCase) || 
                templateName.Equals("-------------------") ||
                string.IsNullOrWhiteSpace(templateName))
            {
                MessageBox.Show("Invalid template name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    else if (element is QRElement qrElement)
                    {
                        elementInfo.Type = "QR";
                        elementInfo.QRTemplateKey = qrElement.GetTemplateKey() ?? 
                            qrTemplateComboBox.SelectedItem?.ToString() ?? "Basic Info";
                        elementInfo.Content = qrElement.GetTemplateContent() ?? "";
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
            if (SaveLabelTemplates())
            {
                MessageBox.Show($"Template '{templateName}' saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateTemplateComboBox();
                templateComboBox.SelectedItem = templateName;
            }
        }

        private bool SaveLabelTemplates()
        {
            try
            {
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

                // Create a temporary file first, then move it to the final location
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
                    string json = JsonSerializer.Serialize(labelTemplates, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });
                
                // Write to temporary file first
                File.WriteAllText(tempFile, json);
                
                // Now move the temporary file to the final location
                try {
                    if (File.Exists(LABEL_TEMPLATES_FILE))
                    {
                        // Create backup file
                        string backupFile = LABEL_TEMPLATES_FILE + ".bak";
                        if (File.Exists(backupFile))
                            File.Delete(backupFile);
                            
                        File.Copy(LABEL_TEMPLATES_FILE, backupFile);
                        File.Delete(LABEL_TEMPLATES_FILE);
                    }
                    
                    File.Move(tempFile, LABEL_TEMPLATES_FILE);
                    return true;
            }
            catch (Exception ex)
            {
                    MessageBox.Show($"Error saving label templates: {ex.Message}\nYour changes have been saved to {tempFile}", 
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving label templates: {ex.Message}\nPlease try again or check file permissions.", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void PrintButton_Click(object sender, EventArgs e)
        {
            // Use the current item if available, otherwise get a sample item
            var sampleItem = currentItem;
            
            // If no current item, try to get a sample item
            if (sampleItem == null)
            {
                var items = ItemManager.GetItems();
                if (!items.Any())
                {
                    MessageBox.Show("Please add at least one item to show a print preview.", "No Items", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                // Use the first item as a sample
                sampleItem = items.First();
            }
            
            // Debug sample item information
            SaveDebugInfo($"Print preview sample item: ID={sampleItem.Id}, Model={sampleItem.ModelNumber}, Supplier={sampleItem.Supplier}");
            SaveDebugInfo($"Number of label elements: {labelElements.Count}");
            
            foreach (var element in labelElements)
            {
                if (element is TextElement textElement)
                {
                    SaveDebugInfo($"TextElement: Text='{textElement.GetText()}', Font={textElement.GetFontFamily()}, Size={textElement.GetFontSize()}");
                }
                else if (element is ImageElement)
                {
                    SaveDebugInfo($"ImageElement detected");
                }
                else if (element is QRElement qrElement)
                {
                    SaveDebugInfo($"QRElement: Content='{qrElement.GetTemplateContent()?.Substring(0, Math.Min(qrElement.GetTemplateContent()?.Length ?? 0, 50))}...'");
                }
            }

            // Create a preview form
            using (var previewForm = new Form())
            {
                previewForm.Text = "Print Preview";
                previewForm.Size = new Size(800, 600);
                previewForm.StartPosition = FormStartPosition.CenterParent;
                previewForm.MinimizeBox = false;
                previewForm.MaximizeBox = false;
                previewForm.FormBorderStyle = FormBorderStyle.FixedDialog;

                // Create preview panel
                Panel previewPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.LightGray,
                    AutoScroll = true
                };

                // Create a white panel to represent the paper
                Panel paperPanel = new Panel
                {
                    BackColor = Color.White,
                    Location = new Point(20, 20),
                    Size = new Size(
                        (int)((double)labelWidthInput.Value * DPI_SCALE),
                        (int)((double)labelHeightInput.Value * DPI_SCALE)
                    ),
                    Margin = new Padding(0)
                };

                // Add paint handler for the paper panel
                paperPanel.Paint += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    
                    // Draw all elements with actual item data
                    foreach (var element in labelElements)
                    {
                        // Create adjusted bounds to correctly position elements within the preview
                        // The element.Bounds already includes the MARGIN for design purposes,
                        // but since the paperPanel doesn't have the margins, we need to adjust
                        var adjustedBounds = new Rectangle(
                            element.Bounds.X - MARGIN,
                            element.Bounds.Y - MARGIN,
                            element.Bounds.Width,
                            element.Bounds.Height
                        );
                        
                        // Ensure no negative coordinates that would push elements out of view
                        if (adjustedBounds.X < 0) adjustedBounds.X = 0;
                        if (adjustedBounds.Y < 0) adjustedBounds.Y = 0;

                        if (element is TextElement textElement)
                        {
                            // Get the actual text with item data
                            string text = textElement.GetText();
                            string originalText = text;
                            
                            // Save debug info
                            SaveDebugInfo($"Processing text element in preview: '{text}'");
                            
                            // Replace placeholders with actual values
                            switch (text.Trim())
                            {
                                case "Model Number":
                                    text = sampleItem.ModelNumber ?? "";
                                    SaveDebugInfo($"Matched 'Model Number' → '{text}'");
                                    break;
                                case "Description":
                                    text = sampleItem.Description ?? "";
                                    SaveDebugInfo($"Matched 'Description' → '{text}'");
                                    break;
                                case "Supplier":
                                    text = sampleItem.Supplier ?? "";
                                    SaveDebugInfo($"Matched 'Supplier' → '{text}'");
                                    break;
                                case "Category":
                                    text = sampleItem.CategoryPath ?? "Uncategorized";
                                    SaveDebugInfo($"Matched 'Category' → '{text}'");
                                    break;
                                case "Default Order Quantity":
                                    text = sampleItem.DefaultOrderQuantity.ToString();
                                    SaveDebugInfo($"Matched 'Default Order Quantity' → '{text}'");
                                    break;
                                case "Product URL":
                                    text = sampleItem.ProductUrl ?? "";
                                    SaveDebugInfo($"Matched 'Product URL' → '{text}'");
                                    break;
                                default:
                                    // Check for field names without exact case matching
                                    if (text.Trim().Equals("supplier", StringComparison.OrdinalIgnoreCase))
                                    {
                                        text = sampleItem.Supplier ?? "";
                                        SaveDebugInfo($"Case-insensitive matched 'supplier' → '{text}'");
                                    }
                                    else if (text.Trim().Equals("description", StringComparison.OrdinalIgnoreCase))
                                    {
                                        text = sampleItem.Description ?? "";
                                        SaveDebugInfo($"Case-insensitive matched 'description' → '{text}'");
                                    }
                                    else if (text.Trim().Equals("category", StringComparison.OrdinalIgnoreCase))
                                    {
                                        text = sampleItem.CategoryPath ?? "Uncategorized";
                                        SaveDebugInfo($"Case-insensitive matched 'category' → '{text}'");
                                    }
                                    else if (text.Trim().Equals("model number", StringComparison.OrdinalIgnoreCase))
                                    {
                                        text = sampleItem.ModelNumber ?? "";
                                        SaveDebugInfo($"Case-insensitive matched 'model number' → '{text}'");
                                    }
                                    else if (text.Trim().Equals("default order quantity", StringComparison.OrdinalIgnoreCase))
                                    {
                                        text = sampleItem.DefaultOrderQuantity.ToString();
                                        SaveDebugInfo($"Case-insensitive matched 'default order quantity' → '{text}'");
                                    }
                                    else if (text.Trim().Equals("product url", StringComparison.OrdinalIgnoreCase))
                                    {
                                        text = sampleItem.ProductUrl ?? "";
                                        SaveDebugInfo($"Case-insensitive matched 'product url' → '{text}'");
                                    }
                                    // If it contains placeholders in curly braces
                                    else if (text.Contains("{"))
                                    {
                                        SaveDebugInfo($"Found template with placeholders: '{text}'");
                                        text = text.Replace("{ModelNumber}", sampleItem.ModelNumber ?? "")
                                                 .Replace("{Description}", sampleItem.Description ?? "")
                                                 .Replace("{Supplier}", sampleItem.Supplier ?? "")
                                                 .Replace("{Category}", sampleItem.CategoryPath ?? "Uncategorized")
                                                 .Replace("{DefaultOrderQuantity}", sampleItem.DefaultOrderQuantity.ToString())
                                                 .Replace("{ProductURL}", sampleItem.ProductUrl ?? "");
                                        SaveDebugInfo($"After replacing placeholders: '{text}'");
                                    }
                                    else
                                    {
                                        SaveDebugInfo($"No field match for: '{text}', keeping original text");
                                    }
                                    break;
                            }
                            
                            // Draw text with the same font and position
                            using (var font = new Font(textElement.GetFontFamily(), textElement.GetFontSize()))
                            {
                                // Create a clear formatting style
                                var format = new StringFormat
                                {
                                    Alignment = StringAlignment.Center,
                                    LineAlignment = StringAlignment.Center
                                };
                                
                                // Draw the text with appropriate color - use red for debugging empty values
                                Brush textBrush = string.IsNullOrWhiteSpace(text) ? Brushes.Red : Brushes.Black;
                                
                                // If text was not substituted, use the original text color
                                if (text == originalText && !text.Contains("{"))
                                {
                                    pe.Graphics.DrawString(text, font, textBrush, adjustedBounds, format);
                                }
                                else
                                {
                                    pe.Graphics.DrawString(text, font, textBrush, adjustedBounds, format);
                                }
                                
                                // Draw a border around text elements for better visibility
                                pe.Graphics.DrawRectangle(Pens.LightGray, adjustedBounds);
                            }
                        }
                        else if (element is QRElement qrElement)
                        {
                            // Use the stored template content from the QR element
                            string qrContent = qrElement.GetTemplateContent();
                            if (string.IsNullOrEmpty(qrContent))
                            {
                                // Fallback to a basic template if no content is stored
                                qrContent = "Model: {ModelNumber} | Description: {Description}";
                            }

                            // Replace placeholders with actual values
                            qrContent = qrContent.Replace("{ModelNumber}", sampleItem.ModelNumber ?? "")
                                               .Replace("{Description}", sampleItem.Description ?? "")
                                               .Replace("{Supplier}", sampleItem.Supplier ?? "")
                                               .Replace("{Category}", sampleItem.CategoryPath ?? "Uncategorized")
                                               .Replace("{DefaultOrderQuantity}", sampleItem.DefaultOrderQuantity.ToString())
                                               .Replace("{ProductURL}", sampleItem.ProductUrl ?? "");

                            // Generate new QR code with actual data
                            try
                            {
                                var writer = new BarcodeWriter<Bitmap>
                                {
                                    Format = BarcodeFormat.QR_CODE,
                                    Options = new QrCodeEncodingOptions
                                    {
                                        Width = adjustedBounds.Width,
                                        Height = adjustedBounds.Height,
                                        Margin = 1,
                                        ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                                    },
                                    Renderer = new BitmapRenderer()
                                };

                                using (var qrImage = writer.Write(qrContent))
                                {
                                    pe.Graphics.DrawImage(qrImage, adjustedBounds);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error generating QR code preview: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else if (element is ImageElement imageElement)
                        {
                            // Get the image path from the element
                            string imagePath = imageElement.GetImagePath();
                            
                            // Check if it's a placeholder image (filename contains "placeholder")
                            bool isPlaceholder = !string.IsNullOrEmpty(imagePath) && 
                                               (imagePath.ToLower().Contains("placeholder") || 
                                                Path.GetFileName(imagePath).ToLower() == "placeholder.png");
                            
                            // If we have a sample item with an image, and we have a placeholder or no valid image, use the sample item's image
                            if (sampleItem != null && !string.IsNullOrEmpty(sampleItem.ImagePath) && 
                                File.Exists(sampleItem.ImagePath) && 
                                (isPlaceholder || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)))
                            {
                                imagePath = sampleItem.ImagePath;
                                SaveDebugInfo($"Using item image: {imagePath}");
                            }
                            
                            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                            {
                                try
                                {
                                    using (var image = Image.FromFile(imagePath))
                                    {
                                        pe.Graphics.DrawImage(image, adjustedBounds);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error loading image for preview: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                };

                // Add print button
                Button printButton = new Button
                {
                    Text = "Print",
                    Dock = DockStyle.Bottom,
                    Height = 40
                };
                printButton.Click += (s, pe) =>
                {
                    using (var printDialog = new PrintDialog())
                    {
                        if (printDialog.ShowDialog() == DialogResult.OK)
                        {
                            // TODO: Implement actual printing in a future update
                            MessageBox.Show("Printing will be implemented in a future update.", "Coming Soon");
                        }
                    }
                };

                // Add close button
                Button closeButton = new Button
                {
                    Text = "Close",
                    Dock = DockStyle.Bottom,
                    Height = 40
                };
                closeButton.Click += (s, pe) => previewForm.Close();

                // Add controls to the preview form
                previewPanel.Controls.Add(paperPanel);
                previewForm.Controls.Add(previewPanel);
                previewForm.Controls.Add(printButton);
                previewForm.Controls.Add(closeButton);

                // Show the preview
                previewForm.ShowDialog();
            }
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
            if (labelElements.Count == 0)
            {
                MessageBox.Show("Please add some elements to the label before updating the MAIN template.", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

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
                else if (element is QRElement qrElement)
                {
                    elementInfo.Type = "QR";
                    elementInfo.QRTemplateKey = qrElement.GetTemplateKey() ?? 
                        qrTemplateComboBox.SelectedItem?.ToString() ?? "Basic Info";
                    elementInfo.Content = qrElement.GetTemplateContent() ?? "";
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
            if (SaveLabelTemplates())
            {
                MessageBox.Show("MAIN template updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Add to combo box if not exists
            if (!templateComboBox.Items.Contains("MAIN"))
            {
                    UpdateTemplateComboBox();
                    templateComboBox.SelectedItem = "MAIN";
            }
            }
        }

        private void DeleteTemplateButton_Click(object sender, EventArgs e)
        {
            string templateName = templateComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(templateName) || 
                templateName == "ADD NEW" || 
                templateName == "-------------------")
            {
                MessageBox.Show("Please select a valid template to delete.", "No Template Selected", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (templateName.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The MAIN template cannot be deleted. You can update it with the 'Update MAIN' button.", 
                    "Cannot Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete the template '{templateName}'?\nThis action cannot be undone.",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                labelTemplates.Remove(templateName);
                if (SaveLabelTemplates())
                {
                    MessageBox.Show($"Template '{templateName}' has been deleted.", "Success", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UpdateTemplateComboBox();
                }
            }
        }

        private void TemplateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (templateComboBox.SelectedItem == null) return;
            
            // Clear existing elements
            foreach (var element in labelElements)
            {
                element.Dispose();
            }
            labelElements.Clear();
            
            // Get the selected template
            string templateName = templateComboBox.SelectedItem.ToString();
            if (labelTemplates.TryGetValue(templateName, out LabelTemplate template))
            {
                // Update label dimensions
                labelWidthInput.Value = template.Width;
                labelHeightInput.Value = template.Height;
                
                // Add all elements from the template
                foreach (var elementInfo in template.Elements)
                {
                    try
                    {
                        LabelElement element = null;
                        
                        // Convert bounds from the serialized format
                        Rectangle bounds = elementInfo.Bounds;
                        
                        switch (elementInfo.Type)
                        {
                            case "Text":
                                element = new TextElement(elementInfo.Content, 
                                    new Point(bounds.X, bounds.Y), 
                                    elementInfo.FontFamily, 
                                    elementInfo.FontSize);
                                break;
                                
                            case "QR":
                                // Generate a QR code image
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
                                element = new QRElement(qrImage, 
                                    new Point(bounds.X, bounds.Y), 
                                    elementInfo.QRTemplateKey,
                                    elementInfo.Content);
                                break;
                                
                            case "Image":
                                string imagePath = elementInfo.Content;
                                
                                // If we have a current item with an image, and the content path is a placeholder or doesn't exist
                                if (currentItem != null && !string.IsNullOrEmpty(currentItem.ImagePath) && File.Exists(currentItem.ImagePath))
                                {
                                    bool isPlaceholder = !string.IsNullOrEmpty(imagePath) && 
                                                       (imagePath.ToLower().Contains("placeholder") || 
                                                        Path.GetFileName(imagePath).ToLower() == "placeholder.png");
                                        
                                    if (isPlaceholder || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                                    {
                                        imagePath = currentItem.ImagePath;
                                        SaveDebugInfo($"Loading template: Using item image instead of placeholder: {imagePath}");
                                    }
                                }
                                
                                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                                {
                                    element = new ImageElement(imagePath, new Point(bounds.X, bounds.Y));
                                }
                                else
                                {
                                    // Create a placeholder
                                    string placeholderPath = Path.Combine(Application.StartupPath, "placeholder.png");
                                    if (!File.Exists(placeholderPath))
                                    {
                                        using (var placeholderImage = new Bitmap(100, 100))
                                        using (var g = Graphics.FromImage(placeholderImage))
                                        {
                                            g.Clear(Color.White);
                                            g.DrawRectangle(Pens.Gray, 0, 0, 99, 99);
                                            g.DrawString("Image", new Font("Arial", 12), Brushes.Gray, 
                                                new Rectangle(0, 0, 100, 100), 
                                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                                            
                                            placeholderImage.Save(placeholderPath, ImageFormat.Png);
                                        }
                                    }
                                    element = new ImageElement(placeholderPath, new Point(bounds.X, bounds.Y));
                                    SaveDebugInfo($"Loading template: Using placeholder image for Image element: {placeholderPath}");
                                }
                                break;
                        }
                        
                        if (element != null)
                        {
                            // Set the bounds to match the saved template
                            element.Bounds = bounds;
                            labelElements.Add(element);
                        }
                    }
                    catch (Exception ex)
                    {
                        SaveDebugInfo($"Error loading element from template: {ex.Message}");
                    }
                }
                
                // Update the canvas to show the new template
                UpdateCanvasSize();
                designCanvas.Invalidate();
            }
        }

        private void LabelBuilderDialog_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Delete key for removing selected element
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedElement();
                e.Handled = true;
            }
        }

        private void DeleteElementButton_Click(object sender, EventArgs e)
        {
            DeleteSelectedElement();
        }

        private void DeleteSelectedElement()
        {
            if (selectedElement != null)
            {
                // Remove the selected element from the list
                labelElements.Remove(selectedElement);
                
                // Dispose the element to free resources
                selectedElement.Dispose();
                
                // Clear the selection
                selectedElement = null;
                
                // Redraw the canvas
                designCanvas.Invalidate();
            }
            else
            {
                MessageBox.Show("Please select an element to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (string.IsNullOrEmpty(text)) return;

            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.None
            };

            // Start with the original font size and scale down until it fits
            float fontSize = font.Size;
            SizeF textSize;
            
            // Binary search for the right font size
            float minSize = 4;  // Minimum readable size
            float maxSize = fontSize;
            float bestSize = minSize;
            
            while (maxSize - minSize > 0.5f)
            {
                fontSize = (minSize + maxSize) / 2;
                using (var testFont = new Font(font.FontFamily, fontSize))
                {
                    textSize = g.MeasureString(text, testFont, bounds.Size, format);
                    if (textSize.Width <= bounds.Width - 4 && textSize.Height <= bounds.Height - 4)
                    {
                        bestSize = fontSize;
                        minSize = fontSize;
                    }
                    else
                    {
                        maxSize = fontSize;
                    }
                }
            }

            // Use the best fitting size found
            using (var scaledFont = new Font(font.FontFamily, bestSize))
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
        private string templateKey;
        private string templateContent;

        public QRElement(Image qrImage, Point location, string templateKey = null, string templateContent = null)
        {
            this.qrImage = qrImage;
            this.templateKey = templateKey;
            this.templateContent = templateContent;
            // QR codes should be square
            bounds = new Rectangle(location, new Size(100, 100));
        }

        public string GetTemplateKey() => templateKey;
        public string GetTemplateContent() => templateContent;

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