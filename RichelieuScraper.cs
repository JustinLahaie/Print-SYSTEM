using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

public class RichelieuScraper
{
    public class ScrapedProduct
    {
        public string ModelNumber { get; set; }
        public string Description { get; set; }
        public List<string> ImageUrls { get; set; } = new List<string>();
    }

    public static async Task<ScrapedProduct> ScrapeProductAsync(string url)
    {
        try
        {
            var product = new ScrapedProduct();

            // Extract model number from URL
            if (url.Contains("sku-"))
            {
                product.ModelNumber = url.Substring(url.LastIndexOf("sku-") + 4);
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                var html = await client.GetStringAsync(url);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // Get description (product title)
                var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
                if (titleNode != null)
                {
                    product.Description = titleNode.InnerText.Trim();
                }

                Console.WriteLine("Searching for product images...");
                
                // First try to find the main product image specifically
                var mainProductImage = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'pms-AddCartBlocTop')]//img | //div[contains(@class, 'product-main-image')]//img");
                if (mainProductImage != null)
                {
                    Console.WriteLine("Found main product image");
                    var imgUrl = GetBestImageUrl(mainProductImage);
                    if (!string.IsNullOrEmpty(imgUrl) && !product.ImageUrls.Contains(imgUrl))
                    {
                        product.ImageUrls.Add(imgUrl);
                        Console.WriteLine($"Added main product image URL: {imgUrl}");
                    }
                }

                // Look for additional product images
                var productImages = doc.DocumentNode.SelectNodes("//div[not(contains(@class, 'suggested')) and not(contains(@class, 'related'))]//img[contains(@class, 'ts-ImgMain') or contains(@class, 'itemImg') or contains(@class, 'pms-AddCartBlocTop_Image')]");
                
                if (productImages != null)
                {
                    Console.WriteLine($"Found {productImages.Count} potential additional product images");
                    foreach (var img in productImages)
                    {
                        Console.WriteLine("\nChecking product image:");
                        var className = img.GetAttributeValue("class", "");
                        var alt = img.GetAttributeValue("alt", "");
                        
                        // Skip images that are clearly not product images
                        if (alt.Contains("delete") || alt.Contains("print"))
                        {
                            Console.WriteLine("Skipping non-product image");
                            continue;
                        }

                        var imgUrl = GetBestImageUrl(img);
                        if (!string.IsNullOrEmpty(imgUrl) && !product.ImageUrls.Contains(imgUrl))
                        {
                            product.ImageUrls.Add(imgUrl);
                            Console.WriteLine($"Added additional product image URL: {imgUrl}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No additional product images found");
                }

                return product;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to scrape product information: {ex.Message}");
        }
    }

    private static string GetBestImageUrl(HtmlNode img)
    {
        var src = img.GetAttributeValue("src", "");
        var dataSrc = img.GetAttributeValue("data-src", "");
        var dataZoomImage = img.GetAttributeValue("data-zoom-image", "");

        Console.WriteLine($"src: {src}");
        Console.WriteLine($"data-src: {dataSrc}");
        Console.WriteLine($"data-zoom-image: {dataZoomImage}");

        // Skip images from static folders
        if (src.Contains("/img/") || src.Contains("/images/") || string.IsNullOrEmpty(src))
        {
            return null;
        }

        // Try to get the highest resolution image URL
        string imgUrl = dataZoomImage;
        if (string.IsNullOrEmpty(imgUrl))
        {
            imgUrl = dataSrc;
        }
        if (string.IsNullOrEmpty(imgUrl))
        {
            imgUrl = src;
        }

        if (!string.IsNullOrEmpty(imgUrl))
        {
            // Clean up the URL
            imgUrl = imgUrl.Replace("&amp;", "&");
            
            // Add domain if it's a relative URL
            if (!imgUrl.StartsWith("http"))
            {
                imgUrl = imgUrl.StartsWith("/") 
                    ? "https://www.richelieu.com" + imgUrl 
                    : "https://www.richelieu.com/" + imgUrl;
            }

            // Try to get higher resolution version
            if (!imgUrl.Contains("_700.jpg"))
            {
                imgUrl = imgUrl.Replace("_300.jpg", "_700.jpg");
            }
            
            return imgUrl;
        }

        return null;
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