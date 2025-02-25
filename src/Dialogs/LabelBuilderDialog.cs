using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using PrintSystem.Models;
using PrintSystem.Managers;

namespace PrintSystem.Dialogs
{
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

        // Store the current item if we're designing from an item context
        private Item currentItem;
        
        // Store label elements
        private List<LabelElement> labelElements;
        private LabelElement selectedElement;
        private Point dragStartPoint;
        private bool isDragging;

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
            if (currentItem != null)
            {
                PopulateFieldsWithItem();
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
            templateComboBox.Items.AddRange(new object[] {
                "Basic Label",
                "Product Label",
                "Price Label",
                "Custom"
            });

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

            templateLayout.Controls.Add(templateComboBox, 0, 0);
            templateLayout.Controls.Add(saveTemplateButton, 1, 0);
            templateLayout.Controls.Add(loadTemplateButton, 1, 1);
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
                Height = 60,
                Padding = new Padding(5)
            };

            generateQRButton = new Button
            {
                Text = "Add QR Code",
                Dock = DockStyle.Fill
            };
            generateQRButton.Click += GenerateQRButton_Click;
            qrGroup.Controls.Add(generateQRButton);
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

            printButton = new Button
            {
                Text = "Print Preview",
                Width = 100,
                Height = 30,
                Location = new Point(bottomPanel.Width - 110, 5),
                Anchor = AnchorStyles.Right
            };
            printButton.Click += PrintButton_Click;
            bottomPanel.Controls.Add(printButton);
            canvasLayout.Controls.Add(bottomPanel, 0, 1);

            rightPanel.Controls.Add(canvasLayout);

            // Add panels to main layout
            mainLayout.Controls.Add(toolbox, 0, 0);
            mainLayout.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainLayout);

            // Set initial template
            templateComboBox.SelectedIndex = 0;
            
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
            using (var qrDialog = new QRBuilderDialog(currentItem))
            {
                if (qrDialog.ShowDialog() == DialogResult.OK)
                {
                    // Get the QR code image and add it as an element
                    if (qrDialog.QRImage != null)
                    {
                        Point location = new Point(GRID_SIZE, GRID_SIZE);
                        var qrElement = new QRElement(qrDialog.QRImage, location);
                        labelElements.Add(qrElement);
                        selectedElement = qrElement;
                        designCanvas.Invalidate();
                    }
                }
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
        }
    }

    // Label element classes
    public abstract class LabelElement : IDisposable
    {
        protected Rectangle bounds;
        protected Point dragOffset;
        protected bool isResizing;
        protected int resizeHandle = -1;
        
        public Rectangle Bounds => bounds;
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

        public ImageElement(string imagePath, Point location)
        {
            image = Image.FromFile(imagePath);
            float ratio = (float)image.Width / image.Height;
            bounds = new Rectangle(location, new Size((int)(100 * ratio), 100));
        }

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

    public class QRElement : ImageElement
    {
        public QRElement(Image qrImage, Point location) : base(null, location)
        {
            // QR codes should be square
            bounds = new Rectangle(location, new Size(100, 100));
        }

        public override void Resize(Point mouseLocation)
        {
            base.Resize(mouseLocation);
            // Keep QR code square
            bounds.Height = bounds.Width;
        }
    }
} 