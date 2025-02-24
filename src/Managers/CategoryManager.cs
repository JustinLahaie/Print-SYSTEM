using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using PrintSystem.Models;
using System.Windows.Forms;

namespace PrintSystem.Managers
{
    public static class CategoryManager
    {
        private static readonly string categoriesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "categories.json");
        private static readonly string categoryImagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CategoryImages");
        private static Dictionary<string, List<Category>> supplierCategories = new Dictionary<string, List<Category>>();
        private static readonly object lockObject = new object();
        private static bool isInitialized = false;

        static CategoryManager()
        {
            LoadCategories();
        }

        private static void LoadCategories()
        {
            lock (lockObject)
            {
                if (!isInitialized)
                {
                    if (!Directory.Exists(categoryImagesDir))
                    {
                        Directory.CreateDirectory(categoryImagesDir);
                    }

                    bool loadedFromFile = false;

                    if (File.Exists(categoriesFilePath))
                    {
                        try
                        {
                            string jsonData = File.ReadAllText(categoriesFilePath);
                            var loadedCategories = JsonSerializer.Deserialize<Dictionary<string, List<Category>>>(jsonData);
                            
                            // Normalize supplier names to match the case from SupplierManager
                            var suppliers = SupplierManager.GetSuppliers();
                            supplierCategories.Clear();
                            
                            foreach (var kvp in loadedCategories)
                            {
                                var supplier = suppliers.FirstOrDefault(s => s.Name.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
                                if (supplier != null)
                                {
                                    supplierCategories[supplier.Name] = kvp.Value;
                                    // Update supplier name in all categories to match case
                                    foreach (var category in kvp.Value)
                                    {
                                        category.Supplier = supplier.Name;
                                    }
                                }
                                else
                                {
                                    supplierCategories[kvp.Key] = kvp.Value;
                                }
                            }
                            
                            loadedFromFile = true;

                            // Restore parent-child relationships
                            foreach (var supplierCats in supplierCategories.Values)
                            {
                                foreach (var category in supplierCats)
                                {
                                    RestoreParentChildRelationships(category);

                                    // Validate missing image files
                                    if (!string.IsNullOrEmpty(category.ImagePath) && !File.Exists(category.ImagePath))
                                    {
                                        category.ImagePath = null;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error loading categories: {ex.Message}", "Error");
                            InitializeDefaultCategories();
                        }
                    }

                    // If we didn't load from file, or you'd like to ensure new suppliers get added:
                    if (!loadedFromFile)
                    {
                        // categories.json doesn't exist or had an error
                        InitializeDefaultCategories();
                    }
                    else
                    {
                        // Attempt to add any suppliers not in the dictionary
                        AddNewSuppliersIfMissing();
                    }

                    isInitialized = true;
                }
            }
        }

        /// <summary>
        /// Ensure that newly introduced suppliers still get their default categories.
        /// </summary>
        private static void AddNewSuppliersIfMissing()
        {
            var allSuppliers = SupplierManager.GetSuppliers();

            foreach (var supplier in allSuppliers)
            {
                if (!supplierCategories.ContainsKey(supplier.Name))
                {
                    // Supplier is missing in the dictionary, so set up default categories
                    supplierCategories[supplier.Name] = new List<Category>();
                    InitializeSupplierCategories(supplier.Name);
                }
            }

            SaveCategories();
        }

        /// <summary>
        /// Builds the default categories for the specific supplier.
        /// </summary>
        private static void InitializeSupplierCategories(string supplierName)
        {
            if (!supplierCategories.ContainsKey(supplierName))
            {
                supplierCategories[supplierName] = new List<Category>();
            }
            
            // Only add Uncategorized as a default category
            if (!supplierCategories[supplierName].Any(c => c.Name == "Uncategorized"))
            {
                AddCategory("Uncategorized", supplierName);
            }
        }

        private static void InitializeDefaultCategories()
        {
            supplierCategories.Clear();
            foreach (var supplier in SupplierManager.GetSuppliers())
            {
                if (!supplierCategories.ContainsKey(supplier.Name))
                {
                    supplierCategories[supplier.Name] = new List<Category>();
                }
                InitializeSupplierCategories(supplier.Name);
            }
            SaveCategories();
        }

        private static void RestoreParentChildRelationships(Category category)
        {
            foreach (var subCategory in category.SubCategories)
            {
                subCategory.ParentCategory = category;
                RestoreParentChildRelationships(subCategory);
            }
        }

        public static void SaveCategories()
        {
            try
            {
                string jsonData = JsonSerializer.Serialize(supplierCategories, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(categoriesFilePath, jsonData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving categories: {ex.Message}", "Error");
            }
        }

        public static Category AddCategory(string name, string supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier))
                throw new ArgumentException("Supplier cannot be null or empty", nameof(supplier));
            
            if (supplier.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("'Supplier' is not a valid supplier name", nameof(supplier));

            // Find the correct supplier key case-insensitively
            var existingKey = supplierCategories.Keys.FirstOrDefault(k => k.Equals(supplier, StringComparison.OrdinalIgnoreCase));
            if (existingKey != null)
            {
                supplier = existingKey; // Use the existing key to maintain case consistency
            }

            if (!supplierCategories.ContainsKey(supplier))
            {
                supplierCategories[supplier] = new List<Category>();
            }

            var category = new Category(name, supplier);
            supplierCategories[supplier].Add(category);
            SaveCategories();
            return category;
        }

        public static Category AddSubCategory(string name, Category parentCategory)
        {
            if (parentCategory == null)
                throw new ArgumentNullException(nameof(parentCategory));

            var subCategory = parentCategory.AddSubCategory(name);
            SaveCategories();
            return subCategory;
        }

        public static void RemoveCategory(Category category)
        {
            if (category == null || category.Supplier == null) return;

            if (supplierCategories.ContainsKey(category.Supplier))
            {
                // Move items to Uncategorized category if this is not the Uncategorized category
                if (category.Name != "Uncategorized" && category.Items.Any())
                {
                    var uncategorized = GetCategories(category.Supplier)
                        .FirstOrDefault(c => c.Name == "Uncategorized") 
                        ?? AddCategory("Uncategorized", category.Supplier);

                    foreach (var item in category.Items.ToList())
                    {
                        RemoveItemFromCategory(item, category);
                        AddItemToCategory(item, uncategorized);
                    }
                }

                // Clean up category images before removing
                CleanupCategoryImages(category);

                // Remove this category from its parent's subcategories
                if (category.ParentCategory != null)
                {
                    category.ParentCategory.RemoveSubCategory(category);
                }
                else
                {
                    // Only remove from supplier categories if it's a top-level category
                    supplierCategories[category.Supplier].Remove(category);
                }

                // Move subcategories to parent or root level
                foreach (var subcategory in category.SubCategories.ToList())
                {
                    if (category.ParentCategory != null)
                    {
                        subcategory.MoveToParent(category.ParentCategory);
                    }
                    else
                    {
                        subcategory.MoveToParent(null);
                        if (!supplierCategories[category.Supplier].Contains(subcategory))
                        {
                            supplierCategories[category.Supplier].Add(subcategory);
                        }
                    }
                }

                SaveCategories();
            }
        }

        public static void AddItemToCategory(Item item, Category category)
        {
            if (category == null || item == null) return;

            // Ensure the item's supplier matches the category's supplier
            if (item.Supplier != category.Supplier)
            {
                var uncategorized = GetCategories(item.Supplier)
                    .FirstOrDefault(c => c.Name == "Uncategorized") 
                    ?? AddCategory("Uncategorized", item.Supplier);
                category = uncategorized;
            }

            // Since Items is IReadOnlyList, we need to use the AddItem method of Category
            category.AddItem(item);
            SaveCategories();
        }

        public static void RemoveItemFromCategory(Item item, Category category)
        {
            if (category != null)
            {
                // Use the RemoveItem method of Category
                if (category.RemoveItem(item))
                {
                    SaveCategories();
                }
            }
        }

        public static List<Category> GetCategories(string supplier)
        {
            if (!supplierCategories.ContainsKey(supplier))
            {
                supplierCategories[supplier] = new List<Category>();
            }
            var categories = supplierCategories[supplier];
            return categories;
        }

        public static List<Category> GetAllCategories()
        {
            return supplierCategories.Values.SelectMany(x => x).ToList();
        }

        public static void MoveCategory(Category category, Category newParent)
        {
            if (category == null) return;
            if (newParent != null && category.Supplier != newParent.Supplier) return;

            category.MoveToParent(newParent);
            SaveCategories();
        }

        public static void UpdateCategoryImage(Category category, string imagePath)
        {
            if (category == null || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return;

            // Delete old image if it exists
            if (!string.IsNullOrEmpty(category.ImagePath) && File.Exists(category.ImagePath))
            {
                try { File.Delete(category.ImagePath); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting old image: {ex.Message}", "Warning");
                }
            }

            // Copy new image to category images directory
            string fileName = $"category_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(imagePath)}";
            string destinationPath = Path.Combine(categoryImagesDir, fileName);

            try
            {
                File.Copy(imagePath, destinationPath, true);
                category.ImagePath = destinationPath;
                SaveCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving category image: {ex.Message}", "Error");
            }
        }

        private static void CleanupCategoryImages(Category category)
        {
            // Delete category image if it exists
            if (!string.IsNullOrEmpty(category.ImagePath) && File.Exists(category.ImagePath))
            {
                try { File.Delete(category.ImagePath); }
                catch { /* Ignore cleanup errors */ }
            }

            // Recursively cleanup subcategory images
            foreach (var subcategory in category.SubCategories)
            {
                CleanupCategoryImages(subcategory);
            }
        }

        public static void CleanupInvalidCategories()
        {
            var invalidSuppliers = supplierCategories.Keys
                .Where(s => s.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var invalidSupplier in invalidSuppliers)
            {
                supplierCategories.Remove(invalidSupplier);
            }
            
            SaveCategories();
        }

        public static void ReloadCategories()
        {
            lock (lockObject)
            {
                isInitialized = false;
                supplierCategories.Clear();
                LoadCategories();
            }
        }
    }
} 