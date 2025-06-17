using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using PrintSystem.Models;
using PrintSystem.Managers;
using PrintSystem.Dialogs;
using CategoryManager = PrintSystem.Managers.CategoryManager;

namespace PrintSystem.Forms
{
    public class MainForm : Form
    {
        private TreeView treeView;
        private Button addButton;
        private Button addCategoryButton;
        private Button settingsButton;
        private ImageList imageList;
        private ImageList smallImageList;
        private Dictionary<string, Image> cachedImages;
        private readonly string supplierLogosPath = "SupplierLogos";
        private readonly string itemImagesPath = "ItemImages";
        private ContextMenuStrip treeViewContextMenu;

        public MainForm()
        {
            try
            {
                // Initialize the dictionary first
                cachedImages = new Dictionary<string, Image>();

                // Create necessary directories
                CreatePortableDirectories();

                // Clean up any stale cached images
                CleanupStaleImages();

                // Copy logo files if they don't exist in the target directory
                string marathonSource = "Marathon_Logo.png";
                string richelieuSource = "Richelieu_Logo.jpg";
                string marathonTarget = Path.Combine(supplierLogosPath, "Marathon_Logo.png");
                string richelieuTarget = Path.Combine(supplierLogosPath, "Richelieu_Logo.jpg");

                Console.WriteLine($"Checking Marathon logo - Source exists: {File.Exists(marathonSource)}, Target exists: {File.Exists(marathonTarget)}");
                Console.WriteLine($"Checking Richelieu logo - Source exists: {File.Exists(richelieuSource)}, Target exists: {File.Exists(richelieuTarget)}");

                if (File.Exists(marathonSource) && !File.Exists(marathonTarget))
                {
                    File.Copy(marathonSource, marathonTarget, true);
                    Console.WriteLine($"Copied Marathon logo to: {marathonTarget}");
                }
                else if (!File.Exists(marathonSource) && !File.Exists(marathonTarget))
                {
                    MessageBox.Show($"Marathon logo not found at: {marathonSource}", "Warning");
                }

                if (File.Exists(richelieuSource) && !File.Exists(richelieuTarget))
                {
                    File.Copy(richelieuSource, richelieuTarget, true);
                    Console.WriteLine($"Copied Richelieu logo to: {richelieuTarget}");
                }
                else if (!File.Exists(richelieuSource) && !File.Exists(richelieuTarget))
                {
                    MessageBox.Show($"Richelieu logo not found at: {richelieuSource}", "Warning");
                }

                InitializeComponents();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in constructor: {ex.Message}\nStack Trace:\n{ex.StackTrace}", "Error");
            }
        }

