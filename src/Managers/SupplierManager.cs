using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using PrintSystem.Models;
using System.Windows.Forms;

namespace PrintSystem.Models
{
    public class Supplier
    {
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public string Description { get; set; }

        public Supplier(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

namespace PrintSystem.Managers
{
    public static class SupplierManager
    {
        private static readonly string suppliersFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "suppliers.json");
        private static readonly string supplierImagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SupplierImages");
        private static readonly string supplierLogosDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SupplierLogos");
        private static List<Supplier> suppliers = new List<Supplier>();
        private static readonly object lockObject = new object();
        private static bool isInitialized = false;

        static SupplierManager()
        {
            LoadSuppliers();
        }

        private static void LoadSuppliers()
        {
            lock (lockObject)
            {
                if (!isInitialized)
                {
                    // Ensure directories exist
                    Directory.CreateDirectory(supplierImagesDir);
                    Directory.CreateDirectory(supplierLogosDir);

                    suppliers.Clear();  // Clear any existing suppliers

                    if (File.Exists(suppliersFilePath))
                    {
                        try
                        {
                            string jsonData = File.ReadAllText(suppliersFilePath);
                            suppliers = JsonSerializer.Deserialize<List<Supplier>>(jsonData) ?? new List<Supplier>();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading suppliers.json: {ex.Message}");
                            suppliers.Clear();
                        }
                    }

                    // Always ensure both Richelieu and Marathon exist with their logos
                    var richelieu = suppliers.FirstOrDefault(s => s.Name.Equals("Richelieu", StringComparison.OrdinalIgnoreCase));
                    if (richelieu == null)
                    {
                        richelieu = new Supplier("Richelieu");
                        suppliers.Add(richelieu);
                    }

                    var marathon = suppliers.FirstOrDefault(s => s.Name.Equals("Marathon", StringComparison.OrdinalIgnoreCase));
                    if (marathon == null)
                    {
                        marathon = new Supplier("Marathon");
                        suppliers.Add(marathon);
                    }

                    // Set default logos if not already set
                    string richelieuLogo = Path.Combine(supplierLogosDir, "Richelieu_Logo.jpg");
                    string marathonLogo = Path.Combine(supplierLogosDir, "Marathon_Logo.png");

                    // First check in the SupplierLogos directory
                    if (!File.Exists(richelieuLogo))
                    {
                        // Try root directory
                        string rootRichelieuLogo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Richelieu_Logo.jpg");
                        if (File.Exists(rootRichelieuLogo))
                        {
                            File.Copy(rootRichelieuLogo, richelieuLogo, true);
                            Console.WriteLine($"Copied Richelieu logo from root to: {richelieuLogo}");
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Richelieu logo not found at: {rootRichelieuLogo}");
                        }
                    }

                    if (!File.Exists(marathonLogo))
                    {
                        // Try root directory
                        string rootMarathonLogo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Marathon_Logo.png");
                        if (File.Exists(rootMarathonLogo))
                        {
                            File.Copy(rootMarathonLogo, marathonLogo, true);
                            Console.WriteLine($"Copied Marathon logo from root to: {marathonLogo}");
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Marathon logo not found at: {rootMarathonLogo}");
                        }
                    }

                    // Update supplier logo paths
                    if (File.Exists(richelieuLogo))
                    {
                        richelieu.ImagePath = richelieuLogo;
                        Console.WriteLine($"Set Richelieu logo path to: {richelieuLogo}");
                    }

                    if (File.Exists(marathonLogo))
                    {
                        marathon.ImagePath = marathonLogo;
                        Console.WriteLine($"Set Marathon logo path to: {marathonLogo}");
                    }

                    // Save the suppliers to ensure the file exists with both suppliers
                    SaveSuppliers();
                    isInitialized = true;
                }
            }
        }

        private static void SaveSuppliers()
        {
            try
            {
                string jsonData = JsonSerializer.Serialize(suppliers, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(suppliersFilePath, jsonData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving suppliers: {ex.Message}", "Error");
            }
        }

        public static Supplier AddSupplier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Supplier name cannot be empty", nameof(name));

            if (suppliers.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return suppliers.First(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            var supplier = new Supplier(name);
            suppliers.Add(supplier);
            SaveSuppliers();
            return supplier;
        }

        public static void UpdateSupplierImage(string supplierName, string imagePath, bool isLogo = false)
        {
            var supplier = GetSupplier(supplierName);
            if (supplier == null || string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return;

            // Delete old image if it exists
            if (!string.IsNullOrEmpty(supplier.ImagePath) && File.Exists(supplier.ImagePath))
            {
                try { File.Delete(supplier.ImagePath); }
                catch { /* Ignore cleanup errors */ }
            }

            // Determine the target directory based on whether this is a logo
            string targetDir = isLogo ? supplierLogosDir : supplierImagesDir;
            
            // For logos, use a consistent name based on the supplier
            string fileName = isLogo 
                ? $"{supplier.Name}_Logo{Path.GetExtension(imagePath)}"
                : $"supplier_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(imagePath)}";
            
            string destinationPath = Path.Combine(targetDir, fileName);

            try
            {
                File.Copy(imagePath, destinationPath, true);
                supplier.ImagePath = destinationPath;
                SaveSuppliers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving supplier image: {ex.Message}", "Error");
            }
        }

        public static void RemoveSupplier(string name)
        {
            var supplier = suppliers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (supplier != null)
            {
                // Delete supplier image if it exists
                if (!string.IsNullOrEmpty(supplier.ImagePath) && File.Exists(supplier.ImagePath))
                {
                    try { File.Delete(supplier.ImagePath); }
                    catch { /* Ignore cleanup errors */ }
                }

                suppliers.Remove(supplier);
                SaveSuppliers();
            }
        }

        public static List<Supplier> GetSuppliers()
        {
            var supplierList = suppliers.ToList();
            return supplierList;
        }

        public static Supplier GetSupplier(string name)
        {
            return suppliers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static void UpdateSupplier(Supplier supplier)
        {
            if (supplier == null)
                throw new ArgumentNullException(nameof(supplier));

            var existingSupplier = GetSupplier(supplier.Name);
            if (existingSupplier != null)
            {
                existingSupplier.Description = supplier.Description;
                existingSupplier.ImagePath = supplier.ImagePath;
                SaveSuppliers();
            }
        }
    }
} 