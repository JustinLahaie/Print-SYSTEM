using System;

public class Item : IEquatable<Item>
{
    private static int nextId = 1;
    
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
    public Category Category { get; set; }
    public string ProductUrl { get; set; }

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