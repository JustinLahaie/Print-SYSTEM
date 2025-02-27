using System;
using System.Windows.Forms;
using System.Drawing;
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
            // Check if the label templates are loaded
            var labelBuilderDialog = new LabelBuilderDialog(item);
            
            // Access the label templates from the dialog
            var templateMethod = labelBuilderDialog.GetType().GetMethod("LoadLabelTemplates", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            templateMethod?.Invoke(labelBuilderDialog, null);
            
            // Create a preview form
            using (var previewForm = new Form())
            {
                previewForm.Text = "Label Print Preview";
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
                
                // Get the template from the dialog
                var templatesField = labelBuilderDialog.GetType().GetField("labelTemplates", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (templatesField != null)
                {
                    var templates = templatesField.GetValue(labelBuilderDialog);
                    
                    // Check if the template exists using reflection
                    var containsKeyMethod = templates.GetType().GetMethod("ContainsKey");
                    var indexerProperty = templates.GetType().GetProperty("Item");
                    
                    if (containsKeyMethod != null && 
                        indexerProperty != null && 
                        (bool)containsKeyMethod.Invoke(templates, new object[] { templateName }))
                    {
                        var template = indexerProperty.GetValue(templates, new object[] { templateName });
                        
                        // Use reflection to get template properties
                        var widthProperty = template.GetType().GetProperty("Width");
                        var heightProperty = template.GetType().GetProperty("Height");
                        var elementsProperty = template.GetType().GetProperty("Elements");
                        
                        decimal width = Convert.ToDecimal(widthProperty?.GetValue(template) ?? 100m);
                        decimal height = Convert.ToDecimal(heightProperty?.GetValue(template) ?? 50m);
                        var elements = elementsProperty?.GetValue(template) as System.Collections.IEnumerable;

                        // Create a white panel to represent the paper
                        Panel paperPanel = new Panel
                        {
                            BackColor = Color.White,
                            Location = new Point(20, 20),
                            Size = new Size(
                                (int)(width * 4.0m), // 4 pixels per mm
                                (int)(height * 4.0m)
                            ),
                            Margin = new Padding(0)
                        };
                        
                        // Add paint handler for the paper panel
                        paperPanel.Paint += (s, pe) =>
                        {
                            // Call method to render label with the item
                            RenderLabelPreview(pe.Graphics, paperPanel, item, elements);
                        };
                        
                        // Add paper panel to preview panel
                        previewPanel.Controls.Add(paperPanel);
                    }
                    else
                    {
                        // Show a message if the template doesn't exist
                        Label noTemplateLabel = new Label
                        {
                            Text = $"Template '{templateName}' not found. Please check settings.",
                            Dock = DockStyle.Fill,
                            TextAlign = ContentAlignment.MiddleCenter,
                            Font = new Font(Font.FontFamily, 12)
                        };
                        previewPanel.Controls.Add(noTemplateLabel);
                    }
                }
                else
                {
                    // Show a message if we couldn't access templates
                    Label errorLabel = new Label
                    {
                        Text = "Could not access label templates. Please check settings.",
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font(Font.FontFamily, 12)
                    };
                    previewPanel.Controls.Add(errorLabel);
                }

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
                            // Will be implemented in future update
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
                previewForm.Controls.Add(previewPanel);
                previewForm.Controls.Add(printButton);
                previewForm.Controls.Add(closeButton);

                // Show the preview
                previewForm.ShowDialog();
            }
            
            // Clean up the dialog
            labelBuilderDialog.Dispose();
        }
        
        private void RenderLabelPreview(Graphics g, Panel paper, Item item, System.Collections.IEnumerable elements)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            if (elements == null) return;
            
            // Process each element in the template
            foreach (var element in elements)
            {
                try
                {
                    var typeProperty = element.GetType().GetProperty("Type");
                    var boundsProperty = element.GetType().GetProperty("Bounds");
                    var contentProperty = element.GetType().GetProperty("Content");
                    var fontFamilyProperty = element.GetType().GetProperty("FontFamily");
                    var fontSizeProperty = element.GetType().GetProperty("FontSize");
                    var qrTemplateKeyProperty = element.GetType().GetProperty("QRTemplateKey");
                    
                    string type = (string)typeProperty?.GetValue(element);
                    Rectangle bounds = (Rectangle)boundsProperty?.GetValue(element);
                    string content = (string)contentProperty?.GetValue(element);
                    
                    // Convert from template space to preview space
                    // The original bounds from the template include the MARGIN (40px) offset
                    // We need to subtract this MARGIN to correctly position elements in the preview
                    const int DESIGN_MARGIN = 40; // Same as MARGIN in LabelBuilderDialog
                    Rectangle adjustedBounds = new Rectangle(
                        bounds.X - DESIGN_MARGIN,
                        bounds.Y - DESIGN_MARGIN,
                        bounds.Width,
                        bounds.Height
                    );
                    
                    // Ensure bounds remain within the paper area
                    if (adjustedBounds.X < 0) adjustedBounds.X = 0;
                    if (adjustedBounds.Y < 0) adjustedBounds.Y = 0;
                    if (adjustedBounds.Right > paper.Width) adjustedBounds.Width = paper.Width - adjustedBounds.X;
                    if (adjustedBounds.Bottom > paper.Height) adjustedBounds.Height = paper.Height - adjustedBounds.Y;
                    
                    switch (type)
                    {
                        case "Text":
                            // Replace placeholders with actual values
                            string text = ReplaceItemPlaceholders(content, item);
                            
                            string fontFamily = (string)fontFamilyProperty?.GetValue(element) ?? "Arial";
                            float fontSize = Convert.ToSingle(fontSizeProperty?.GetValue(element) ?? 10f);
                            
                            using (var font = new Font(fontFamily, fontSize))
                            {
                                var format = new StringFormat
                                {
                                    Alignment = StringAlignment.Center,
                                    LineAlignment = StringAlignment.Center
                                };
                                g.DrawString(text, font, Brushes.Black, adjustedBounds, format);
                            }
                            break;
                            
                        case "QR":
                            // Get QR content - try to get from QRTemplateKey first
                            string qrTemplateKey = (string)qrTemplateKeyProperty?.GetValue(element);
                            string qrContent;
                            
                            if (!string.IsNullOrEmpty(qrTemplateKey))
                            {
                                // This is a reference to a stored QR template
                                LogDebug($"Using QR template key: {qrTemplateKey}");
                                
                                // Get the QR templates file path
                                string qrTemplatesFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qr_templates.json");
                                Dictionary<string, string> qrTemplates = new Dictionary<string, string>();
                                
                                // Try to load QR templates
                                if (File.Exists(qrTemplatesFile))
                                {
                                    try
                                    {
                                        string json = File.ReadAllText(qrTemplatesFile);
                                        qrTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                                    }
                                    catch (Exception ex)
                                    {
                                        LogDebug($"Error loading QR templates: {ex.Message}");
                                    }
                                }
                                
                                // Get the template content
                                if (qrTemplates.TryGetValue(qrTemplateKey, out string templateContent))
                                {
                                    qrContent = templateContent;
                                }
                                else
                                {
                                    // Handle built-in templates
                                    qrContent = qrTemplateKey switch
                                    {
                                        "Basic Info" => "Model: {ModelNumber} | Description: {Description}",
                                        "Full Details" => "Model: {ModelNumber}"
                                            + " | Description: {Description}"
                                            + " | Supplier: {Supplier}"
                                            + " | Category: {Category}"
                                            + " | Order Qty: {DefaultOrderQuantity}"
                                            + " | URL: {ProductURL}",
                                        "URL Only" => "{ProductURL}",
                                        _ => content // Fall back to content field
                                    };
                                }
                            }
                            else
                            {
                                // Use the content field directly
                                qrContent = content;
                            }
                            
                            // Replace placeholders with actual values
                            qrContent = ReplaceItemPlaceholders(qrContent, item);
                            
                            // Generate QR code
                            try
                            {
                                var writer = new ZXing.BarcodeWriter<Bitmap>
                                {
                                    Format = ZXing.BarcodeFormat.QR_CODE,
                                    Options = new ZXing.QrCode.QrCodeEncodingOptions
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
                                    g.DrawImage(qrImage, adjustedBounds);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogDebug($"Error generating QR code: {ex.Message}");
                            }
                            break;
                            
                        case "Image":
                            // Use item's image if available, otherwise use the image path from the template
                            string imagePath = !string.IsNullOrEmpty(item.ImagePath) ? item.ImagePath : content;
                            
                            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                            {
                                try
                                {
                                    using (var image = Image.FromFile(imagePath))
                                    {
                                        g.DrawImage(image, adjustedBounds);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogDebug($"Error loading image: {ex.Message}");
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error rendering element: {ex.Message}");
                }
            }
        }
        
        private string ReplaceItemPlaceholders(string text, Item item)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            return text.Replace("{ModelNumber}", item.ModelNumber ?? "")
                     .Replace("{Description}", item.Description ?? "")
                     .Replace("{Supplier}", item.Supplier ?? "")
                     .Replace("{Category}", item.Category?.GetFullPath() ?? "Uncategorized")
                     .Replace("{DefaultOrderQuantity}", item.DefaultOrderQuantity.ToString())
                     .Replace("{ProductURL}", item.ProductUrl ?? "");
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