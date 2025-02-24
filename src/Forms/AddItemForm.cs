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
            printPreviewButton.Click += (s, e) =>
            {
                // TODO: Implement print preview functionality
                MessageBox.Show("Print Preview functionality will be implemented soon!", "Coming Soon");
            };
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