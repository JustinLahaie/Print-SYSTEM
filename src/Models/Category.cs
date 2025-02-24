using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;

namespace PrintSystem.Models
{
    /// <summary>
    /// Represents a category in the hierarchical category system.
    /// Categories can contain items and subcategories, forming a tree structure.
    /// </summary>
    public class Category
    {
        private readonly ConcurrentDictionary<string, Category> _categoryCache;
        private readonly List<Item> _items;
        private readonly List<Category> _subCategories;
        
        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Category name must be between 1 and 100 characters")]
        public string Name { get; set; }

        public IReadOnlyList<Item> Items => _items;

        public bool IsExpanded { get; set; }

        [StringLength(100, ErrorMessage = "Supplier name cannot exceed 100 characters")]
        public string Supplier { get; set; }

        public IReadOnlyList<Category> SubCategories => _subCategories;
        
        [JsonIgnore]
        public Category ParentCategory { get; internal set; }
        
        [StringLength(500, ErrorMessage = "Image path cannot exceed 500 characters")]
        public string ImagePath { get; set; }
        
        [JsonIgnore]
        public bool IsSupplierCategory => ParentCategory == null;

        /// <summary>
        /// Initializes a new instance of the Category class.
        /// </summary>
        public Category()
        {
            _items = new List<Item>();
            _subCategories = new List<Category>();
            _categoryCache = new ConcurrentDictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            IsExpanded = true;
        }

        /// <summary>
        /// Initializes a new instance of the Category class with a specified name and optional supplier.
        /// </summary>
        /// <param name="name">The name of the category</param>
        /// <param name="supplier">The supplier associated with the category</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null</exception>
        /// <exception cref="ArgumentException">Thrown when name is empty or whitespace</exception>
        public Category(string name, string supplier = null) : this()
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name cannot be empty or whitespace", nameof(name));

            Name = name;
            Supplier = supplier;
        }

        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Gets the full hierarchical path of the category.
        /// </summary>
        /// <returns>A string representing the full path from root to this category</returns>
        public string GetFullPath()
        {
            var path = new List<string>();
            var current = this;
            
            while (current != null)
            {
                path.Insert(0, current.Name);
                current = current.ParentCategory;
            }
            
            if (!string.IsNullOrEmpty(Supplier))
            {
                path.Insert(0, Supplier);
            }
            
            return string.Join(" > ", path);
        }

        /// <summary>
        /// Gets all items in this category and its subcategories.
        /// </summary>
        /// <returns>An enumerable of all items</returns>
        public IEnumerable<Item> GetAllItems()
        {
            return _items.Concat(_subCategories.SelectMany(sc => sc.GetAllItems()));
        }

        /// <summary>
        /// Finds a subcategory by name, including in nested subcategories.
        /// </summary>
        /// <param name="name">The name of the subcategory to find</param>
        /// <returns>The found category or null if not found</returns>
        public Category FindSubCategory(string name)
        {
            if (string.IsNullOrEmpty(name)) 
                return null;

            // Check cache first
            if (_categoryCache.TryGetValue(name, out var cachedCategory))
                return cachedCategory;

            var category = _subCategories.FirstOrDefault(sc => sc.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) 
                ?? _subCategories.Select(sc => sc.FindSubCategory(name)).FirstOrDefault(found => found != null);

            if (category != null)
                _categoryCache.TryAdd(name, category);

            return category;
        }

        /// <summary>
        /// Creates a new supplier-level category. This should only be used for top-level supplier categories.
        /// </summary>
        /// <param name="supplierName">The name of the supplier</param>
        /// <returns>A new supplier-level category</returns>
        public static Category CreateSupplierCategory(string supplierName)
        {
            if (string.IsNullOrWhiteSpace(supplierName))
                throw new ArgumentException("Supplier name cannot be empty", nameof(supplierName));

            return new Category(supplierName)
            {
                Supplier = supplierName,
                ParentCategory = null
            };
        }

        /// <summary>
        /// Validates that this category follows the correct hierarchy rules.
        /// </summary>
        /// <returns>True if the hierarchy is valid, false otherwise</returns>
        public bool ValidateHierarchy()
        {
            // If this is a supplier category (root level)
            if (ParentCategory == null)
            {
                return !string.IsNullOrEmpty(Supplier) && Name == Supplier;
            }

            // For all other categories
            if (string.IsNullOrEmpty(Supplier))
                return false;

            // Verify supplier matches all the way up the chain
            var current = this;
            while (current != null)
            {
                if (current.Supplier != this.Supplier)
                    return false;
                current = current.ParentCategory;
            }

            return true;
        }

        /// <summary>
        /// Adds a new subcategory to this category.
        /// </summary>
        /// <param name="name">The name of the new subcategory</param>
        /// <returns>The newly created subcategory</returns>
        /// <exception cref="ArgumentException">Thrown when a subcategory with the same name already exists or when trying to add to an invalid parent</exception>
        public Category AddSubCategory(string name)
        {
            if (_subCategories.Any(sc => sc.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"A subcategory with the name '{name}' already exists", nameof(name));

            if (string.IsNullOrEmpty(this.Supplier))
                throw new InvalidOperationException("Cannot add subcategories to a category without a supplier");

            var subCategory = new Category(name, this.Supplier) { ParentCategory = this };
            _subCategories.Add(subCategory);
            _categoryCache.TryAdd(name, subCategory);
            return subCategory;
        }

        /// <summary>
        /// Adds an item to this category.
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <exception cref="ArgumentNullException">Thrown when item is null</exception>
        public void AddItem(Item item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!_items.Contains(item))
                _items.Add(item);
        }

        /// <summary>
        /// Removes an item from this category.
        /// </summary>
        /// <param name="item">The item to remove</param>
        /// <returns>True if the item was removed, false otherwise</returns>
        public bool RemoveItem(Item item)
        {
            return _items.Remove(item);
        }

        /// <summary>
        /// Moves this category to a new parent category.
        /// </summary>
        /// <param name="newParent">The new parent category</param>
        /// <exception cref="InvalidOperationException">Thrown when trying to move to a parent with different supplier</exception>
        public void MoveToParent(Category newParent)
        {
            if (newParent != null && newParent.Supplier != this.Supplier)
                throw new InvalidOperationException("Cannot move category to a parent with different supplier");

            if (ParentCategory != null)
            {
                ParentCategory._subCategories.Remove(this);
                Category dummy;
                ParentCategory._categoryCache.TryRemove(this.Name, out dummy);
            }
            
            ParentCategory = newParent;
            
            if (newParent != null)
            {
                newParent._subCategories.Add(this);
                newParent._categoryCache.TryAdd(this.Name, this);
            }
        }

        /// <summary>
        /// Gets the depth level of this category in the hierarchy.
        /// </summary>
        /// <returns>The depth level (0 for root categories)</returns>
        public int GetHierarchyLevel()
        {
            int level = 0;
            var current = this;
            while (current.ParentCategory != null)
            {
                level++;
                current = current.ParentCategory;
            }
            return level;
        }

        /// <summary>
        /// Removes a subcategory from this category.
        /// </summary>
        /// <param name="subCategory">The subcategory to remove</param>
        /// <returns>True if the subcategory was removed, false otherwise</returns>
        public bool RemoveSubCategory(Category subCategory)
        {
            if (subCategory == null)
                return false;

            Category dummy;
            _categoryCache.TryRemove(subCategory.Name, out dummy);
            return _subCategories.Remove(subCategory);
        }

        /// <summary>
        /// Clears the category cache to free up memory.
        /// </summary>
        public void ClearCache()
        {
            _categoryCache.Clear();
        }
    }
} 