using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
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
                // Create default templates if file doesn't exist
                if (!File.Exists(QR_TEMPLATES_FILE))
                {
                    qrTemplates = CreateDefaultQRTemplates();
                    SaveQRTemplates();
                    SaveDebugInfo("Created default QR templates file");
                }
                else
                {
                    try
                    {
                        string json = File.ReadAllText(QR_TEMPLATES_FILE);
                        if (string.IsNullOrWhiteSpace(json) || json == "{}")
                        {
                            qrTemplates = CreateDefaultQRTemplates();
                            SaveDebugInfo("Loaded default QR templates (file was empty)");
                        }
                        else
                        {
                            qrTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? CreateDefaultQRTemplates();
                            SaveDebugInfo($"Loaded {qrTemplates.Count} QR templates from file");
                        }
                    }
                    catch (Exception ex)
                    {
                        SaveDebugInfo($"Error reading QR templates: {ex.Message}");
                        qrTemplates = CreateDefaultQRTemplates();
                        SaveQRTemplates(); // Save the defaults
                    }
                }
                
                // Ensure we always have at least one template
                if (qrTemplates.Count == 0)
                {
                    qrTemplates = CreateDefaultQRTemplates();
                    SaveQRTemplates();
                }
            }
            catch (Exception ex)
            {
                SaveDebugInfo($"Error in LoadQRTemplates: {ex.Message}");
                qrTemplates = CreateDefaultQRTemplates();
            }

            UpdateQRTemplateComboBox();
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
            // Early exit if the combo box is disposed or null
            if (qrTemplateComboBox == null || qrTemplateComboBox.IsDisposed || this.IsDisposed)
            {
                SaveDebugInfo("QR template combo box is null or disposed, skipping update");
                return;
            }
            
            try
            {
                string previouslySelected = qrTemplateComboBox.SelectedItem?.ToString();
                
                qrTemplateComboBox.BeginUpdate();
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
                
                qrTemplateComboBox.EndUpdate();
                
                // Debug check
                SaveDebugInfo($"QR Template ComboBox updated: {qrTemplateComboBox.Items.Count} items, Selected: {qrTemplateComboBox.SelectedItem}");
            }
            catch (Exception ex)
            {
                SaveDebugInfo($"Error updating QR template combo box: {ex.Message}");
                
                // Make sure EndUpdate is always called
                try
                {
                    qrTemplateComboBox.EndUpdate();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
        }

        private void LoadLabelTemplates()
        {
            try
            {
                // Create default templates if file doesn't exist
                if (!File.Exists(LABEL_TEMPLATES_FILE))
                {
                    labelTemplates = new Dictionary<string, LabelTemplate>();
                    labelTemplates["MAIN"] = CreateDefaultMainTemplate();
                    SaveLabelTemplates();
                    SaveDebugInfo("Created default label templates file");
                }
                else
                {
                    try
                    {
                        using (var fileStream = new FileStream(LABEL_TEMPLATES_FILE, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                        {
                            if (fileStream.Length == 0)
                            {
                                // File is empty, create default templates
                                labelTemplates = new Dictionary<string, LabelTemplate>();
                                labelTemplates["MAIN"] = CreateDefaultMainTemplate();
                                
                                // Write defaults to the file
                                var json = JsonSerializer.Serialize(labelTemplates);
                                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                                fileStream.Write(bytes, 0, bytes.Length);
                                
                                SaveDebugInfo("Created default label templates (file was empty)");
                            }
                            else
                            {
                                // Read existing templates
                                fileStream.Seek(0, SeekOrigin.Begin);
                                using (var reader = new StreamReader(fileStream))
                                {
                                    var json = reader.ReadToEnd();
                                    labelTemplates = JsonSerializer.Deserialize<Dictionary<string, LabelTemplate>>(json) ?? new Dictionary<string, LabelTemplate>();
                                    SaveDebugInfo($"Loaded {labelTemplates.Count} label templates from file");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SaveDebugInfo($"Error reading label templates: {ex.Message}");
                        labelTemplates = new Dictionary<string, LabelTemplate>();
                        labelTemplates["MAIN"] = CreateDefaultMainTemplate();
                        SaveLabelTemplates(); // Save the defaults
                    }
                }
                
                // Ensure we always have at least the MAIN template
                if (!labelTemplates.ContainsKey("MAIN"))
                {
                    labelTemplates["MAIN"] = CreateDefaultMainTemplate();
                    SaveLabelTemplates();
                }
                
                // Clean up any invalid templates
                var invalidTemplates = labelTemplates.Where(kvp => !kvp.Value.IsValid()).Select(kvp => kvp.Key).ToList();
                foreach (var key in invalidTemplates)
                {
                    SaveDebugInfo($"Removing invalid template: {key}");
                    labelTemplates.Remove(key);
                }
                
                if (invalidTemplates.Any())
                {
                    SaveLabelTemplates();
                }
            }
            catch (Exception ex)
            {
                SaveDebugInfo($"Error in LoadLabelTemplates: {ex.Message}");
                labelTemplates = new Dictionary<string, LabelTemplate>();
                labelTemplates["MAIN"] = CreateDefaultMainTemplate();
            }

            UpdateTemplateComboBox();
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
                                g.DrawString("Item Image", new Font("Arial", 12), Brushes.Gray, 
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
                        Width = 1200,  // Much higher resolution for better quality
                        Height = 1200,
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
                                        Width = 600,
                                        Height = 600,
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
                            string imagePath = elementInfo.Content;
                            
                            // First try using the path from the template
                            if (File.Exists(imagePath))
                            {
                                element = new ImageElement(imagePath, new Point(elementInfo.Bounds.X, elementInfo.Bounds.Y));
                                SaveDebugInfo($"Loaded image from template path: {imagePath}");
                            }
                            // If that fails, try using the current item's image path
                            else if (currentItem != null && !string.IsNullOrEmpty(currentItem.ImagePath) && File.Exists(currentItem.ImagePath))
                            {
                                element = new ImageElement(currentItem.ImagePath, new Point(elementInfo.Bounds.X, elementInfo.Bounds.Y));
                                SaveDebugInfo($"Using current item's image instead of template path: {currentItem.ImagePath}");
                            }
                            // If both fail, create a placeholder
                            else
                            {
                                SaveDebugInfo($"Image file not found at path: {imagePath}");
                                SaveDebugInfo("Creating placeholder image instead");
                                
                                string placeholderPath = Path.Combine(Application.StartupPath, "placeholder.png");
                                if (!File.Exists(placeholderPath))
                                {
                                    // Create a simple placeholder if it doesn't exist
                                    using (var placeholderImage = new Bitmap(100, 100))
                                    using (var g = Graphics.FromImage(placeholderImage))
                                    {
                                        g.Clear(Color.White);
                                        g.DrawRectangle(Pens.Gray, 0, 0, 99, 99);
                                        g.DrawString("Placeholder", new Font("Arial", 12), Brushes.Gray, 
                                            new Rectangle(0, 0, 100, 100), 
                                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                                        
                                        placeholderImage.Save(placeholderPath, ImageFormat.Png);
                                    }
                                }
                                element = new ImageElement(placeholderPath, new Point(elementInfo.Bounds.X, elementInfo.Bounds.Y));
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
            try
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
                
                // Create a local copy of the label elements to prevent issues if elements are modified/disposed
                var elementsToDisplay = new List<LabelElement>(labelElements);
                
                // Update any image elements with the current item's image path if available
                if (sampleItem != null && !string.IsNullOrEmpty(sampleItem.ImagePath) && File.Exists(sampleItem.ImagePath))
                {
                    foreach (var element in elementsToDisplay)
                    {
                        if (element is ImageElement imageElement)
                        {
                            // Check if the current image path is a placeholder or doesn't exist
                            string currentPath = imageElement.GetImagePath();
                            bool shouldUpdate = string.IsNullOrEmpty(currentPath) || 
                                               !File.Exists(currentPath) || 
                                               imageElement.IsPlaceholder();
                                               
                            if (shouldUpdate)
                            {
                                // Update the image path to use the current item's image
                                imageElement.UpdateImagePath(sampleItem.ImagePath);
                                SaveDebugInfo($"Updated image element to use current item's image: {sampleItem.ImagePath}");
                            }
                        }
                    }
                }
                
                // Store label dimensions locally to avoid accessing potentially disposed controls
                decimal labelWidth = labelWidthInput?.Value ?? 100m;
                decimal labelHeight = labelHeightInput?.Value ?? 50m;
                
                SaveDebugInfo($"Print preview sample item: ID={sampleItem.Id}, Model={sampleItem.ModelNumber}, Supplier={sampleItem.Supplier}");
                SaveDebugInfo($"Number of label elements: {elementsToDisplay.Count}");
                
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
                            (int)((double)labelWidth * DPI_SCALE),
                            (int)((double)labelHeight * DPI_SCALE)
                        ),
                        Margin = new Padding(0)
                    };

                    // Add paint handler for the paper panel using a local method to avoid closure issues
                    paperPanel.Paint += PaperPanelPaint;

                    // Add print button
                    Button printButton = new Button
                    {
                        Text = "Print",
                        Dock = DockStyle.Bottom,
                        Height = 40
                    };
                    printButton.Click += PrintButtonClick;

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
                    
                    // Local function for paper panel paint to keep everything needed in scope
                    void PaperPanelPaint(object s, PaintEventArgs pe)
                    {
                        try 
                        {
                            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            
                            // Draw all elements with actual item data
                            foreach (var element in elementsToDisplay)
                            {
                                try
                                {
                                    // Create adjusted bounds to correctly position elements within the preview
                                    var adjustedBounds = new Rectangle(
                                        element.Bounds.X - MARGIN,
                                        element.Bounds.Y - MARGIN,
                                        element.Bounds.Width,
                                        element.Bounds.Height
                                    );
                                    
                                    // Ensure no negative coordinates
                                    if (adjustedBounds.X < 0) adjustedBounds.X = 0;
                                    if (adjustedBounds.Y < 0) adjustedBounds.Y = 0;
                                    
                                    if (element is TextElement textElement)
                                    {
                                        RenderTextElement(pe.Graphics, textElement, adjustedBounds, sampleItem);
                                    }
                                    else if (element is QRElement qrElement)
                                    {
                                        RenderQRElement(pe.Graphics, qrElement, adjustedBounds, sampleItem);
                                    }
                                    else if (element is ImageElement imageElement)
                                    {
                                        RenderImageElement(pe.Graphics, imageElement, adjustedBounds, sampleItem);
                                    }
                                }
                                catch (Exception elementEx)
                                {
                                    SaveDebugInfo($"Error rendering element: {elementEx.Message}");
                                }
                            }
                        }
                        catch (Exception paintEx)
                        {
                            SaveDebugInfo($"Error during paint event: {paintEx.Message}");
                        }
                    }
                    
                    // Local function for print button click
                    void PrintButtonClick(object s, EventArgs pe)
                    {
                        try
                        {
                            using (var printDialog = new PrintDialog())
                            {
                                // Create a PrintDocument for printing
                                var printDocument = new PrintDocument();
                                printDocument.DocumentName = $"Label - {sampleItem?.ModelNumber ?? "Unknown"}";
                                
                                // Get settings
                                var settings = SettingsManager.GetSettings();
                                
                                // Set default printer if configured
                                if (!string.IsNullOrEmpty(settings.DefaultLabelPrinter))
                                {
                                    try
                                    {
                                        // Check if the printer exists
                                        bool printerExists = false;
                                        foreach (string printer in PrinterSettings.InstalledPrinters)
                                        {
                                            if (printer == settings.DefaultLabelPrinter)
                                            {
                                                printerExists = true;
                                                break;
                                            }
                                        }
                                        
                                        if (printerExists)
                                        {
                                            printDocument.PrinterSettings.PrinterName = settings.DefaultLabelPrinter;
                                            SaveDebugInfo($"Using default label printer: {settings.DefaultLabelPrinter}");
                                        }
                                        else
                                        {
                                            SaveDebugInfo($"Configured printer '{settings.DefaultLabelPrinter}' not found");
                                        }
                                    }
                                    catch (Exception printerEx)
                                    {
                                        SaveDebugInfo($"Error setting printer: {printerEx.Message}");
                                    }
                                }
                                
                                // Set the PrintDocument to the PrintDialog
                                printDialog.Document = printDocument;
                                
                                // Get the print orientation from settings
                                bool isPortrait = settings.PrintOrientation == "Portrait";
                                SaveDebugInfo($"Print orientation: {settings.PrintOrientation}");
                                
                                // Handle the printing
                                printDocument.PrintPage += (sender, e) => 
                                {
                                    // Set high quality rendering for print output
                                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                                    
                                    // Calculate label dimensions in pixels
                                    int labelWidthPx = (int)((double)labelWidth * DPI_SCALE);
                                    int labelHeightPx = (int)((double)labelHeight * DPI_SCALE);
                                        
                                    // Create a bitmap to draw the content - increase resolution to create higher quality output
                                    using (var bitmap = new Bitmap(labelWidthPx * 2, labelHeightPx * 2))
                                    {
                                        using (var g = Graphics.FromImage(bitmap))
                                        {
                                            // Set high quality rendering for the bitmap
                                            g.SmoothingMode = SmoothingMode.AntiAlias;
                                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                            g.CompositingQuality = CompositingQuality.HighQuality;
                                            g.Clear(Color.White);
                                            
                                            // Draw all elements with actual item data
                                    foreach (var element in elementsToDisplay)
                                    {
                                                // Create adjusted bounds to correctly position elements
                                                var adjustedBounds = new Rectangle(
                                                    element.Bounds.X - MARGIN,
                                                    element.Bounds.Y - MARGIN,
                                                    element.Bounds.Width,
                                                    element.Bounds.Height
                                                );
                                                
                                                // Ensure no negative coordinates
                                                if (adjustedBounds.X < 0) adjustedBounds.X = 0;
                                                if (adjustedBounds.Y < 0) adjustedBounds.Y = 0;
                                            
                                            if (element is TextElement textElement)
                                            {
                                                    RenderTextElement(g, textElement, adjustedBounds, sampleItem);
                                            }
                                            else if (element is QRElement qrElement)
                                            {
                                                    RenderQRElement(g, qrElement, adjustedBounds, sampleItem);
                                            }
                                            else if (element is ImageElement imageElement)
                                            {
                                                    RenderImageElement(g, imageElement, adjustedBounds, sampleItem);
                                                }
                                            }
                                        }
                                        
                                        // Calculate target rectangle for drawing the bitmap in correct orientation
                                        Rectangle destRect;
                                        if (isPortrait)
                                                {
                                            destRect = new Rectangle(
                                                e.MarginBounds.Left,
                                                e.MarginBounds.Top,
                                                Math.Min(e.MarginBounds.Width, labelWidthPx),
                                                Math.Min(e.MarginBounds.Height, labelHeightPx)
                                            );
                                                        }
                                        else
                                        {
                                            // In landscape, swap width and height
                                            destRect = new Rectangle(
                                                e.MarginBounds.Left,
                                                e.MarginBounds.Top,
                                                Math.Min(e.MarginBounds.Width, labelHeightPx),
                                                Math.Min(e.MarginBounds.Height, labelWidthPx)
                                            );
                                        }
                                        
                                        // Draw the bitmap to the print document with high quality settings
                                        // Set destination and source rectangles for highest quality scaling
                                        Rectangle srcRect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                                        e.Graphics.DrawImage(bitmap, destRect, srcRect, GraphicsUnit.Pixel);
                                    }
                                    
                                    // Only print one page
                                    e.HasMorePages = false;
                                };
                                
                                // Show the print dialog directly instead of the print preview dialog
                                if (printDialog.ShowDialog() == DialogResult.OK)
                                {
                                    try
                                {
                                    printDocument.Print();
                                }
                                    catch (Exception printEx)
                                    {
                                        SaveDebugInfo($"Error during print: {printEx.Message}");
                                        MessageBox.Show($"Error printing: {printEx.Message}", "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                            }
                        }
                        catch (Exception ex)
                        {
                            SaveDebugInfo($"Error in PrintButtonClick: {ex.Message}");
                            MessageBox.Show($"Error preparing to print: {ex.Message}", "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SaveDebugInfo($"Error generating print preview: {ex.Message}");
                MessageBox.Show($"An error occurred while generating the print preview: {ex.Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Helper method to render text elements in the preview
        private void RenderTextElement(Graphics g, TextElement textElement, Rectangle bounds, Item item)
        {
            try
            {
                string text = textElement.GetText();
                
                // Replace placeholders with actual values
                switch (text.Trim())
                {
                    case "Model Number":
                        text = item.ModelNumber ?? "";
                        break;
                    case "Description":
                        text = item.Description ?? "";
                        break;
                    case "Supplier":
                        text = item.Supplier ?? "";
                        break;
                    case "Category":
                        text = item.CategoryPath ?? "Uncategorized";
                        break;
                    case "Default Order Quantity":
                        text = item.DefaultOrderQuantity.ToString();
                        break;
                    case "Product URL":
                        text = item.ProductUrl ?? "";
                        break;
                    default:
                        // Check for field names without exact case matching
                        if (text.Trim().Equals("supplier", StringComparison.OrdinalIgnoreCase))
                        {
                            text = item.Supplier ?? "";
                        }
                        else if (text.Trim().Equals("description", StringComparison.OrdinalIgnoreCase))
                        {
                            text = item.Description ?? "";
                        }
                        else if (text.Trim().Equals("category", StringComparison.OrdinalIgnoreCase))
                        {
                            text = item.CategoryPath ?? "Uncategorized";
                        }
                        else if (text.Trim().Equals("model number", StringComparison.OrdinalIgnoreCase))
                        {
                            text = item.ModelNumber ?? "";
                        }
                        else if (text.Trim().Equals("default order quantity", StringComparison.OrdinalIgnoreCase))
                        {
                            text = item.DefaultOrderQuantity.ToString();
                        }
                        else if (text.Trim().Equals("product url", StringComparison.OrdinalIgnoreCase))
                        {
                            text = item.ProductUrl ?? "";
                        }
                        // If it contains placeholders in curly braces
                        else if (text.Contains("{"))
                        {
                            text = text.Replace("{ModelNumber}", item.ModelNumber ?? "")
                                     .Replace("{Description}", item.Description ?? "")
                                     .Replace("{Supplier}", item.Supplier ?? "")
                                     .Replace("{Category}", item.CategoryPath ?? "Uncategorized")
                                     .Replace("{DefaultOrderQuantity}", item.DefaultOrderQuantity.ToString())
                                     .Replace("{ProductURL}", item.ProductUrl ?? "");
                        }
                        break;
                }
                
                // Create a string format for text alignment
                var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                
                // Draw the text with appropriate color
                Brush textBrush = string.IsNullOrWhiteSpace(text) ? Brushes.Red : Brushes.Black;
                
                // Calculate best font size to fit the bounds
                float bestSize = CalculateBestFitFontSize(
                    g, 
                    text, 
                    new FontFamily(textElement.GetFontFamily()), 
                    Rectangle.Round(bounds));
                
                using (var font = new Font(textElement.GetFontFamily(), bestSize))
                {
                    g.DrawString(text, font, textBrush, bounds, format);
                }
                
                // Draw a border around text elements for better visibility
                g.DrawRectangle(Pens.LightGray, bounds);
            }
            catch (Exception ex)
            {
                SaveDebugInfo($"Error rendering text element: {ex.Message}");
            }
        }

        // Helper method to render QR elements in the preview
        private void RenderQRElement(Graphics g, QRElement qrElement, Rectangle bounds, Item item)
        {
            try
            {
                // Check if we can use the original high-resolution bitmap
                var originalBitmap = qrElement.GetOriginalBitmap();
                if (originalBitmap != null)
                {
                    // Use the high-res bitmap directly
                    // Configure high-quality drawing
                    var oldInterpolationMode = g.InterpolationMode;
                    var oldPixelOffsetMode = g.PixelOffsetMode;
                    var oldCompositingQuality = g.CompositingQuality;
                    var oldSmoothingMode = g.SmoothingMode;
                    
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    
                    // Draw the original high-res QR code image using PixelFormat-preserving method
                    g.DrawImage(originalBitmap, bounds);
                    
                    // Restore previous graphics settings
                    g.InterpolationMode = oldInterpolationMode;
                    g.PixelOffsetMode = oldPixelOffsetMode;
                    g.CompositingQuality = oldCompositingQuality;
                    g.SmoothingMode = oldSmoothingMode;
                    
                    SaveDebugInfo("Successfully rendered QR code from high-res bitmap");
                    return;
                }
                
                // If no original bitmap is available, continue with standard generation
                // Get the QR content template, defaulting to a basic template if none exists
                string qrContent = qrElement.GetTemplateContent();
                if (string.IsNullOrEmpty(qrContent))
                {
                    qrContent = "Model: {ModelNumber} | Description: {Description}";
                    SaveDebugInfo("Using default QR content template");
                }

                // Replace placeholders with actual values
                try
                {
                    qrContent = qrContent.Replace("{ModelNumber}", item?.ModelNumber ?? "")
                                       .Replace("{Description}", item?.Description ?? "")
                                       .Replace("{Supplier}", item?.Supplier ?? "")
                                       .Replace("{Category}", item?.CategoryPath ?? "Uncategorized")
                                       .Replace("{DefaultOrderQuantity}", item?.DefaultOrderQuantity.ToString() ?? "0")
                                       .Replace("{ProductURL}", item?.ProductUrl ?? "");
                    
                    SaveDebugInfo($"Prepared QR content: {qrContent.Substring(0, Math.Min(50, qrContent.Length))}...");
                }
                catch (Exception contentEx)
                {
                    // If placeholder replacement fails, use a simpler content
                    SaveDebugInfo($"Error replacing QR placeholders: {contentEx.Message}");
                    qrContent = item?.ModelNumber ?? "QR Data Error";
                }

                // Generate new QR code with actual data
                try
                {
                    var writer = new BarcodeWriter<Bitmap>
                    {
                        Format = BarcodeFormat.QR_CODE,
                        Options = new QrCodeEncodingOptions
                        {
                            // Create larger QR code for better resolution
                            Width = Math.Max(600, bounds.Width * 4),  // Increased resolution
                            Height = Math.Max(600, bounds.Height * 4), // Increased resolution
                            Margin = 1,
                            ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                        },
                        Renderer = new BitmapRenderer
                        {
                            // Specify the background and foreground colors explicitly
                            Background = Color.White,
                            Foreground = Color.Black
                        }
                    };

                    // Generate and draw the QR code using intermediate bitmap to preserve quality
                    using (var generatedQrImage = writer.Write(qrContent))
                    using (var intermediateBitmap = new Bitmap(generatedQrImage.Width, generatedQrImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        // Create a high-quality copy
                        using (var intermediateGraphics = Graphics.FromImage(intermediateBitmap))
                        {
                            intermediateGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            intermediateGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            intermediateGraphics.CompositingQuality = CompositingQuality.HighQuality;
                            intermediateGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                            
                            // Draw with white background first
                            intermediateGraphics.Clear(Color.White);
                            intermediateGraphics.DrawImage(generatedQrImage, 0, 0, generatedQrImage.Width, generatedQrImage.Height);
                        }
                        
                        // Set high quality rendering for the destination graphics
                        var oldInterpolationMode = g.InterpolationMode;
                        var oldPixelOffsetMode = g.PixelOffsetMode;
                        var oldCompositingQuality = g.CompositingQuality;
                        var oldSmoothingMode = g.SmoothingMode;
                        
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        
                        // Draw the intermediate bitmap to the destination
                        g.DrawImage(intermediateBitmap, bounds);
                        
                        // Restore previous graphics settings
                        g.InterpolationMode = oldInterpolationMode;
                        g.PixelOffsetMode = oldPixelOffsetMode;
                        g.CompositingQuality = oldCompositingQuality;
                        g.SmoothingMode = oldSmoothingMode;
                        
                        SaveDebugInfo("Successfully rendered QR code through intermediate bitmap");
                    }
                }
                catch (Exception qrEx)
                {
                    // If QR generation fails, draw a placeholder
                    SaveDebugInfo($"Error generating QR code: {qrEx.Message}");
                    DrawQRPlaceholder(g, bounds, item);
                }
            }
            catch (Exception ex)
            {
                SaveDebugInfo($"Error in RenderQRElement: {ex.Message}");
                DrawQRPlaceholder(g, bounds, item);
            }
        }
        
        // Helper method to draw a placeholder for QR codes when generation fails
        private void DrawQRPlaceholder(Graphics g, Rectangle bounds, Item item = null)
        {
            // Fill with light background
            using (var brush = new SolidBrush(Color.FromArgb(240, 240, 240)))
            {
                g.FillRectangle(brush, bounds);
            }
            
            // Draw border
            g.DrawRectangle(Pens.DarkGray, bounds);
            
            // Draw cross pattern to indicate it's a placeholder
            g.DrawLine(Pens.LightGray, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            g.DrawLine(Pens.LightGray, bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
            
            // Draw grid pattern to resemble QR code
            int cellSize = Math.Max(4, bounds.Width / 10);
            for (int x = bounds.Left; x < bounds.Right; x += cellSize)
            {
                g.DrawLine(Pens.LightGray, x, bounds.Top, x, bounds.Bottom);
            }
            for (int y = bounds.Top; y < bounds.Bottom; y += cellSize)
            {
                g.DrawLine(Pens.LightGray, bounds.Left, y, bounds.Right, y);
            }
            
            // Add text
            using (var font = new Font("Arial", 9, FontStyle.Bold))
            {
                float bestSize = CalculateBestFitFontSize(g, "QR", font.FontFamily, bounds);
                using (var scaledFont = new Font(font.FontFamily, bestSize, FontStyle.Bold))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    
                    g.DrawString("QR", scaledFont, Brushes.Black, bounds, format);
                }
            }
        }

        // Helper method to render image elements in the preview
        private void RenderImageElement(Graphics g, ImageElement imageElement, Rectangle bounds, Item item)
        {
            try
            {
                string imagePath = imageElement.GetImagePath();
                bool imageDrawn = false;
                
                SaveDebugInfo($"RenderImageElement: Starting render with element path: {imagePath ?? "null"}");
                SaveDebugInfo($"RenderImageElement: Item image path: {item?.ImagePath ?? "null"}");
                
                // Option 1: Try to use the item's actual image first (highest priority)
                if (item != null && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                {
                    try
                    {
                        using (var itemImage = Image.FromFile(item.ImagePath))
                        {
                            g.DrawImage(itemImage, bounds);
                            imageDrawn = true;
                            SaveDebugInfo($"RenderImageElement: Successfully rendered item's actual image: {item.ImagePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        SaveDebugInfo($"RenderImageElement: Error loading item image: {ex.Message}");
                        // Continue to next option
                    }
                }
                else if (item == null)
                {
                    SaveDebugInfo("RenderImageElement: Item is null");
                }
                else if (string.IsNullOrEmpty(item.ImagePath))
                {
                    SaveDebugInfo("RenderImageElement: Item's ImagePath is empty");
                }
                else if (!File.Exists(item.ImagePath))
                {
                    SaveDebugInfo($"RenderImageElement: Item's image file does not exist at path: {item.ImagePath}");
                }
                
                // Option 2: If item image failed, try the image element's path (if it's not a placeholder)
                if (!imageDrawn && !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath) && 
                   !imageElement.IsPlaceholder())
                {
                    try
                    {
                        using (var image = Image.FromFile(imagePath))
                        {
                            g.DrawImage(image, bounds);
                            imageDrawn = true;
                            SaveDebugInfo($"RenderImageElement: Rendered image element's path: {imagePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        SaveDebugInfo($"RenderImageElement: Error loading image element's image: {ex.Message}");
                        // Continue to placeholder
                    }
                }
                
                // Option 3: If all fails, draw a placeholder with the item information if available
                if (!imageDrawn)
                {
                    SaveDebugInfo($"RenderImageElement: All image loading attempts failed, using placeholder");
                    DrawImagePlaceholder(g, bounds, item);
                }
            }
            catch (Exception ex)
            {
                SaveDebugInfo($"RenderImageElement: Error processing image element: {ex.Message}");
                DrawImagePlaceholder(g, bounds, item);
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
                                        Width = 600,
                                        Height = 600,
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
                                            g.DrawString("Item Image", new Font("Arial", 12), Brushes.Gray, 
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

        // Helper method to draw a placeholder for images
        private void DrawImagePlaceholder(Graphics g, Rectangle bounds, Item item = null)
        {
            // Try to use the item's image if available
            bool imageDrawn = false;
            
            if (item != null && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                try
                {
                    using (var itemImage = Image.FromFile(item.ImagePath))
                    {
                        g.DrawImage(itemImage, bounds);
                        imageDrawn = true;
                        SaveDebugInfo($"DrawImagePlaceholder: Successfully rendered item image: {item.ImagePath}");
                    }
                }
                catch (Exception ex)
                {
                    SaveDebugInfo($"DrawImagePlaceholder: Error loading item image: {ex.Message}");
                    // Continue to placeholder
                }
            }
            else
            {
                // Log why image can't be drawn
                if (item == null)
                {
                    SaveDebugInfo("DrawImagePlaceholder: Item is null");
                }
                else if (string.IsNullOrEmpty(item.ImagePath))
                {
                    SaveDebugInfo("DrawImagePlaceholder: Item's ImagePath is empty");
                }
                else if (!File.Exists(item.ImagePath))
                {
                    SaveDebugInfo($"DrawImagePlaceholder: Item's image file does not exist at path: {item.ImagePath}");
                }
            }
            
            // If we couldn't draw the image, show a placeholder
            if (!imageDrawn)
            {
                SaveDebugInfo("DrawImagePlaceholder: Drawing placeholder text and graphics");
                
                // Draw a more visually distinct placeholder
                using (var brush = new SolidBrush(Color.FromArgb(240, 240, 240)))
                {
                    g.FillRectangle(brush, bounds);
                }
                
                // Draw border
                g.DrawRectangle(Pens.DarkGray, bounds);
                
                // Draw diagonal lines to indicate it's a placeholder
                g.DrawLine(Pens.LightGray, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                g.DrawLine(Pens.LightGray, bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
                
                // Draw text label
                using (var font = new Font("Arial", 9, FontStyle.Bold))
                {
                    // Determine the best font size to fill the bounds
                    float bestSize = CalculateBestFitFontSize(g, "No Image Available", font.FontFamily, bounds);
                    using (var scaledFont = new Font(font.FontFamily, bestSize, FontStyle.Bold))
                    {
                        var format = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        
                        // Draw the text with a light gray background to make it more readable
                        g.DrawString("No Image Available", scaledFont, Brushes.Black, bounds, format);
                    }
                }
            }
        }
        
        // Calculate the best font size to fit text within bounds
        private float CalculateBestFitFontSize(Graphics g, string text, FontFamily fontFamily, Rectangle bounds)
        {
            if (string.IsNullOrEmpty(text)) return 9f;
            
            float maxSize = 36f;  // Start with a fairly large size
            float minSize = 6f;   // Minimum readable size
            float bestSize = minSize;
            
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.None
            };
            
            // Binary search for the best size
            while (maxSize - minSize > 0.5f)
            {
                float fontSize = (minSize + maxSize) / 2;
                using (var testFont = new Font(fontFamily, fontSize))
                {
                    SizeF textSize = g.MeasureString(text, testFont, bounds.Size, format);
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
            
            return bestSize;
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

            // Start with a larger font size and scale down until it fits
            float maxSize = Math.Max(font.Size * 2, 72);  // Allow for larger maximum sizing
            float minSize = 4;  // Minimum readable size
            float bestSize = minSize;
            
            // Binary search for the right font size
            while (maxSize - minSize > 0.5f)
            {
                float fontSize = (minSize + maxSize) / 2;
                using (var testFont = new Font(font.FontFamily, fontSize))
                {
                    SizeF textSize = g.MeasureString(text, testFont, bounds.Size, format);
                    
                    // Use a smaller margin to allow text to better fill the space
                    if (textSize.Width <= bounds.Width - 2 && textSize.Height <= bounds.Height - 2)
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
            try
            {
                image = Image.FromFile(imagePath);
                float ratio = (float)image.Width / image.Height;
                bounds = new Rectangle(location, new Size((int)(100 * ratio), 100));
            }
            catch (Exception ex)
            {
                // Log error but continue with a default size for the element
                Console.WriteLine($"Error loading image in ImageElement: {ex.Message}");
                bounds = new Rectangle(location, new Size(100, 100));
            }
        }

        public string GetImagePath() => imagePath;
        
        // Check if this is a placeholder image
        public bool IsPlaceholder() 
        {
            // If there's no path, it's definitely a placeholder
            if (string.IsNullOrEmpty(imagePath))
                return true;
                
            // Check if the path contains placeholder in the name
            if (imagePath.ToLower().Contains("placeholder") || 
                Path.GetFileName(imagePath).ToLower() == "placeholder.png")
                return true;
                
            // Check if the file doesn't exist
            if (!File.Exists(imagePath))
                return true;
                
            // Check if the image is the "Item Image" placeholder text image
            try {
                if (image != null && image.Width <= 100 && image.Height <= 100)
                {
                    // Small image dimensions might be a placeholder
                    // We could do more complex checking here if needed
                    // such as checking pixel values for known placeholder patterns
                }
            }
            catch {
                // If we get an error checking the image, treat it as a placeholder
                return true;
            }
            
            return false;
        }

        public void UpdateImagePath(string newImagePath)
        {
            if (string.IsNullOrEmpty(newImagePath) || !File.Exists(newImagePath))
                return;

            // Dispose old image
            image?.Dispose();
            
            // Update path and image
            imagePath = newImagePath;
            try
            {
                image = Image.FromFile(newImagePath);
                
                // Maintain aspect ratio when updating image
                if (maintainAspectRatio && image != null)
                {
                    float ratio = (float)image.Width / image.Height;
                    bounds.Height = (int)(bounds.Width / ratio);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating image: {ex.Message}");
            }
        }

        public override void Draw(Graphics g)
        {
            if (image != null)
            {
                g.DrawImage(image, bounds);
            }
            else
            {
                // Draw a placeholder rectangle with text if image is null
                g.DrawRectangle(Pens.Gray, bounds);
                using (var font = new Font("Arial", 9))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("Image Placeholder", font, Brushes.Gray, bounds, format);
                }
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
        private Bitmap originalBitmap;  // Store the original high resolution bitmap

        public QRElement(Image qrImage, Point location, string templateKey = null, string templateContent = null)
        {
            this.templateKey = templateKey;
            this.templateContent = templateContent;
            
            // Store high-resolution bitmap for better quality rendering
            if (qrImage is Bitmap bmp)
            {
                try
                {
                    // Create a deep copy of the bitmap with the same pixel format
                    Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    this.originalBitmap = bmp.Clone(rect, bmp.PixelFormat);
                    this.qrImage = new Bitmap(this.originalBitmap);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cloning bitmap: {ex.Message}");
                    // Fallback method if clone fails
                    this.originalBitmap = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(this.originalBitmap))
                    {
                        g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                    }
                    this.qrImage = new Bitmap(this.originalBitmap);
                }
            }
            else if (qrImage != null)
            {
                try
                {
                    // Convert to bitmap if it isn't already
                    this.originalBitmap = new Bitmap(qrImage.Width, qrImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(this.originalBitmap))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(qrImage, 0, 0, qrImage.Width, qrImage.Height);
                    }
                    this.qrImage = new Bitmap(this.originalBitmap);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating bitmap from image: {ex.Message}");
                    // If all else fails, create a new blank bitmap
                    int size = Math.Max(qrImage.Width, qrImage.Height);
                    this.originalBitmap = new Bitmap(size, size);
                    this.qrImage = new Bitmap(size, size);
                }
            }
            
            // QR codes should be square
            bounds = new Rectangle(location, new Size(100, 100));
        }

        public string GetTemplateKey() => templateKey;
        public string GetTemplateContent() => templateContent;
        
        // Get the original high-res bitmap for printing
        public Bitmap GetOriginalBitmap() => originalBitmap;

        public override void Draw(Graphics g)
        {
            if (qrImage != null)
            {
                // Set high quality drawing for the QR code
                var oldMode = g.InterpolationMode;
                var oldPixelMode = g.PixelOffsetMode;
                var oldCompositeMode = g.CompositingQuality;
                
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                
                g.DrawImage(qrImage, bounds);
                
                // Restore previous graphic settings
                g.InterpolationMode = oldMode;
                g.PixelOffsetMode = oldPixelMode;
                g.CompositingQuality = oldCompositeMode;
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
            originalBitmap?.Dispose();
            qrImage?.Dispose();
        }
    }
} 