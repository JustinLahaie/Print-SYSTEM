using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using PrintSystem.Models;
using PrintSystem.Managers;
using PrintSystem.Dialogs;
using CategoryManager = PrintSystem.Managers.CategoryManager;
using System.Text.Json;
using ZXing.Windows.Compatibility;

namespace PrintSystem.Forms
{
    public class AddItemForm : Form
    {
        private TextBox modelNumberTextBox;
        private TextBox descriptionTextBox;
        private ComboBox supplierComboBox;
        private ComboBox categoryComboBox;
        private NumericUpDown quantityNumericUpDown;
        private PictureBox imagePreview;
        private Button browseButton;
        private Button saveButton;
        private Button cancelButton;
        private Button addSupplierButton;
        private Button addCategoryButton;
        private string selectedImagePath;
        private TextBox debugOutput;
        private Panel urlPanel;
        
        // New controls for URL scraping
        private TextBox urlTextBox;
        private Button scrapeButton;
        private bool isEditMode;

        public Item Item { get; private set; }

        public AddItemForm(Item itemToEdit = null)
        {
            isEditMode = itemToEdit != null;
            InitializeComponents();
            LoadSuppliers();
            LoadCategories();

            if (isEditMode)
            {
                // Load existing item data
                this.Text = "Edit Item";
                modelNumberTextBox.Text = itemToEdit.ModelNumber;
                descriptionTextBox.Text = itemToEdit.Description;
                supplierComboBox.Text = itemToEdit.Supplier;
                
                // Find and select the correct category (whether wrapped or not)
                foreach (var item in categoryComboBox.Items)
                {
                    var category = item is CategoryWrapper wrapper ? wrapper.Category : item as Category;
                    if (category == itemToEdit.Category)
                    {
                        categoryComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                quantityNumericUpDown.Value = itemToEdit.DefaultOrderQuantity;
                urlTextBox.Text = itemToEdit.ProductUrl;
                
                if (!string.IsNullOrEmpty(itemToEdit.ImagePath) && File.Exists(itemToEdit.ImagePath))
                {
                    selectedImagePath = itemToEdit.ImagePath;
                    try
                    {
                        if (imagePreview.Image != null)
                        {
                            imagePreview.Image.Dispose();
                        }
                        imagePreview.Image = Image.FromFile(selectedImagePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}", "Image Load Error");
                    }
                }

                // In edit mode, make URL clickable
                if (!string.IsNullOrEmpty(itemToEdit.ProductUrl))
                {
                    var urlLinkLabel = new LinkLabel
                    {
                        Text = "Open Product URL",
                        Location = new Point(scrapeButton.Right + 10, scrapeButton.Top),
                        AutoSize = true
                    };
                    urlLinkLabel.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = itemToEdit.ProductUrl,
                        UseShellExecute = true
                    });
                    urlPanel.Controls.Add(urlLinkLabel);
                }
            }
        }

        private void LoadSuppliers()
        {
            supplierComboBox.Items.Clear();
            foreach (var supplier in SupplierManager.GetSuppliers())
            {
                supplierComboBox.Items.Add(supplier);
            }
            
            // When supplier changes, reload categories
            supplierComboBox.SelectedIndexChanged += (s, e) => LoadCategories();
            
            // If editing an item, select its supplier
            if (isEditMode && Item != null && !string.IsNullOrEmpty(Item.Supplier))
            {
                supplierComboBox.Text = Item.Supplier;
            }
            // Otherwise, if there are suppliers, select the first one
            else if (supplierComboBox.Items.Count > 0)
            {
                supplierComboBox.SelectedIndex = 0;
            }
        }

        private void InitializeComponents()
        {
            this.Text = "Add New Item";
            this.Size = new Size(800, 800); // Increased size for debug output
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Create layout
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8, // Increased for debug output
                Padding = new Padding(10)
            };

            // URL Input (New)
            layout.Controls.Add(new Label { Text = "Product URL:" }, 0, 0);
            urlPanel = new Panel { Dock = DockStyle.Fill };
            urlTextBox = new TextBox { Width = 200 };
            scrapeButton = new Button
            {
                Text = "Import",
                Location = new Point(210, 0),
                Width = 70
            };
            scrapeButton.Click += ScrapeButton_Click;
            urlPanel.Controls.Add(urlTextBox);
            urlPanel.Controls.Add(scrapeButton);
            layout.Controls.Add(urlPanel, 1, 0);