        private void CreatePortableDirectories()
        {
            // Create necessary directories if they don't exist
            Directory.CreateDirectory(supplierLogosPath);
            Directory.CreateDirectory(itemImagesPath);
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
            smallImageList.ImageSize = new Size(96, 48);  // Doubled the size for better visibility
            smallImageList.ColorDepth = ColorDepth.Depth32Bit;

            // Initialize TreeView
            treeView = new TreeView();
            treeView.Dock = DockStyle.Fill;
            treeView.ImageList = smallImageList;
            treeView.StateImageList = smallImageList;
            treeView.ItemHeight = 56;  // Increased to accommodate larger images
            treeView.Indent = 96;  // Match indent with new image width
            treeView.ShowPlusMinus = true;
            treeView.ShowLines = true;
            treeView.ShowRootLines = true;
            treeView.FullRowSelect = true;
            treeView.LabelEdit = false;
            treeView.MouseDoubleClick += TreeView_MouseDoubleClick;  // Add double-click handler
            treeView.BeforeExpand += TreeView_BeforeExpand;
            treeView.BeforeCollapse += TreeView_BeforeCollapse;

            // Initialize context menu
            treeViewContextMenu = new ContextMenuStrip();
            var deleteMenuItem = new ToolStripMenuItem("Delete", null, DeleteMenuItem_Click);
            deleteMenuItem.ShortcutKeys = Keys.Delete;
            treeViewContextMenu.Items.Add(deleteMenuItem);
            
            // Attach context menu to TreeView
            treeView.ContextMenuStrip = treeViewContextMenu;
            
            // Handle node click to show/hide context menu
            treeView.NodeMouseClick += (s, e) => {
                if (e.Button == MouseButtons.Right)
                {
                    treeView.SelectedNode = e.Node;
                    var node = e.Node;
                    if (node?.Tag is Item)
                    {
                        deleteMenuItem.Enabled = true;
                    }
                    else
                    {
                        deleteMenuItem.Enabled = false;
                    }
                }
            };

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

            // Initialize Settings Button
            settingsButton = new Button();
            settingsButton.Text = "⚙";
            settingsButton.Font = new Font(settingsButton.Font.FontFamily, 14);
            settingsButton.Dock = DockStyle.Left;
            settingsButton.Width = 40;
            settingsButton.Click += SettingsButton_Click;

            // Add controls to form
            buttonPanel.Controls.Add(addButton);
            buttonPanel.Controls.Add(addCategoryButton);
            buttonPanel.Controls.Add(settingsButton);
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

        private void TreeView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Get the node at the clicked position
            TreeNode node = treeView.GetNodeAt(e.X, e.Y);
            if (node?.Tag is Item item)
            {
                using (var editForm = new AddItemForm(item))
                {
                    if (editForm.ShowDialog() == DialogResult.OK)
                    {
                        var updatedItem = editForm.Item;
                        
                        // Update item in its category
                        if (item.Category != null)
                        {
                            var categories = CategoryManager.GetCategories(item.Supplier);
                            foreach (var category in categories)
                            {
                                var categoryItems = category.Items.ToList();
                                var itemIndex = categoryItems.FindIndex(i => i.Id == item.Id);
                                if (itemIndex != -1)
                                {
                                    // Since we can't modify the IReadOnlyList directly,
                                    // we need to use the category's AddItem/RemoveItem methods
                                    category.RemoveItem(item);
                                    category.AddItem(updatedItem);
                                    break;
                                }
                            }
                        }

                        RefreshTreeView();
                    }
                }
            }
        }

