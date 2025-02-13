using System;
using System.Collections.Generic;

public static class SupplierManager
{
    private static List<string> suppliers = new List<string> { "Richelieu", "Marathon" };

    public static List<string> GetSuppliers()
    {
        return new List<string>(suppliers);
    }

    public static void AddSupplier(string supplier)
    {
        if (!string.IsNullOrWhiteSpace(supplier) && !suppliers.Contains(supplier))
        {
            suppliers.Add(supplier);
        }
    }
} 