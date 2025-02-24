using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using PrintSystem.Models;
using CategoryManager = PrintSystem.Managers.CategoryManager;

namespace PrintSystem.Managers
{
    public static class ItemManager
    {
        private static List<Item> items = new List<Item>();
        private static readonly string itemsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "items.json");
        private static bool isInitialized = false;
        private static readonly object lockObject = new object();

        static ItemManager()
        {
            LoadItems();
        }

        private static void LoadItems()
        {
            lock (lockObject)
            {
                if (!isInitialized)
                {
                    if (File.Exists(itemsFilePath))
                    {
                        try
                        {
                            string jsonData = File.ReadAllText(itemsFilePath);
                            items = JsonSerializer.Deserialize<List<Item>>(jsonData) ?? new List<Item>();
                            
                            // Categories will be automatically restored through the CategoryPath property
                            // No need for additional restoration logic
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Forms.MessageBox.Show($"Error loading items: {ex.Message}", "Error");
                            items.Clear();
                        }
                    }
                    isInitialized = true;
                }
            }
        }

        private static void SaveItems()
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };
                
                string jsonData = JsonSerializer.Serialize(items, options);
                File.WriteAllText(itemsFilePath, jsonData);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error saving items: {ex.Message}", "Error");
            }
        }

        public static IReadOnlyList<Item> GetItems()
        {
            return items.AsReadOnly();
        }

        public static void AddItem(Item item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "Item cannot be null");
            
            if (item.Category == null)
                throw new ArgumentException("Item must have a category assigned", nameof(item));

            if (!items.Contains(item))
            {
                items.Add(item);
                SaveItems();
            }
        }

        public static void RemoveItem(Item item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item), "Item cannot be null");

            if (items.Remove(item))
            {
                SaveItems();
            }
        }

        public static void UpdateItem(Item oldItem, Item newItem)
        {
            if (oldItem == null)
                throw new ArgumentNullException(nameof(oldItem), "Old item cannot be null");
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "New item cannot be null");

            var index = items.IndexOf(oldItem);
            if (index != -1)
            {
                items[index] = newItem;
                SaveItems();
            }
        }

        public static List<Item> GetItemsByCategory(Category category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category), "Category cannot be null");

            return items.Where(i => i.Category == category).ToList();
        }

        public static List<Item> GetItemsBySupplier(string supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier))
                throw new ArgumentException("Supplier cannot be null or empty", nameof(supplier));

            return items.Where(i => i.Supplier == supplier).ToList();
        }
    }
} 