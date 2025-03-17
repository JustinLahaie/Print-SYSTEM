using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PrintSystem.Models;
using PrintSystem.Managers;
using PrintSystem.Forms;
using CategoryManager = PrintSystem.Managers.CategoryManager;

namespace PrintSystem.Dialogs
{
    public enum NodeType
    {
        Supplier,
        Category
    }

    public class NodeData
    {
        public NodeType Type { get; set; }
        public string Name { get; set; }
        public Category Category { get; set; }
        public Supplier Supplier { get; set; }
    }

    public class CategorySettingsDialog : Form
    {
        private TreeView categoryTreeView;
        private Button addCategoryButton;
        private Button editButton;
        private Button deleteButton;
        private PictureBox imagePreview;
        private Button browseImageButton;
        private ComboBox supplierComboBox;
        private TextBox nameTextBox;
        private Label nameLabel;
        private GroupBox imageGroup;
        private GroupBox detailsGroup;

        public CategorySettingsDialog()
        {
            InitializeComponents();
            LoadSuppliers();
            LoadCategories();
        }

        private void InitializeComponents()
        {
            this.Text = "Category Settings";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(800, 500);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterParent;

            // Create main layout panel
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));

            // Left panel with TreeView
            Panel leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            categoryTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false
            };
            categoryTreeView.AfterSelect += CategoryTreeView_AfterSelect;

            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 35
            };

            addCategoryButton = new Button
            {
                Text = "Add Category",
                Width = 90,
                Location = new Point(0, 5)
            };
            addCategoryButton.Click += AddCategoryButton_Click;

            deleteButton = new Button
            {
                Text = "Delete",
                Width = 90,
                Location = new Point(95, 5),
                Enabled = false
            };
            deleteButton.Click += DeleteButton_Click;

            buttonPanel.Controls.AddRange(new Control[] { addCategoryButton, deleteButton });
            leftPanel.Controls.AddRange(new Control[] { categoryTreeView, buttonPanel });

            // Right panel with details
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Create a TableLayoutPanel for the right side to better organize controls
            TableLayoutPanel rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(5)
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));  // Supplier row
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F)); // Details row
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Image row

            // Supplier selection
            Panel supplierPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 35,
                Padding = new Padding(5)
            };

            var supplierLabel = new Label 
            { 
                Text = "Supplier:", 
                AutoSize = true,
                Location = new Point(0, 8)
            };

            supplierComboBox = new ComboBox
            {
                Width = 150,
                Location = new Point(supplierLabel.Width + 10, 5),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left
            };

            supplierPanel.Controls.AddRange(new Control[] { supplierLabel, supplierComboBox });
            rightLayout.Controls.Add(supplierPanel, 0, 0);

            // Details group
            detailsGroup = new GroupBox
            {
                Text = "Details",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            TableLayoutPanel detailsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 3,
                Padding = new Padding(5)
            };

            nameLabel = new Label { Text = "Name:", AutoSize = true };
            nameTextBox = new TextBox 
            { 
                Width = 250,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            editButton = new Button
            {
                Text = "Apply",
                Width = 80,
                Height = 25
            };
            editButton.Click += EditButton_Click;

            detailsLayout.Controls.Add(nameLabel, 0, 0);
            detailsLayout.Controls.Add(nameTextBox, 1, 0);
            detailsLayout.Controls.Add(editButton, 2, 0);
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // Label column
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // TextBox column
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));    // Button column

            detailsGroup.Controls.Add(detailsLayout);
            rightLayout.Controls.Add(detailsGroup, 0, 1);

            // Image group
            imageGroup = new GroupBox
            {
                Text = "Image",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            TableLayoutPanel imageLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(5)
            };
            imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Image space
            imageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));  // Button row

            // Center the PictureBox in a panel
            Panel picturePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            imagePreview = new PictureBox
            {
                Size = new Size(300, 300),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.None
            };

            // Center the PictureBox in its panel
            imagePreview.Location = new Point(
                (picturePanel.ClientSize.Width - imagePreview.Width) / 2,
                (picturePanel.ClientSize.Height - imagePreview.Height) / 2
            );

            picturePanel.Controls.Add(imagePreview);
            imageLayout.Controls.Add(picturePanel, 0, 0);

            // Button panel for the Change Image button
            Panel imageButtonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 35
            };

            browseImageButton = new Button
            {
                Text = "Change Image",
                Width = 120,
                Height = 25,
                Location = new Point(0, 0),
                Enabled = false
            };
            browseImageButton.Click += BrowseImageButton_Click;

            imageButtonPanel.Controls.Add(browseImageButton);
            imageLayout.Controls.Add(imageButtonPanel, 0, 1);

            imageGroup.Controls.Add(imageLayout);
            rightLayout.Controls.Add(imageGroup, 0, 2);

            rightPanel.Controls.Add(rightLayout);

            // Add panels to main layout
            mainLayout.Controls.Add(leftPanel, 0, 0);
            mainLayout.Controls.Add(rightPanel, 1, 0);

            // Bottom panel with Save/Cancel buttons
            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40
            };

            var saveButton = new Button
            {
                Text = "Save Changes",
                DialogResult = DialogResult.OK,
                Width = 120,
                Height = 30,
                Location = new Point(bottomPanel.Width - 260, 5),
                Anchor = AnchorStyles.Right
            };
            
            // Add click handler to ensure categories are saved before dialog closes
            saveButton.Click += (s, e) => {
                // Explicitly save categories to disk
                CategoryManager.SaveCategories();
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 120,
                Height = 30,
                Location = new Point(bottomPanel.Width - 130, 5),
                Anchor = AnchorStyles.Right
            };

            bottomPanel.Controls.AddRange(new Control[] { saveButton, cancelButton });
            mainLayout.Controls.Add(bottomPanel, 0, 1);
            mainLayout.SetColumnSpan(bottomPanel, 2);

            // Add main layout to form
            this.Controls.Add(mainLayout);

            // Set AcceptButton and CancelButton
            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;

            // Now set up the event handler after all initialization is done
            supplierComboBox.SelectedIndexChanged += (s, e) => LoadCategories();

            // Handle resize to keep PictureBox centered
            picturePanel.Resize += (s, e) =>
            {
                if (imagePreview != null)
                {
                    imagePreview.Location = new Point(
                        (picturePanel.ClientSize.Width - imagePreview.Width) / 2,
                        (picturePanel.ClientSize.Height - imagePreview.Height) / 2
                    );
                }
            };
        }

        private void LoadSuppliers()
        {
            supplierComboBox.Items.Clear();
            var suppliers = SupplierManager.GetSuppliers();
            
            // Add all suppliers to the combo box
            foreach (var supplier in suppliers)
            {
                supplierComboBox.Items.Add(supplier);
            }
            supplierComboBox.DisplayMember = "Name";

            // Select the first supplier if available
            if (supplierComboBox.Items.Count > 0)
            {
                supplierComboBox.SelectedIndex = 0;  // This will trigger LoadCategories once
            }
        }

        private void LoadCategories()
        {
            categoryTreeView.Nodes.Clear();

            // Add all suppliers to the tree view
            var suppliers = SupplierManager.GetSuppliers();
            foreach (var supplier in suppliers)
            {
                // Create a root node for each supplier
                var supplierNodeData = new NodeData
                {
                    Type = NodeType.Supplier,
                    Name = supplier.Name,
                    Supplier = supplier
                };
                var supplierNode = new TreeNode(supplier.Name) { Tag = supplierNodeData };
                categoryTreeView.Nodes.Add(supplierNode);

                // Get categories for this supplier
                var categories = CategoryManager.GetCategories(supplier.Name);
                
                if (categories == null || !categories.Any())
                {
                    // If no categories exist, ensure at least Uncategorized exists
                    CategoryManager.AddCategory("Uncategorized", supplier.Name);
                    categories = CategoryManager.GetCategories(supplier.Name);
                }

                // Add root-level categories
                var rootCategories = categories.Where(c => c.ParentCategory == null).ToList();
                
                foreach (var category in rootCategories)
                {
                    supplierNode.Nodes.Add(CreateCategoryNode(category));
                }
            }

            categoryTreeView.ExpandAll();
        }

        private TreeNode CreateCategoryNode(Category category)
        {
            var nodeData = new NodeData
            {
                Type = NodeType.Category,
                Category = category,
                Name = category.Name
            };

            var node = new TreeNode(category.Name)
            {
                Tag = nodeData
            };

            foreach (var subCategory in category.SubCategories)
            {
                node.Nodes.Add(CreateCategoryNode(subCategory));
            }

            return node;
        }

        private void CategoryTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var nodeData = e.Node?.Tag as NodeData;
            if (nodeData == null) return;

            deleteButton.Enabled = nodeData.Type == NodeType.Category;
            editButton.Enabled = true;
            browseImageButton.Enabled = true;

            if (nodeData.Type == NodeType.Supplier)
            {
                nameTextBox.Text = nodeData.Name;
                UpdateImagePreview(nodeData.Supplier?.ImagePath);
            }
            else if (nodeData.Type == NodeType.Category)
            {
                nameTextBox.Text = nodeData.Category.Name;
                UpdateImagePreview(nodeData.Category.ImagePath);
            }
        }

        private void UpdateImagePreview(string imagePath)
        {
            if (imagePreview.Image != null)
            {
                imagePreview.Image.Dispose();
                imagePreview.Image = null;
            }

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    using (var originalImage = Image.FromFile(imagePath))
                    {
                        // Create a copy of the image to avoid file locking
                        imagePreview.Image = new Bitmap(originalImage);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Image Load Error");
                }
            }
        }

        private void AddCategoryButton_Click(object sender, EventArgs e)
        {
            var selectedSupplier = supplierComboBox.SelectedItem as Supplier;
            if (selectedSupplier == null) return;

            using (var inputDialog = new CategoryInputDialog("Add Category", selectedSupplier.Name))
            {
                if (inputDialog.ShowDialog() == DialogResult.OK)
                {
                    CategoryManager.AddCategory(inputDialog.CategoryName, inputDialog.SelectedSupplier);
                    LoadCategories();
                }
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            var nodeData = categoryTreeView.SelectedNode?.Tag as NodeData;
            if (nodeData?.Category == null) return;

            if (MessageBox.Show(
                "Are you sure you want to delete this category? All items will be moved to Uncategorized.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                CategoryManager.RemoveCategory(nodeData.Category);
                LoadCategories();
            }
        }

        private void BrowseImageButton_Click(object sender, EventArgs e)
        {
            var nodeData = categoryTreeView.SelectedNode?.Tag as NodeData;
            if (nodeData == null) return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
                openFileDialog.Title = "Select Image";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (nodeData.Type == NodeType.Supplier)
                    {
                        // When changing a supplier's image from the category settings, treat it as a logo
                        SupplierManager.UpdateSupplierImage(nodeData.Name, openFileDialog.FileName, true);
                    }
                    else if (nodeData.Type == NodeType.Category)
                    {
                        CategoryManager.UpdateCategoryImage(nodeData.Category, openFileDialog.FileName);
                    }

                    CategoryTreeView_AfterSelect(null, new TreeViewEventArgs(categoryTreeView.SelectedNode));
                }
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            var nodeData = categoryTreeView.SelectedNode?.Tag as NodeData;
            if (nodeData == null || string.IsNullOrWhiteSpace(nameTextBox.Text)) return;

            if (nodeData.Type == NodeType.Supplier)
            {
                var supplier = nodeData.Supplier;
                if (supplier != null)
                {
                    supplier.Name = nameTextBox.Text;
                    SupplierManager.UpdateSupplier(supplier);
                }
            }
            else if (nodeData.Type == NodeType.Category)
            {
                nodeData.Category.Name = nameTextBox.Text;
                CategoryManager.SaveCategories();
            }

            LoadCategories();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (imagePreview.Image != null)
            {
                imagePreview.Image.Dispose();
            }
            
            // If the dialog is being closed with OK, ensure categories are saved
            if (DialogResult == DialogResult.OK)
            {
                CategoryManager.SaveCategories();
            }
        }
    }

    public class CategoryInputDialog : Form
    {
        public string CategoryName { get; private set; }
        public string SelectedSupplier { get; private set; }
        private TextBox nameTextBox;
        private ComboBox supplierComboBox;

        public CategoryInputDialog(string title, string currentSupplier = null)
        {
            this.Text = title;
            this.Size = new Size(400, 200);
            this.MinimumSize = new Size(350, 180);  // Set minimum size
            this.FormBorderStyle = FormBorderStyle.Sizable;  // Make the form resizable
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 4,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            // Make columns scale properly
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // First column auto-size
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Second column takes remaining space

            layout.Controls.Add(new Label { Text = "Category Name:", Anchor = AnchorStyles.Left }, 0, 0);
            nameTextBox = new TextBox { 
                Dock = DockStyle.Fill,  // Make textbox fill its cell
                Anchor = AnchorStyles.Left | AnchorStyles.Right  // Allow horizontal stretching
            };
            layout.Controls.Add(nameTextBox, 1, 0);

            layout.Controls.Add(new Label { Text = "Supplier:", Anchor = AnchorStyles.Left }, 0, 1);
            supplierComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,  // Make combo box fill its cell
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left | AnchorStyles.Right  // Allow horizontal stretching
            };

            // Load entire Supplier objects
            var suppliers = SupplierManager.GetSuppliers();
            foreach (var supplier in suppliers)
            {
                supplierComboBox.Items.Add(supplier);
            }
            supplierComboBox.DisplayMember = "Name";

            // Select current supplier if provided
            if (!string.IsNullOrEmpty(currentSupplier))
            {
                for (int i = 0; i < supplierComboBox.Items.Count; i++)
                {
                    var supplier = supplierComboBox.Items[i] as Supplier;
                    if (supplier != null && supplier.Name.Equals(currentSupplier, StringComparison.OrdinalIgnoreCase))
                    {
                        supplierComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            // If no supplier was selected and we have items, select the first one
            if (supplierComboBox.SelectedIndex == -1 && supplierComboBox.Items.Count > 0)
            {
                supplierComboBox.SelectedIndex = 0;
            }

            layout.Controls.Add(supplierComboBox, 1, 1);

            var buttonPanel = new Panel { Dock = DockStyle.Fill };
            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 80,
                Location = new Point(70, 10)
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 80,
                Location = new Point(160, 10)
            };

            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    MessageBox.Show("Please enter a category name.", "Validation Error");
                    DialogResult = DialogResult.None;
                    return;
                }

                if (supplierComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select a supplier.", "Validation Error");
                    DialogResult = DialogResult.None;
                    return;
                }

                CategoryName = nameTextBox.Text;
                
                var selectedSupplier = supplierComboBox.SelectedItem as Supplier;
                if (selectedSupplier != null)
                {
                    SelectedSupplier = selectedSupplier.Name;
                }
                else
                {
                    MessageBox.Show("Invalid supplier selection.", "Error");
                    DialogResult = DialogResult.None;
                    return;
                }
            };

            buttonPanel.Controls.AddRange(new Control[] { okButton, cancelButton });
            layout.Controls.Add(buttonPanel, 1, 2);

            this.Controls.Add(layout);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }
} 