            // Model Number
            layout.Controls.Add(new Label { Text = "Model Number:" }, 0, 1);
            modelNumberTextBox = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(modelNumberTextBox, 1, 1);

            // Description
            layout.Controls.Add(new Label { Text = "Description:" }, 0, 2);
            descriptionTextBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60 };
            layout.Controls.Add(descriptionTextBox, 1, 2);

            // Supplier
            layout.Controls.Add(new Label { Text = "Supplier:" }, 0, 3);
            Panel supplierPanel = new Panel { Dock = DockStyle.Fill };
            supplierComboBox = new ComboBox { Width = 200 };
            supplierComboBox.DropDownStyle = ComboBoxStyle.DropDown;
            
            addSupplierButton = new Button
            {
                Text = "Add New",
                Location = new Point(210, 0),
                Width = 70
            };
            addSupplierButton.Click += AddSupplierButton_Click;
            
            supplierPanel.Controls.Add(supplierComboBox);
            supplierPanel.Controls.Add(addSupplierButton);
            layout.Controls.Add(supplierPanel, 1, 3);

            // Category Selection
            layout.Controls.Add(new Label { Text = "Category:" }, 0, 4);
            Panel categoryPanel = new Panel { Dock = DockStyle.Fill };
            categoryComboBox = new ComboBox { Width = 200 };
            categoryComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            
            addCategoryButton = new Button
            {
                Text = "Add New",
                Location = new Point(210, 0),
                Width = 70
            };
            addCategoryButton.Click += AddCategoryButton_Click;
            
            categoryPanel.Controls.Add(categoryComboBox);
            categoryPanel.Controls.Add(addCategoryButton);
            layout.Controls.Add(categoryPanel, 1, 4);

