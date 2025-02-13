using System;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;

public class RichelieuScraper
{
    public class ScrapedProduct
    {
        public string ModelNumber { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
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

                Console.WriteLine("Searching for product image...");
                
                // First try to find the main product image specifically
                var mainProductImage = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'pms-AddCartBlocTop')]//img | //div[contains(@class, 'product-main-image')]//img");
                if (mainProductImage != null)
                {
                    Console.WriteLine("Found main product image");
                    var src = mainProductImage.GetAttributeValue("src", "");
                    var dataSrc = mainProductImage.GetAttributeValue("data-src", "");
                    var dataZoomImage = mainProductImage.GetAttributeValue("data-zoom-image", "");
                    
                    Console.WriteLine($"src: {src}");
                    Console.WriteLine($"data-src: {dataSrc}");
                    Console.WriteLine($"data-zoom-image: {dataZoomImage}");

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
                        
                        product.ImageUrl = imgUrl;
                        Console.WriteLine($"Found main product image URL: {product.ImageUrl}");
                        return product;
                    }
                }

                // Fallback: Try to find product images with specific classes, excluding suggested products
                var productImages = doc.DocumentNode.SelectNodes("//div[not(contains(@class, 'suggested')) and not(contains(@class, 'related'))]//img[contains(@class, 'ts-ImgMain') or contains(@class, 'itemImg') or contains(@class, 'pms-AddCartBlocTop_Image')]");
                
                if (productImages != null)
                {
                    Console.WriteLine($"Found {productImages.Count} potential product images");
                    foreach (var img in productImages)
                    {
                        Console.WriteLine("\nChecking product image:");
                        var src = img.GetAttributeValue("src", "");
                        var dataSrc = img.GetAttributeValue("data-src", "");
                        var dataZoomImage = img.GetAttributeValue("data-zoom-image", "");
                        var className = img.GetAttributeValue("class", "");
                        var alt = img.GetAttributeValue("alt", "");
                        
                        Console.WriteLine($"src: {src}");
                        Console.WriteLine($"data-src: {dataSrc}");
                        Console.WriteLine($"data-zoom-image: {dataZoomImage}");
                        Console.WriteLine($"class: {className}");
                        Console.WriteLine($"alt: {alt}");

                        // Skip images that are clearly not product images
                        if (src.Contains("/img/") || src.Contains("/images/") || 
                            alt.Contains("delete") || alt.Contains("print") ||
                            string.IsNullOrEmpty(src))
                        {
                            Console.WriteLine("Skipping non-product image");
                            continue;
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

                            // Check if this is a product image matching our expected pattern (e.g. 1061880_700.jpg)
                            if (imgUrl.Contains("_700.jpg"))
                            {
                                product.ImageUrl = imgUrl;
                                Console.WriteLine($"Found matching product image URL: {product.ImageUrl}");
                                break;
                            }

                            // If we haven't found a _700 image yet, store this as a fallback
                            if (string.IsNullOrEmpty(product.ImageUrl))
                            {
                                // Try to get higher resolution version
                                imgUrl = imgUrl.Replace("_300.jpg", "_700.jpg");
                                product.ImageUrl = imgUrl;
                                Console.WriteLine($"Found potential product image URL: {product.ImageUrl}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No product images found");
                }

                return product;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to scrape product information: {ex.Message}");
        }
    }

    public static async Task<string> DownloadImageAsync(string imageUrl, string destinationFolder)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl)) return null;

            // Create the images directory if it doesn't exist
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // Generate a filename based on timestamp to avoid conflicts
            string fileName = $"product_image_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            string filePath = Path.Combine(destinationFolder, fileName);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                var imageBytes = await client.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(filePath, imageBytes);
                return filePath;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to download image: {ex.Message}");
        }
    }
} 