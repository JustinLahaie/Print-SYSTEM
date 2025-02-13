using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

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
            categoryComboBox.SelectedItem = itemToEdit.Category;
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

        // Add Debug Output
        layout.Controls.Add(new Label { Text = "Debug Output:" }, 0, 8);
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
        layout.Controls.Add(debugOutput, 1, 8);

        this.Controls.Add(layout);

        LoadCategories();  // Load available categories
    }

    private void LoadCategories()
    {
        categoryComboBox.Items.Clear();
        foreach (var category in CategoryManager.GetCategories())
        {
            if (!category.IsSupplierCategory)  // Only add non-supplier categories
            {
                categoryComboBox.Items.Add(category);
            }
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

            var product = await RichelieuScraper.ScrapeProductAsync(urlTextBox.Text);

            if (product != null)
            {
                modelNumberTextBox.Text = product.ModelNumber;
                descriptionTextBox.Text = product.Description;
                supplierComboBox.Text = "Richelieu";

                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    statusLabel.Text = "Downloading image...";
                    LogDebug("Starting image download...");
                    Application.DoEvents();

                    string imagesFolder = Path.Combine(Application.StartupPath, "Images");
                    selectedImagePath = await RichelieuScraper.DownloadImageAsync(product.ImageUrl, imagesFolder);
                    
                    if (File.Exists(selectedImagePath))
                    {
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
                        statusLabel.Text = "Failed to download image";
                        statusLabel.ForeColor = Color.Red;
                        LogDebug("Failed to download image");
                    }
                }
                else
                {
                    statusLabel.Text = "No image found";
                    statusLabel.ForeColor = Color.Red;
                    LogDebug("No image found");
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
        using (var inputForm = new Form())
        {
            inputForm.Text = "Add New Category";
            inputForm.Size = new Size(300, 150);
            inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            inputForm.StartPosition = FormStartPosition.CenterParent;
            inputForm.MaximizeBox = false;
            inputForm.MinimizeBox = false;

            var categoryLabel = new Label { Text = "Category Name:", Location = new Point(10, 20) };
            var categoryTextBox = new TextBox { Location = new Point(10, 40), Width = 260 };
            
            var addButton = new Button { Text = "Add", Location = new Point(100, 70), Width = 80, DialogResult = DialogResult.OK };
            var cancelBtn = new Button { Text = "Cancel", Location = new Point(190, 70), Width = 80, DialogResult = DialogResult.Cancel };

            inputForm.Controls.AddRange(new Control[] { categoryLabel, categoryTextBox, addButton, cancelBtn });
            inputForm.AcceptButton = addButton;
            inputForm.CancelButton = cancelBtn;

            if (inputForm.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(categoryTextBox.Text))
            {
                var newCategory = CategoryManager.AddCategory(categoryTextBox.Text);
                LoadCategories();
                categoryComboBox.SelectedItem = newCategory;
            }
        }
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(modelNumberTextBox.Text))
        {
            MessageBox.Show("Please enter a model number.", "Validation Error");
            return;
        }

        if (string.IsNullOrWhiteSpace(supplierComboBox.Text))
        {
            MessageBox.Show("Please select or enter a supplier.", "Validation Error");
            return;
        }

        if (categoryComboBox.SelectedItem == null)
        {
            MessageBox.Show("Please select a category.", "Validation Error");
            return;
        }

        // Add the supplier if it's a new one
        SupplierManager.AddSupplier(supplierComboBox.Text);

        // Create or update item
        Item = new Item
        {
            ModelNumber = modelNumberTextBox.Text,
            Description = descriptionTextBox.Text,
            Supplier = supplierComboBox.Text,
            DefaultOrderQuantity = (int)quantityNumericUpDown.Value,
            ImagePath = selectedImagePath,
            Category = categoryComboBox.SelectedItem as Category,
            ProductUrl = urlTextBox.Text
        };

        this.DialogResult = DialogResult.OK;
        this.Close();
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