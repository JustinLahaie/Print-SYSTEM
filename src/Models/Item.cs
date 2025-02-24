using System;
using System.Text.Json.Serialization;
using PrintSystem.Managers;

namespace PrintSystem.Models
{
    public class Item : IEquatable<Item>
    {
        private static int nextId = 1;
        private Category category;
        
        public Item()
        {
            Id = nextId++;
        }
        
        public int Id { get; private set; }
        public string ModelNumber { get; set; }
        public string Description { get; set; }
        public string Supplier { get; set; }
        public int DefaultOrderQuantity { get; set; }
        public string ImagePath { get; set; }
        public string ProductUrl { get; set; }
        
        [JsonIgnore]
        public Category Category 
        { 
            get => category;
            set
            {
                if (category == value) return;
                
                var oldCategory = category;
                category = value;
                
                // Update supplier to match category's supplier
                if (category != null)
                {
                    Supplier = category.Supplier;
                }
                
                // Handle category change in CategoryManager
                if (oldCategory != null)
                {
                    CategoryManager.RemoveItemFromCategory(this, oldCategory);
                }
                if (category != null)
                {
                    CategoryManager.AddItemToCategory(this, category);
                }
            }
        }

        // Add a property to store the category path for serialization
        public string CategoryPath 
        { 
            get => Category?.GetFullPath();
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                
                // Split the path and find the category
                var pathParts = value.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length > 0)
                {
                    var supplier = pathParts[0];
                    var categories = CategoryManager.GetCategories(supplier);
                    
                    foreach (var category in categories)
                    {
                        if (category.GetFullPath() == value)
                        {
                            Category = category;
                            break;
                        }
                    }
                }
            }
        }

        public bool Equals(Item other)
        {
            if (other is null) return false;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Item);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
} 