        private void AddCategoryButton_Click(object sender, EventArgs e)
        {
            using (var settingsDialog = new CategorySettingsDialog())
            {
                settingsDialog.Owner = this;  // Set this MainForm as the owner
                var result = settingsDialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    // Just refresh the tree view without reloading categories from disk
                    // This prevents losing in-memory changes that might not have been saved to disk yet
                    RefreshTreeView();
                }
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (var addItemForm = new AddItemForm())
                {
                    if (addItemForm.ShowDialog() == DialogResult.OK && addItemForm.Item != null)
                    {
                        ItemManager.AddItem(addItemForm.Item);
                        RefreshTreeView();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshTreeView()
        {
            treeView.BeginUpdate();
            try
            {
                // Store expanded state before clearing
                var expandedPaths = new HashSet<string>();
                StoreExpandedState(treeView.Nodes, expandedPaths);
                
                treeView.Nodes.Clear();
                
                // Dispose of existing images before clearing
                foreach (Image img in imageList.Images)
                {
                    img?.Dispose();
                }
                imageList.Images.Clear();
                
                foreach (Image img in smallImageList.Images)
                {
                    img?.Dispose();
                }
                smallImageList.Images.Clear();
                
                // Clear cached images
                foreach (var kvp in cachedImages)
                {
                    kvp.Value?.Dispose();
                }
                cachedImages.Clear();

                var items = ItemManager.GetItems();
                var suppliers = SupplierManager.GetSuppliers();

                if (suppliers == null)
                {
                    MessageBox.Show("No suppliers found", "Warning");
                    return;
                }
                
                foreach (var supplier in suppliers.Where(s => s != null))  // Filter out null suppliers
                {
                    try
                    {
                        string supplierName = supplier.Name;
                        if (string.IsNullOrEmpty(supplierName))
                        {
                            Console.WriteLine("Found supplier with null or empty name");
                            continue;
                        }

                        var supplierNode = new TreeNode("");  // Empty text, only show logo
                        
                        // Debug: Print the expected logo path
                        string logoPath = Path.Combine(supplierLogosPath, $"{supplierName}_Logo.{(supplierName.Equals("Marathon", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg")}");
                        Console.WriteLine($"Looking for logo at: {logoPath}");
                        
                        if (!File.Exists(logoPath))
                        {
                            Console.WriteLine($"Logo file not found at: {logoPath}");
                            // Try to copy from root directory if exists
                            string rootLogoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{supplierName}_Logo.{(supplierName.Equals("Marathon", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg")}");
                            if (File.Exists(rootLogoPath))
                            {
                                File.Copy(rootLogoPath, logoPath, true);
                                Console.WriteLine($"Copied logo from {rootLogoPath} to {logoPath}");
                            }
                        }

                        if (File.Exists(logoPath))
                        {
                            try
                            {
                                string imageKey = $"supplier_{supplierName}";
                                Console.WriteLine($"Setting up image with key: {imageKey}");

                                // Remove existing image if present
                                if (smallImageList.Images.ContainsKey(imageKey))
                                {
                                    smallImageList.Images.RemoveByKey(imageKey);
                                }
                                if (cachedImages.ContainsKey(imageKey))
                                {
                                    cachedImages[imageKey].Dispose();
                                    cachedImages.Remove(imageKey);
                                }

                                // Load and process the image
                                using (var fs = new FileStream(logoPath, FileMode.Open, FileAccess.Read))
                                using (var originalImage = Image.FromStream(fs))
                                using (var thumbnail = new Bitmap(96, 48))  // Match new ImageList size
                                {
                                    using (var g = Graphics.FromImage(thumbnail))
                                    {
                                        g.Clear(Color.Transparent);
                                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                        g.DrawImage(originalImage, 0, 0, 96, 48);  // Match new ImageList size
                                    }

                                    // Create a new bitmap that will be stored in both collections
                                    var newImage = new Bitmap(thumbnail);
                                    
                                    // Add to cached images
                                    cachedImages[imageKey] = newImage;
                                    
                                    // Add to ImageList
                                    smallImageList.Images.Add(imageKey, newImage);
                                    
                                    // Set the image key for the node
                                    supplierNode.ImageKey = imageKey;
                                    supplierNode.SelectedImageKey = imageKey;
                                    
                                    Console.WriteLine($"Successfully loaded and set logo for {supplierName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error loading logo for {supplierName}: {ex.Message}\n{ex.StackTrace}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"No logo found for {supplierName} at {logoPath}");
                        }

                        // Filter items by supplier name
                        var supplierItems = items?.Where(i => 
                            i?.Supplier != null && 
                            i.Supplier.Equals(supplierName, StringComparison.OrdinalIgnoreCase)
                        ).ToList() ?? new List<Item>();

                        var supplierGroup = supplierItems
                            .GroupBy(i => i.Supplier ?? "Unknown")
                            .FirstOrDefault();

                        var supplierCategories = CategoryManager.GetCategories(supplierName) ?? new List<Category>();
                        
                        foreach (var category in supplierCategories.Where(c => c?.ParentCategory == null))
                        {
                            if (category != null)
                            {
                                var categoryNode = CreateCategoryNodeWithItems(category, supplierGroup);
                                supplierNode.Nodes.Add(categoryNode);
                            }
                        }
                        
                        treeView.Nodes.Add(supplierNode);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing supplier: {ex.Message}\n{ex.StackTrace}");
                    }
                }

                foreach (TreeNode supplierNode in treeView.Nodes)
                {
                    try
                    {
                        RestoreNodeState(supplierNode);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error restoring node state: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RefreshTreeView: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                treeView.EndUpdate();
            }
        }

        private TreeNode CreateCategoryNodeWithItems(Category category, IGrouping<string, Item> supplierItems)
        {
            var node = new TreeNode(category.Name);
            node.Tag = category;
            
            // Handle category image if it exists
            if (!string.IsNullOrEmpty(category.ImagePath) && File.Exists(category.ImagePath))
            {
                try
                {
                    string imageKey = $"category_{category.Name}";
                    if (!cachedImages.ContainsKey(imageKey))
                    {
                        using (var originalImage = Image.FromFile(category.ImagePath))
                        {
                            // Calculate dimensions maintaining aspect ratio
                            double aspectRatio = (double)originalImage.Width / originalImage.Height;
                            int newWidth, newHeight;
                            
                            if (aspectRatio > 2) // If image is wider than 2:1
                            {
                                newWidth = 96;
                                newHeight = (int)(96 / aspectRatio);
                            }
                            else // If image is taller or square
                            {
                                newHeight = 48;
                                newWidth = (int)(48 * aspectRatio);
                            }

                            // Create thumbnail with calculated dimensions
                            var thumbnail = new Bitmap(96, 48);
                            using (var g = Graphics.FromImage(thumbnail))
                            {
                                g.Clear(Color.Transparent);
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                
                                // Center the image in the thumbnail
                                int x = (96 - newWidth) / 2;
                                int y = (48 - newHeight) / 2;
                                g.DrawImage(originalImage, x, y, newWidth, newHeight);
                            }
                            cachedImages[imageKey] = thumbnail;
                        }
                    }

                    if (!smallImageList.Images.ContainsKey(imageKey))
                    {
                        smallImageList.Images.Add(imageKey, cachedImages[imageKey]);
                    }
                    
                    node.ImageKey = imageKey;
                    node.SelectedImageKey = imageKey;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading category image for {category.Name}: {ex.Message}");
                }
            }
            
            // Add items that belong directly to this category
            if (supplierItems != null)
            {
                foreach (var item in supplierItems.Where(i => i.Category == category).OrderBy(i => i.ModelNumber))
                {
                    node.Nodes.Add(CreateItemNode(item));
                }
            }

            // Add all subcategories regardless of whether they have items
            foreach (var subcategory in category.SubCategories)
            {
                var subcategoryNode = CreateCategoryNodeWithItems(subcategory, supplierItems);
                node.Nodes.Add(subcategoryNode);
            }

            return node;
        }

        private void RestoreNodeState(TreeNode node)
        {
            if (node.Tag is Category category && category.IsExpanded)
            {
                node.Expand();
            }
            foreach (TreeNode childNode in node.Nodes)
            {
                RestoreNodeState(childNode);
            }
        }

        private void StoreExpandedState(TreeNodeCollection nodes, HashSet<string> expandedPaths)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsExpanded)
                {
                    expandedPaths.Add(GetNodePath(node));
                }
                StoreExpandedState(node.Nodes, expandedPaths);
            }
        }

        private string GetNodePath(TreeNode node)
        {
            var path = node.Text;
            var parent = node.Parent;
            while (parent != null)
            {
                path = parent.Text + "/" + path;
                parent = parent.Parent;
            }
            return path;
        }

        private TreeNode CreateItemNode(Item item)
        {
            var node = new TreeNode();
            node.Text = item.Description;  // Only show the description
            node.Tag = item;

            if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                try
                {
                    string imageKey = item.ModelNumber;
                    if (!cachedImages.ContainsKey(imageKey))
                    {
                        using (var originalImage = Image.FromFile(item.ImagePath))
                        {
                            // Calculate dimensions maintaining aspect ratio
                            double aspectRatio = (double)originalImage.Width / originalImage.Height;
                            int newWidth, newHeight;
                            
                            if (aspectRatio > 2) // If image is wider than 2:1
                            {
                                newWidth = 96;
                                newHeight = (int)(96 / aspectRatio);
                            }
                            else // If image is taller or square
                            {
                                newHeight = 48;
                                newWidth = (int)(48 * aspectRatio);
                            }

                            // Create thumbnail with calculated dimensions
                            var thumbnail = new Bitmap(96, 48);
                            using (var g = Graphics.FromImage(thumbnail))
                            {
                                g.Clear(Color.Transparent);
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                
                                // Center the image in the thumbnail
                                int x = (96 - newWidth) / 2;
                                int y = (48 - newHeight) / 2;
                                g.DrawImage(originalImage, x, y, newWidth, newHeight);
                            }
                            cachedImages[imageKey] = thumbnail;
                        }
                    }

                    if (!smallImageList.Images.ContainsKey(imageKey))
                    {
                        smallImageList.Images.Add(imageKey, cachedImages[imageKey]);
                    }
                    
                    node.ImageKey = imageKey;
                    node.SelectedImageKey = imageKey;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image for {item.ModelNumber}: {ex.Message}", "Image Load Error");
                }
            }

            return node;
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            using (var settingsDialog = new SettingsDialog())
            {
                if (settingsDialog.ShowDialog() == DialogResult.OK)
                {
                    // Refresh the view if needed based on new settings
                    RefreshTreeView();
                }
            }
        }

        private void DeleteMenuItem_Click(object sender, EventArgs e)
        {
            var selectedNode = treeView.SelectedNode;
            if (selectedNode?.Tag is Item item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the item '{item.ModelNumber}'?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        // Remove from category first
                        if (item.Category != null)
                        {
                            CategoryManager.RemoveItemFromCategory(item, item.Category);
                        }

                        // Remove the node from TreeView before cleaning up images
                        selectedNode.Remove();

                        // Now clean up images after the node is removed
                        if (imageList.Images.ContainsKey(item.ModelNumber))
                        {
                            imageList.Images.RemoveByKey(item.ModelNumber);
                        }
                        if (smallImageList.Images.ContainsKey(item.ModelNumber))
                        {
                            smallImageList.Images.RemoveByKey(item.ModelNumber);
                        }
                        if (cachedImages.ContainsKey(item.ModelNumber))
                        {
                            using (var image = cachedImages[item.ModelNumber])
                            {
                                cachedImages.Remove(item.ModelNumber);
                            }
                        }

                        // Delete the item's image file if it exists
                        if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                        {
                            try
                            {
                                File.Delete(item.ImagePath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Could not delete image file: {ex.Message}");
                            }
                        }

                        // Remove the item from the manager
                        ItemManager.RemoveItem(item);

                        MessageBox.Show("Item deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CleanupStaleImages()
        {
            try
            {
                // Clear existing image lists
                if (imageList != null)
                {
                    imageList.Images.Clear();
                }
                if (smallImageList != null)
                {
                    smallImageList.Images.Clear();
                }

                // Clean up cached images
                if (cachedImages != null)
                {
                    foreach (var image in cachedImages.Values)
                    {
                        try
                        {
                            image.Dispose();
                        }
                        catch { /* Ignore disposal errors */ }
                    }
                    cachedImages.Clear();
                }

                // Clean up stale item images
                if (Directory.Exists(itemImagesPath))
                {
                    var items = ItemManager.GetItems();
                    var validImagePaths = items.Select(i => i.ImagePath).Where(p => !string.IsNullOrEmpty(p)).ToHashSet();
                    
                    foreach (var file in Directory.GetFiles(itemImagesPath))
                    {
                        if (!validImagePaths.Contains(file))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch { /* Ignore deletion errors */ }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up stale images: {ex.Message}");
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Clean up any invalid categories before starting
            CategoryManager.CleanupInvalidCategories();
            
            // Perform a full cleanup of stale data
            CleanupStaleData();
            
            Application.Run(new MainForm());
        }

        private static void CleanupStaleData()
        {
            try
            {
                // Reload categories to ensure clean state
                CategoryManager.ReloadCategories();

                // Get all items and validate their relationships
                var items = ItemManager.GetItems().ToList();
                foreach (var item in items)
                {
                    if (item.Category != null)
                    {
                        // Validate category exists and matches supplier
                        var categories = CategoryManager.GetCategories(item.Supplier);
                        if (!categories.Contains(item.Category))
                        {
                            // Find or create Uncategorized category
                            var uncategorized = categories.FirstOrDefault(c => c.Name == "Uncategorized")
                                ?? CategoryManager.AddCategory("Uncategorized", item.Supplier);
                            
                            // Move item to Uncategorized
                            CategoryManager.RemoveItemFromCategory(item, item.Category);
                            CategoryManager.AddItemToCategory(item, uncategorized);
                        }
                    }
                }

                // Save changes
                CategoryManager.SaveCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during cleanup: {ex.Message}", "Cleanup Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
} 