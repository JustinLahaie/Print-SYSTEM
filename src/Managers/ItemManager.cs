using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrintSystem.Models;
using CategoryManager = PrintSystem.Managers.CategoryManager;

namespace PrintSystem.Managers
{
    public class ItemListWrapper
    {
        [JsonPropertyName("$values")]
        public List<Item> Values { get; set; }
    }

    public static class ItemManager
    {
        private static List<Item> items = new List<Item>();
        private static readonly string itemsFilePath = "items.json";
        private static bool isInitialized = false;
        private static readonly object lockObject = new object();
        private static int maxItemId = 0;

        static ItemManager()
        {
            LoadItems();
        }

        public static int GetNextAvailableId()
        {
            lock (lockObject)
            {
                return maxItemId + 1;
            }
        }

        public static void UpdateMaxId(int id)
        {
            lock (lockObject)
            {
                if (id > maxItemId)
                {
                    maxItemId = id;
                }
            }
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
                            var options = new JsonSerializerOptions
                            {
                                WriteIndented = true,
                                PropertyNameCaseInsensitive = true,
                                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                            };
                            
                            // Try to deserialize as a wrapper first (for backward compatibility)
                            try 
                            {
                                var wrapper = JsonSerializer.Deserialize<ItemListWrapper>(jsonData, options);
                                if (wrapper?.Values != null)
                                {
                                    items = wrapper.Values;
                                }
                                else
                                {
                                    // Try direct deserialization
                                    var loadedItems = JsonSerializer.Deserialize<List<Item>>(jsonData, options);
                                    items = loadedItems ?? new List<Item>();
                                }
                            }
                            catch (JsonException)
                            {
                                // If wrapper deserialization fails, try direct deserialization
                                var loadedItems = JsonSerializer.Deserialize<List<Item>>(jsonData, options);
                                items = loadedItems ?? new List<Item>();
                            }

                            // Find the maximum ID from loaded items
                            maxItemId = items.Any() ? items.Max(i => i.Id) : 0;
                        }
                        catch (JsonException ex)
                        {
                            System.Windows.Forms.MessageBox.Show($"Error parsing items file: {ex.Message}\nCreating new items list.", "Warning");
                            items = new List<Item>();
                            maxItemId = 0;
                            
                            // Backup the problematic file
                            if (File.Exists(itemsFilePath))
                            {
                                string backupPath = itemsFilePath + ".backup";
                                try
                                {
                                    File.Copy(itemsFilePath, backupPath, true);
                                    System.Windows.Forms.MessageBox.Show($"Original items file backed up to: {backupPath}", "Backup Created");
                                }
                                catch
                                {
                                    // Ignore backup errors
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Forms.MessageBox.Show($"Error loading items: {ex.Message}", "Error");
                            items = new List<Item>();
                            maxItemId = 0;
                        }
                    }
                    else
                    {
                        items = new List<Item>();
                        maxItemId = 0;
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
                    PropertyNameCaseInsensitive = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                
                // Create a new items.json file with the correct format
                var wrapper = new ItemListWrapper { Values = items };
                string jsonData = JsonSerializer.Serialize(wrapper, options);
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