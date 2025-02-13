using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;

public class MainForm : Form
{
    private TreeView treeView;
    private Button addButton;
    private Button addCategoryButton;
    private List<Item> items;
    private ImageList imageList;
    private ImageList smallImageList;
    private Dictionary<string, Image> cachedImages;

    public MainForm()
    {
        InitializeComponents();
        items = new List<Item>();
        cachedImages = new Dictionary<string, Image>();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        // Clean up cached images
        foreach (var image in cachedImages.Values)
        {
            image.Dispose();
        }
        cachedImages.Clear();
    }

    private void InitializeComponents()
    {
        this.Text = "Item Management System";
        this.Size = new Size(1000, 800);

        // Initialize ImageLists
        imageList = new ImageList();
        imageList.ImageSize = new Size(100, 100);
        imageList.ColorDepth = ColorDepth.Depth32Bit;

        smallImageList = new ImageList();
        smallImageList.ImageSize = new Size(16, 16);
        smallImageList.ColorDepth = ColorDepth.Depth32Bit;

        // Initialize TreeView
        treeView = new TreeView();
        treeView.Dock = DockStyle.Fill;
        treeView.ImageList = imageList;
        treeView.ItemHeight = 120;
        treeView.ShowPlusMinus = true;
        treeView.ShowLines = true;
        treeView.AfterSelect += TreeView_AfterSelect;
        treeView.BeforeExpand += TreeView_BeforeExpand;
        treeView.BeforeCollapse += TreeView_BeforeCollapse;

        // Initialize button panel
        Panel buttonPanel = new Panel();
        buttonPanel.Dock = DockStyle.Bottom;
        buttonPanel.Height = 40;
        buttonPanel.Padding = new Padding(5);

        // Initialize Add Item Button
        addButton = new Button();
        addButton.Text = "Add New Item";
        addButton.Dock = DockStyle.Right;
        addButton.Width = 120;
        addButton.Click += AddButton_Click;

        // Initialize Add Category Button
        addCategoryButton = new Button();
        addCategoryButton.Text = "Add Category";
        addCategoryButton.Dock = DockStyle.Right;
        addCategoryButton.Width = 120;
        addCategoryButton.Margin = new Padding(0, 0, 5, 0);
        addCategoryButton.Click += AddCategoryButton_Click;

        // Add controls to form
        buttonPanel.Controls.Add(addButton);
        buttonPanel.Controls.Add(addCategoryButton);
        this.Controls.Add(treeView);
        this.Controls.Add(buttonPanel);

        RefreshTreeView();
    }

    private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
    {
        if (e.Node.Tag is Category category)
        {
            category.IsExpanded = true;
        }
    }

    private void TreeView_BeforeCollapse(object sender, TreeViewCancelEventArgs e)
    {
        if (e.Node.Tag is Category category)
        {
            category.IsExpanded = false;
        }
    }

    private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
    {
        if (e.Node.Tag is Item item)
        {
            using (var editForm = new AddItemForm(item))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    var updatedItem = editForm.Item;
                    
                    // Update item in the main list
                    int index = items.FindIndex(i => i.Id == item.Id);
                    if (index != -1)
                    {
                        items[index] = updatedItem;
                    }

                    // Update item in its category
                    foreach (var category in CategoryManager.GetCategories())
                    {
                        int categoryItemIndex = category.Items.FindIndex(i => i.Id == item.Id);
                        if (categoryItemIndex != -1)
                        {
                            category.Items[categoryItemIndex] = updatedItem;
                            break;
                        }
                    }

                    RefreshTreeView();
                }
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
                // Add as a general category that will be used when adding items
                CategoryManager.AddCategory(categoryTextBox.Text);
                RefreshTreeView();
            }
        }
    }

    private void AddButton_Click(object sender, EventArgs e)
    {
        using (var addForm = new AddItemForm())
        {
            if (addForm.ShowDialog() == DialogResult.OK)
            {
                var newItem = addForm.Item;
                items.Add(newItem);
                CategoryManager.AddItemToCategory(newItem, newItem.Category);
                RefreshTreeView();
            }
        }
    }

    private void RefreshTreeView()
    {
        treeView.BeginUpdate();
        try
        {
            treeView.Nodes.Clear();
            imageList.Images.Clear();

            if (items == null || !items.Any())
            {
                return;
            }

            // Group items by supplier
            var itemsBySupplier = items.GroupBy(i => i.Supplier ?? "Unknown");
            
            foreach (var supplierGroup in itemsBySupplier)
            {
                var supplierNode = new TreeNode(supplierGroup.Key);
                
                // Group items by category within this supplier
                var itemsByCategory = supplierGroup
                    .GroupBy(i => i.Category?.Name ?? "Uncategorized")
                    .OrderBy(g => g.Key);
                
                foreach (var categoryGroup in itemsByCategory)
                {
                    var categoryNode = new TreeNode(categoryGroup.Key);
                    
                    // Add items to category node
                    foreach (var item in categoryGroup.OrderBy(i => i.ModelNumber))
                    {
                        var itemNode = CreateItemNode(item);
                        categoryNode.Nodes.Add(itemNode);
                    }
                    
                    supplierNode.Nodes.Add(categoryNode);
                }
                
                treeView.Nodes.Add(supplierNode);
            }

            // Expand all supplier nodes by default
            treeView.ExpandAll();
        }
        finally
        {
            treeView.EndUpdate();
        }
    }

    private TreeNode CreateItemNode(Item item)
    {
        var node = new TreeNode();
        node.Text = $"{item.ModelNumber}\n{item.Description}\nSupplier: {item.Supplier}\nQty: {item.DefaultOrderQuantity}";
        node.Tag = item;

        if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
        {
            try
            {
                if (!cachedImages.ContainsKey(item.ModelNumber))
                {
                    using (var image = Image.FromFile(item.ImagePath))
                    {
                        var thumbnail = image.GetThumbnailImage(100, 100, null, IntPtr.Zero);
                        cachedImages[item.ModelNumber] = thumbnail;
                    }
                }

                if (!imageList.Images.ContainsKey(item.ModelNumber))
                {
                    imageList.Images.Add(item.ModelNumber, cachedImages[item.ModelNumber]);
                }
                
                node.ImageKey = item.ModelNumber;
                node.SelectedImageKey = item.ModelNumber;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image for {item.ModelNumber}: {ex.Message}", "Image Load Error");
            }
        }

        return node;
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
} 