            // Default Order Quantity
            layout.Controls.Add(new Label { Text = "Default Order Quantity:" }, 0, 5);
            quantityNumericUpDown = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 1000 };
            layout.Controls.Add(quantityNumericUpDown, 1, 5);

            // Image Preview and Upload
            layout.Controls.Add(new Label { Text = "Image:" }, 0, 6);
            Panel imagePanel = new Panel { Dock = DockStyle.Fill };
            
            imagePreview = new PictureBox
            {
                Size = new Size(150, 150),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            imagePanel.Controls.Add(imagePreview);

            browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(160, 60)
            };
            browseButton.Click += BrowseButton_Click;
            imagePanel.Controls.Add(browseButton);
            
            layout.Controls.Add(imagePanel, 1, 6);

            // Buttons
            Panel buttonPanel = new Panel { Dock = DockStyle.Fill };
            saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(0, 0)
            };
            saveButton.Click += SaveButton_Click;
            
            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(100, 0)
            };

            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);
            layout.Controls.Add(buttonPanel, 1, 7);

            // Add Print Preview Button
            var printPreviewButton = new Button
            {
                Text = "Print Preview",
                Width = 120,
                Height = 30,
                Dock = DockStyle.Right,
                Margin = new Padding(0, 5, 0, 5)
            };
            printPreviewButton.Click += PrintPreviewButton_Click;
            layout.Controls.Add(printPreviewButton, 1, 8);

            // Add Debug Output
            layout.Controls.Add(new Label { Text = "Debug Output:" }, 0, 9);
            debugOutput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Height = 150,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9)
            };
            layout.Controls.Add(debugOutput, 1, 9);

            this.Controls.Add(layout);
        }

        private void LoadCategories()
        {
            categoryComboBox.Items.Clear();
            string supplier = supplierComboBox.Text;
            if (string.IsNullOrEmpty(supplier)) return;

            var categories = CategoryManager.GetCategories(supplier);
            foreach (var category in categories)
            {
                if (category.ParentCategory == null && !category.Name.Equals(supplier, StringComparison.OrdinalIgnoreCase))  // Only add top-level categories here, excluding supplier category
                {
                    categoryComboBox.Items.Add(new CategoryWrapper(category, 0));
                    // Add subcategories with indentation
                    AddSubcategoriesToComboBox(category, 1);
                }
            }
        }

        private void AddSubcategoriesToComboBox(Category category, int level)
        {
            foreach (var subcategory in category.SubCategories)
            {
                // Create an indented wrapper for the subcategory
                var wrapper = new CategoryWrapper(subcategory, level);
                categoryComboBox.Items.Add(wrapper);
                // Recursively add sub-subcategories with increased indentation
                AddSubcategoriesToComboBox(subcategory, level + 1);
            }
        }

        // Wrapper class to handle indentation in the ComboBox
        private class CategoryWrapper
        {
            private readonly Category category;
            private readonly int level;
            private const string INDENT = "    "; // 4 spaces for each level
            private const string TREE_SYMBOL = "└─ ";

            public CategoryWrapper(Category category, int level)
            {
                this.category = category;
                this.level = Math.Max(0, level); // Ensure level is never negative
            }

            public Category Category => category;

            public override string ToString()
            {
                if (level == 0)
                    return category.Name;
                    
                return string.Concat(string.Concat(Enumerable.Repeat(INDENT, level - 1)), TREE_SYMBOL, category.Name);
            }
        }

        private void LogDebug(string message)
        {
            if (debugOutput.InvokeRequired)
            {
                debugOutput.Invoke(new Action(() => LogDebug(message)));
                return;
            }

            debugOutput.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + ": " + message + Environment.NewLine);
            debugOutput.SelectionStart = debugOutput.TextLength;
            debugOutput.ScrollToCaret();
        }

        private async void ScrapeButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(urlTextBox.Text))
            {
                MessageBox.Show("Please enter a product URL.", "Validation Error");
                return;
            }

            List<string> downloadedImages = null;

            try
            {
                debugOutput.Clear(); // Clear previous output
                scrapeButton.Enabled = false;
                scrapeButton.Text = "Importing...";
                Cursor = Cursors.WaitCursor;

                // Create a status label if it doesn't exist
                Label statusLabel = Controls.Find("statusLabel", true).FirstOrDefault() as Label;
                if (statusLabel == null)
                {
                    statusLabel = new Label
                    {
                        Name = "statusLabel",
                        AutoSize = true,
                        Location = new Point(scrapeButton.Right + 10, scrapeButton.Top + 3)
                    };
                    Controls.Add(statusLabel);
                }

                statusLabel.Text = "Fetching product information...";
                statusLabel.ForeColor = Color.Blue;
                Application.DoEvents();

                // Override console output to show in our debug window
                Console.SetOut(new DebugTextWriter(LogDebug));

                // Determine which scraper to use based on the URL
                var url = urlTextBox.Text.ToLower();
                dynamic product;
                string supplier;

                if (url.Contains("richelieu.com"))
                {
                    product = await RichelieuScraper.ScrapeProductAsync(urlTextBox.Text);
                    supplier = "Richelieu";
                }
                else if (url.Contains("marathonhardware.com"))
                {
                    product = await MarathonScraper.ScrapeProductAsync(urlTextBox.Text);
                    supplier = "Marathon";
                }
                else
                {
                    throw new Exception("Unsupported supplier URL. Currently supporting only Richelieu and Marathon Hardware.");
                }

                if (product != null)
                {
                    modelNumberTextBox.Text = product.ModelNumber;
                    descriptionTextBox.Text = product.Description;
                    supplierComboBox.Text = supplier;

                    string imagesFolder = Path.Combine(Application.StartupPath, "Images");

                    if ((supplier == "Marathon" && product.ImageUrls?.Count > 0) ||
                        (supplier == "Richelieu" && product.ImageUrls?.Count > 0))
                    {
                        statusLabel.Text = "Downloading images...";
                        LogDebug("Starting image downloads...");
                        Application.DoEvents();

                        downloadedImages = supplier == "Marathon" 
                            ? await MarathonScraper.DownloadImagesAsync(product.ImageUrls, imagesFolder)
                            : await RichelieuScraper.DownloadImagesAsync(product.ImageUrls, imagesFolder);
                        
                        if (downloadedImages.Count > 0)
                        {
                            if (downloadedImages.Count == 1)
                            {
                                // If there's only one image, select it automatically
                                selectedImagePath = downloadedImages[0];
                                if (imagePreview.Image != null)
                                {
                                    imagePreview.Image.Dispose();
                                }
                                imagePreview.Image = Image.FromFile(selectedImagePath);
                                statusLabel.Text = "Import completed successfully";
                                statusLabel.ForeColor = Color.Green;
                                LogDebug("Import completed successfully");
                            }
                            else
                            {
                                using (var dialog = new ImageSelectionDialog(downloadedImages))
                                {
                                    if (dialog.ShowDialog() == DialogResult.OK)
                                    {
                                        selectedImagePath = dialog.SelectedImagePath;
                                        if (imagePreview.Image != null)
                                        {
                                            imagePreview.Image.Dispose();
                                        }
                                        imagePreview.Image = Image.FromFile(selectedImagePath);
                                        statusLabel.Text = "Import completed successfully";
                                        statusLabel.ForeColor = Color.Green;
                                        LogDebug("Import completed successfully");
                                    }
                                    else
                                    {
                                        // User cancelled, clean up all downloaded images
                                        foreach (var image in downloadedImages)
                                        {
                                            try
                                            {
                                                if (File.Exists(image))
                                                    File.Delete(image);
                                            }
                                            catch (Exception ex)
                                            {
                                                LogDebug($"Failed to delete image {image}: {ex.Message}");
                                            }
                                        }
                                        selectedImagePath = null;
                                        statusLabel.Text = "Import cancelled";
                                        statusLabel.ForeColor = Color.Red;
                                    }
                                }
                            }

                            // Clean up unused images if we have a selected image
                            if (selectedImagePath != null)
                            {
                                if (supplier == "Marathon")
                                {
                                    MarathonScraper.CleanupUnusedImages(downloadedImages, selectedImagePath);
                                }
                                else
                                {
                                    RichelieuScraper.CleanupUnusedImages(downloadedImages, selectedImagePath);
                                }
                            }
                        }
                        else
                        {
                            statusLabel.Text = "No images found";
                            statusLabel.ForeColor = Color.Red;
                            LogDebug("No images found");
                        }
                    }
                    else
                    {
                        statusLabel.Text = "No images found";
                        statusLabel.ForeColor = Color.Red;
                        LogDebug("No images found");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error: {ex.Message}");
                MessageBox.Show($"Failed to import product information: {ex.Message}", "Error");
                Label statusLabel = Controls.Find("statusLabel", true).FirstOrDefault() as Label;
                if (statusLabel != null)
                {
                    statusLabel.Text = "Import failed";
                    statusLabel.ForeColor = Color.Red;
                }

                // Clean up any downloaded images if there was an error
                if (downloadedImages != null)
                {
                    foreach (var image in downloadedImages)
                    {
                        try
                        {
                            if (File.Exists(image))
                                File.Delete(image);
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                scrapeButton.Enabled = true;
                scrapeButton.Text = "Import";
                Cursor = Cursors.Default;
            }
        }

        private void AddSupplierButton_Click(object sender, EventArgs e)
        {
            using (var inputForm = new Form())
            {
                inputForm.Text = "Add New Supplier";
                inputForm.Size = new Size(300, 150);
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;

                var textBox = new TextBox { Location = new Point(10, 20), Width = 260 };
                var addButton = new Button { Text = "Add", Location = new Point(50, 60), DialogResult = DialogResult.OK };
                var cancelBtn = new Button { Text = "Cancel", Location = new Point(150, 60), DialogResult = DialogResult.Cancel };

                inputForm.Controls.AddRange(new Control[] { textBox, addButton, cancelBtn });
                inputForm.AcceptButton = addButton;
                inputForm.CancelButton = cancelBtn;

                if (inputForm.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    SupplierManager.AddSupplier(textBox.Text);
                    LoadSuppliers();
                    supplierComboBox.Text = textBox.Text;
                }
            }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
                openFileDialog.Title = "Select an Image";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedImagePath = openFileDialog.FileName;
                    if (imagePreview.Image != null)
                    {
                        imagePreview.Image.Dispose();
                    }
                    imagePreview.Image = Image.FromFile(selectedImagePath);
                }
            }
        }

        private void AddCategoryButton_Click(object sender, EventArgs e)
        {
            string supplier = supplierComboBox.Text;
            if (string.IsNullOrEmpty(supplier))
            {
                MessageBox.Show("Please select a supplier first.", "Validation Error");
                return;
            }

            using (var categoryDialog = new CategorySettingsDialog())
            {
                if (categoryDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadCategories();
                    // Try to select the newly added category if possible
                    // This will be the last category in the list for the current supplier
                    var categories = CategoryManager.GetCategories(supplier);
                    if (categories.Any())
                    {
                        var lastCategory = categories.Last();
                        foreach (var item in categoryComboBox.Items)
                        {
                            var category = item is CategoryWrapper wrapper ? wrapper.Category : item as Category;
                            if (category == lastCategory)
                            {
                                categoryComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate all required fields
                if (string.IsNullOrWhiteSpace(modelNumberTextBox.Text))
                {
                    MessageBox.Show("Please enter a model number.", "Validation Error");
                    modelNumberTextBox.Focus();
                    DialogResult = DialogResult.None;  // Prevent dialog from closing
                    return;
                }

                if (string.IsNullOrWhiteSpace(descriptionTextBox.Text))
                {
                    MessageBox.Show("Please enter a description.", "Validation Error");
                    descriptionTextBox.Focus();
                    DialogResult = DialogResult.None;  // Prevent dialog from closing
                    return;
                }

                if (string.IsNullOrWhiteSpace(supplierComboBox.Text))
                {
                    MessageBox.Show("Please select or enter a supplier.", "Validation Error");
                    supplierComboBox.Focus();
                    DialogResult = DialogResult.None;  // Prevent dialog from closing
                    return;
                }

                if (categoryComboBox.SelectedItem == null)
                {
                    var result = MessageBox.Show(
                        "A category must be selected before saving. Would you like to add a new category now?",
                        "Category Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        AddCategoryButton_Click(sender, e);
                    }
                    
                    categoryComboBox.Focus();
                    DialogResult = DialogResult.None;  // Prevent dialog from closing
                    return;
                }

                if (quantityNumericUpDown.Value <= 0)
                {
                    MessageBox.Show("Please enter a valid default order quantity.", "Validation Error");
                    quantityNumericUpDown.Focus();
                    DialogResult = DialogResult.None;  // Prevent dialog from closing
                    return;
                }

                if (string.IsNullOrWhiteSpace(selectedImagePath))
                {
                    MessageBox.Show("Please select an image for the item.", "Validation Error");
                    browseButton.Focus();
                    DialogResult = DialogResult.None;  // Prevent dialog from closing
                    return;
                }

                // Add the supplier if it's a new one
                SupplierManager.AddSupplier(supplierComboBox.Text);

                // Get the actual category from the wrapper if needed
                var selectedCategory = categoryComboBox.SelectedItem is CategoryWrapper wrapper ? 
                    wrapper.Category : categoryComboBox.SelectedItem as Category;

                // Create or update item
                Item = new Item
                {
                    ModelNumber = modelNumberTextBox.Text,
                    Description = descriptionTextBox.Text,
                    Supplier = supplierComboBox.Text,
                    DefaultOrderQuantity = (int)quantityNumericUpDown.Value,
                    ImagePath = selectedImagePath,
                    Category = selectedCategory,
                    ProductUrl = urlTextBox.Text
                };

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogDebug($"Error saving item: {ex.Message}");
                DialogResult = DialogResult.None;  // Prevent dialog from closing on error
            }
        }

        private void PrintPreviewButton_Click(object sender, EventArgs e)
        {
            // Verify image path exists before proceeding
            if (string.IsNullOrEmpty(selectedImagePath) || !File.Exists(selectedImagePath))
            {
                LogDebug($"Warning: Image path is invalid or doesn't exist: {selectedImagePath}");
                MessageBox.Show(
                    "The image path for this item is invalid or the file doesn't exist. The preview may not display images correctly.", 
                    "Image Path Warning", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Warning);
            }
            
            // Create a temporary item with form values for preview
            Item previewItem = new Item
            {
                ModelNumber = modelNumberTextBox.Text,
                Description = descriptionTextBox.Text,
                Supplier = supplierComboBox.SelectedItem?.ToString(),
                CategoryPath = categoryComboBox.SelectedItem is CategoryWrapper wrapper ? wrapper.Category.GetFullPath() : null,
                DefaultOrderQuantity = (int)quantityNumericUpDown.Value,
                ProductUrl = urlTextBox.Text,
                ImagePath = selectedImagePath
            };
            
            // Get default template from settings
            var settings = SettingsManager.GetSettings();
            string templateName = settings.DefaultLabelTemplate;
            
            // Show a label preview dialog with the current item
            ShowLabelPreview(previewItem, templateName);
        }
        
        private void ShowLabelPreview(Item item, string templateName)
        {
            // Create a label builder dialog with the item
            LabelBuilderDialog labelBuilderDialog = null;
            
            try
            {
                // Skip preview if item is null
                if (item == null)
                {
                    LogDebug("Cannot show label preview: Item is null");
                    MessageBox.Show("Cannot create a preview without item data.", "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Make sure there's at least a model number
                if (string.IsNullOrWhiteSpace(item.ModelNumber))
                {
                    LogDebug("Cannot show label preview: Item has no model number");
                    MessageBox.Show("Please enter a model number before previewing the label.", "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                LogDebug($"Creating label preview for item: {item.ModelNumber}");
                labelBuilderDialog = new LabelBuilderDialog(item);

                // We're no longer using reflection to invoke PrintButton_Click because it doesn't
                // properly use the selected printer. Instead, we'll create our own print preview
                // that uses the default printer from settings.
                
                // Create a PrintDocument
                var printDocument = new PrintDocument();
                printDocument.DocumentName = $"Label - {item.ModelNumber}";
                
                // Get settings
                var settings = SettingsManager.GetSettings();
                
                // Set the default printer if configured
                if (!string.IsNullOrEmpty(settings.DefaultLabelPrinter))
                {
                    try
                    {
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
                            LogDebug($"Using default label printer from settings: {settings.DefaultLabelPrinter}");
                            printDocument.PrinterSettings.PrinterName = settings.DefaultLabelPrinter;
                        }
                        else
                        {
                            LogDebug($"Configured printer '{settings.DefaultLabelPrinter}' not found");
                        }
                    }
                    catch (Exception printerEx)
                    {
                        LogDebug($"Error setting printer: {printerEx.Message}");
                    }
                }
                
                // Create PrintPreviewDialog
                using (var printPreviewDialog = new PrintPreviewDialog())
                {
                    // Set up print preview dialog
                    printPreviewDialog.Document = printDocument;
                    printPreviewDialog.StartPosition = FormStartPosition.CenterScreen;
                    printPreviewDialog.Width = 800;
                    printPreviewDialog.Height = 600;
                    
                    // Set PrintPage event handler
                    printDocument.PrintPage += (sender, e) =>
                    {
                        // Call the label builder dialog's method to render the label
                        // We'll use reflection to get the required methods and data
                        try
                        {
                            // Get the label elements from the dialog
                            var elementsField = labelBuilderDialog.GetType().GetField("labelElements", 
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            
                            if (elementsField != null)
                            {
                                var elements = elementsField.GetValue(labelBuilderDialog) as IEnumerable<object>;
                                if (elements != null)
                                {
                                    LogDebug($"Got {elements.Count()} label elements for rendering");
                                    
                                    // Call our rendering method
                                    RenderLabelToGraphics(e.Graphics, item, elements);
                                }
                                else
                                {
                                    LogDebug("Failed to get label elements (null)");
                                }
                            }
                            else
                            {
                                LogDebug("Failed to get labelElements field via reflection");
                            }
                        }
                        catch (Exception renderEx)
                        {
                            LogDebug($"Error rendering label: {renderEx.Message}");
                            if (renderEx.InnerException != null)
                            {
                                LogDebug($"Inner exception: {renderEx.InnerException.Message}");
                            }
                        }
                    };
                    
                    // Show the print preview dialog
                    LogDebug("Showing print preview dialog");
                    printPreviewDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error during label preview: {ex.Message}");
                
                // Get the inner exception if it's a reflection exception
                Exception realException = ex;
                if (ex is System.Reflection.TargetInvocationException && ex.InnerException != null)
                {
                    realException = ex.InnerException;
                    LogDebug($"Inner exception: {realException.Message}");
                }
                
                MessageBox.Show($"Error creating label preview: {realException.Message}", "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Dispose the dialog if it was created
                labelBuilderDialog?.Dispose();
            }
        }
        
        private void RenderLabelToGraphics(Graphics g, Item item, IEnumerable<object> elements)
        {
            LogDebug("Rendering label to graphics");
            
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            foreach (var element in elements)
            {
                try
                {
                    // Get the element's bounds
                    var boundsProperty = element.GetType().GetProperty("Bounds");
                    if (boundsProperty == null) continue;
                    
                    Rectangle bounds = (Rectangle)boundsProperty.GetValue(element);
                    
                    // Determine element type and render accordingly
                    string typeName = element.GetType().Name;
                    LogDebug($"Rendering element of type {typeName}");
                    
                    if (typeName == "TextElement")
                    {
                        // Get text content
                        var getTextMethod = element.GetType().GetMethod("GetText");
                        var getFontFamilyMethod = element.GetType().GetMethod("GetFontFamily");
                        var getFontSizeMethod = element.GetType().GetMethod("GetFontSize");
                        
                        if (getTextMethod != null && getFontFamilyMethod != null && getFontSizeMethod != null)
                        {
                            string text = (string)getTextMethod.Invoke(element, null);
                            string fontFamily = (string)getFontFamilyMethod.Invoke(element, null);
                            int fontSize = (int)getFontSizeMethod.Invoke(element, null);
                            
                            // Replace placeholders
                            text = ReplaceItemPlaceholders(text, item);
                            
                            // Create font and draw text
                            using (var font = new Font(fontFamily, fontSize))
                            {
                                g.DrawString(text, font, Brushes.Black, bounds);
                            }
                        }
                    }
                    else if (typeName == "QRElement")
                    {
                        // Get QR content and generate a proper QR code instead of just a placeholder
                        var getTemplateContentMethod = element.GetType().GetMethod("GetTemplateContent");
                        var getTemplateKeyMethod = element.GetType().GetMethod("GetTemplateKey");
                        var getOriginalBitmapMethod = element.GetType().GetMethod("GetOriginalBitmap");
                        
                        try
                        {
                            // First try to use the original high-resolution bitmap if available
                            if (getOriginalBitmapMethod != null)
                            {
                                var originalBitmap = getOriginalBitmapMethod.Invoke(element, null) as Bitmap;
                                if (originalBitmap != null)
                                {
                                    // Use high quality settings
                                    var oldInterpolationMode = g.InterpolationMode;
                                    var oldPixelOffsetMode = g.PixelOffsetMode;
                                    var oldCompositingQuality = g.CompositingQuality;
                                    var oldSmoothingMode = g.SmoothingMode;
                                    
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                    
                                    // Draw the bitmap using high quality settings
                                    g.DrawImage(originalBitmap, bounds);
                                    
                                    // Restore original graphics settings
                                    g.InterpolationMode = oldInterpolationMode;
                                    g.PixelOffsetMode = oldPixelOffsetMode;
                                    g.CompositingQuality = oldCompositingQuality;
                                    g.SmoothingMode = oldSmoothingMode;
                                    
                                    LogDebug("Drew QR code from original bitmap");
                                    continue; // Skip the rest of this iteration
                                }
                            }
                            
                            // If no original bitmap, generate a new QR code
                            string qrContent = null;
                            
                            if (getTemplateContentMethod != null)
                            {
                                qrContent = getTemplateContentMethod.Invoke(element, null) as string;
                            }
                            
                            // If no template content, try to create a default QR code content
                            if (string.IsNullOrEmpty(qrContent))
                            {
                                qrContent = $"Model: {item?.ModelNumber ?? ""} | Description: {item?.Description ?? ""}";
                                LogDebug("Using default QR content");
                            }
                            else
                            {
                                // Replace placeholders in the content
                                qrContent = ReplaceItemPlaceholders(qrContent, item);
                            }
                            
                            // Generate the QR code
                            var writer = new ZXing.Windows.Compatibility.BarcodeWriter
                            {
                                Format = ZXing.BarcodeFormat.QR_CODE,
                                Options = new ZXing.QrCode.QrCodeEncodingOptions
                                {
                                    Width = Math.Max(600, bounds.Width * 4),  // High resolution
                                    Height = Math.Max(600, bounds.Height * 4), // High resolution
                                    Margin = 1,
                                    ErrorCorrection = ZXing.QrCode.Internal.ErrorCorrectionLevel.M
                                },
                                Renderer = new ZXing.Windows.Compatibility.BitmapRenderer
                                {
                                    Background = Color.White,
                                    Foreground = Color.Black
                                }
                            };

                            // Generate QR code
                            using (var qrBitmap = writer.Write(qrContent))
                            {
                                // Use high quality settings for drawing
                                var oldInterpolationMode = g.InterpolationMode;
                                var oldPixelOffsetMode = g.PixelOffsetMode;
                                var oldCompositingQuality = g.CompositingQuality;
                                var oldSmoothingMode = g.SmoothingMode;
                                
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                
                                // Draw the QR code
                                g.DrawImage(qrBitmap, bounds);
                                
                                // Restore original graphics settings
                                g.InterpolationMode = oldInterpolationMode;
                                g.PixelOffsetMode = oldPixelOffsetMode;
                                g.CompositingQuality = oldCompositingQuality;
                                g.SmoothingMode = oldSmoothingMode;
                                
                                LogDebug("Generated and drew new QR code");
                            }
                        }
                        catch (Exception qrEx)
                        {
                            LogDebug($"Error generating QR code: {qrEx.Message}");
                            
                            // Fallback to placeholder as before
                            g.FillRectangle(Brushes.White, bounds);
                            g.DrawRectangle(Pens.Black, bounds);
                            
                            // Draw a QR code placeholder
                            int size = Math.Min(bounds.Width, bounds.Height);
                            int x = bounds.X + (bounds.Width - size) / 2;
                            int y = bounds.Y + (bounds.Height - size) / 2;
                            g.DrawRectangle(Pens.Black, x, y, size, size);
                            
                            // Draw a pattern inside to represent QR code
                            int cellSize = size / 10;
                            for (int i = 0; i < 10; i++)
                            {
                                for (int j = 0; j < 10; j++)
                                {
                                    if ((i + j) % 2 == 0)
                                    {
                                        g.FillRectangle(Brushes.Black, x + i * cellSize, y + j * cellSize, cellSize, cellSize);
                                    }
                                }
                            }
                        }
                    }
                    else if (typeName == "ImageElement")
                    {
                        // Get image path
                        var getImagePathMethod = element.GetType().GetMethod("GetImagePath");
                        if (getImagePathMethod != null)
                        {
                            string imagePath = (string)getImagePathMethod.Invoke(element, null);
                            
                            // If there's no image path or if it's a placeholder, use the item's image path
                            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                            {
                                if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                                {
                                    imagePath = item.ImagePath;
                                }
                            }
                            
                            // Draw the image if the path exists
                            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                            {
                                try
                                {
                                    using (var image = Image.FromFile(imagePath))
                                    {
                                        g.DrawImage(image, bounds);
                                    }
                                }
                                catch (Exception imgEx)
                                {
                                    LogDebug($"Error loading image {imagePath}: {imgEx.Message}");
                                    
                                    // Draw a placeholder for the image
                                    g.FillRectangle(Brushes.LightGray, bounds);
                                    g.DrawRectangle(Pens.Gray, bounds);
                                    
                                    // Draw X across the rectangle
                                    g.DrawLine(Pens.Gray, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                                    g.DrawLine(Pens.Gray, bounds.Right, bounds.Top, bounds.Left, bounds.Bottom);
                                    
                                    // Draw text
                                    using (var font = new Font("Arial", 8))
                                    {
                                        string errorText = "Image Error";
                                        SizeF textSize = g.MeasureString(errorText, font);
                                        float x = bounds.Left + (bounds.Width - textSize.Width) / 2;
                                        float y = bounds.Top + (bounds.Height - textSize.Height) / 2;
                                        g.DrawString(errorText, font, Brushes.Red, x, y);
                                    }
                                }
                            }
                            else
                            {
                                // Draw a placeholder for the image
                                g.FillRectangle(Brushes.LightGray, bounds);
                                g.DrawRectangle(Pens.Gray, bounds);
                                
                                // Draw image icon
                                int iconSize = Math.Min(bounds.Width, bounds.Height) / 2;
                                int x = bounds.X + (bounds.Width - iconSize) / 2;
                                int y = bounds.Y + (bounds.Height - iconSize) / 2;
                                
                                // Draw a simple image icon
                                g.FillRectangle(Brushes.White, x, y, iconSize, iconSize);
                                g.DrawRectangle(Pens.Black, x, y, iconSize, iconSize);
                                g.FillEllipse(Brushes.Yellow, x + iconSize/4, y + iconSize/4, iconSize/2, iconSize/2);
                            }
                        }
                    }
                }
                catch (Exception elemEx)
                {
                    LogDebug($"Error rendering element: {elemEx.Message}");
                }
            }
            
            LogDebug("Finished rendering label");
        }
        
        private string ReplaceItemPlaceholders(string text, Item item)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            return text.Replace("{ModelNumber}", item.ModelNumber ?? "")
                     .Replace("{Description}", item.Description ?? "")
                     .Replace("{Supplier}", item.Supplier ?? "")
                     .Replace("{Category}", item.Category?.GetFullPath() ?? "Uncategorized")
                     .Replace("{DefaultOrderQuantity}", item.DefaultOrderQuantity.ToString())
                     .Replace("{ProductURL}", item.ProductUrl ?? "")
                     .Replace("{ImagePath}", item.ImagePath ?? "");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (imagePreview.Image != null)
            {
                imagePreview.Image.Dispose();
            }
        }
    }

    public class DebugTextWriter : TextWriter
    {
        private Action<string> _log;

        public DebugTextWriter(Action<string> log)
        {
            _log = log;
        }

        public override void Write(char value)
        {
            _log(value.ToString());
        }

        public override void Write(string value)
        {
            _log(value);
        }

        public override void WriteLine(string value)
        {
            _log(value);
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
} 