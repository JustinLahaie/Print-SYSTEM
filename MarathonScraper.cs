using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

public class MarathonScraper
{
    public class ScrapedProduct
    {
        public string ModelNumber { get; set; }
        public string Description { get; set; }
        public List<string> ImageUrls { get; set; } = new List<string>();
        public string SelectedImageUrl { get; set; }
    }

    public static async Task<ScrapedProduct> ScrapeProductAsync(string url)
    {
        try
        {
            var product = new ScrapedProduct();

            // Extract model number from URL (it's after the last slash)
            product.ModelNumber = url.Split('/').Last();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                var html = await client.GetStringAsync(url);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                Console.WriteLine("HTML Content:");
                Console.WriteLine(html.Substring(0, Math.Min(1000, html.Length)));

                // Get description (product title)
                var titleNode = doc.DocumentNode.SelectSingleNode("//h1") ?? 
                              doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'product-title')]") ??
                              doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'title')]");
                
                if (titleNode != null)
                {
                    product.Description = titleNode.InnerText.Trim();
                    Console.WriteLine($"Found title: {product.Description}");
                }

                Console.WriteLine("Searching for product images...");
                
                // Get all main product images
                var mainProductImages = doc.DocumentNode.SelectNodes("//img[@id='main_picture']");
                if (mainProductImages != null)
                {
                    foreach (var img in mainProductImages)
                    {
                        var src = img.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src) && !product.ImageUrls.Contains(src))
                        {
                            product.ImageUrls.Add(src);
                            Console.WriteLine($"Found main product image: {src}");
                        }
                    }
                }

                // Also look for other product images
                var productImages = doc.DocumentNode.SelectNodes("//img[contains(@src, 'products/')]");
                if (productImages != null)
                {
                    foreach (var img in productImages)
                    {
                        var src = img.GetAttributeValue("src", "");
                        
                        // Skip thumbnails, icons, and duplicates
                        if (!string.IsNullOrEmpty(src) && 
                            !src.Contains("icon") && 
                            !src.Contains("logo") && 
                            !src.Contains("thumb") && 
                            !product.ImageUrls.Contains(src))
                        {
                            product.ImageUrls.Add(src);
                            Console.WriteLine($"Found additional product image: {src}");
                        }
                    }
                }

                Console.WriteLine($"Total images found: {product.ImageUrls.Count}");
                return product;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to scrape product information: {ex.Message}");
        }
    }

    public static async Task<List<string>> DownloadImagesAsync(List<string> imageUrls, string destinationFolder)
    {
        var downloadedFiles = new List<string>();
        
        try
        {
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                
                foreach (var imageUrl in imageUrls)
                {
                    try
                    {
                        string fileName = $"product_image_{DateTime.Now:yyyyMMddHHmmss}_{downloadedFiles.Count + 1}.jpg";
                        string filePath = Path.Combine(destinationFolder, fileName);
                        
                        var imageBytes = await client.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                        downloadedFiles.Add(filePath);
                        Console.WriteLine($"Downloaded image to: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to download image {imageUrl}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during image downloads: {ex.Message}");
        }

        return downloadedFiles;
    }

    public static void CleanupUnusedImages(List<string> allImages, string selectedImage)
    {
        foreach (var image in allImages)
        {
            if (image != selectedImage && File.Exists(image))
            {
                try
                {
                    File.Delete(image);
                    Console.WriteLine($"Deleted unused image: {image}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete image {image}: {ex.Message}");
                }
            }
        }
    }
} 