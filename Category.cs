using System;
using System.Collections.Generic;
using System.Linq;

public class Category
{
    public string Name { get; set; }
    public List<Item> Items { get; set; }
    public bool IsExpanded { get; set; }
    public bool IsSupplierCategory { get; private set; }
    public List<Category> SubCategories { get; set; }
    public Category ParentCategory { get; set; }

    public Category(string name, bool isSupplierCategory = false)
    {
        Name = name;
        Items = new List<Item>();
        SubCategories = new List<Category>();
        IsExpanded = true;
        IsSupplierCategory = isSupplierCategory;
    }

    public override string ToString()
    {
        return Name;
    }

    public string GetFullPath()
    {
        if (ParentCategory == null) return Name;
        return $"{ParentCategory.GetFullPath()} > {Name}";
    }
}

public static class CategoryManager
{
    private static List<Category> categories = new List<Category>();
    private static bool isInitialized = false;

    static CategoryManager()
    {
        // Initialize with a default category
        if (!isInitialized)
        {
            AddCategory("Uncategorized");
            isInitialized = true;
        }
    }

    public static List<Category> GetCategories()
    {
        return categories;
    }

    public static Category AddCategory(string name)
    {
        var category = new Category(name);
        categories.Add(category);
        return category;
    }

    public static void RemoveCategory(Category category)
    {
        categories.Remove(category);
    }

    public static void AddItemToCategory(Item item, Category category)
    {
        // If category is null, add to Uncategorized
        if (category == null)
        {
            category = categories.FirstOrDefault(c => c.Name == "Uncategorized") ?? AddCategory("Uncategorized");
        }

        if (!category.Items.Contains(item))
        {
            category.Items.Add(item);
        }
    }

    public static void RemoveItemFromCategory(Item item, Category category)
    {
        category?.Items.Remove(item);
    }
} 