using System;
using System.Drawing;
using System.Windows.Forms;

namespace PrintSystem.Models
{
    public abstract class LabelElement : IDisposable
    {
        protected Rectangle bounds;
        protected bool isSelected;
        protected bool isResizing;

        public Rectangle Bounds
        {
            get { return bounds; }
            set { bounds = value; }
        }

        public bool IsSelected
        {
            get { return isSelected; }
            set { isSelected = value; }
        }

        public LabelElement(Point location)
        {
            bounds = new Rectangle(location, new Size(100, 50));
            isSelected = false;
            isResizing = false;
        }

        public abstract void Draw(Graphics g);

        public virtual void BeginDragOrResize()
        {
            isSelected = true;
        }

        public virtual void EndDragOrResize()
        {
            isResizing = false;
        }

        public virtual void Resize(Point mouseLocation)
        {
            // Base implementation for resizing an element
            int width = Math.Max(50, mouseLocation.X - bounds.X);
            int height = Math.Max(25, mouseLocation.Y - bounds.Y);
            bounds.Size = new Size(width, height);
        }

        public virtual bool ContainsPoint(Point point)
        {
            return bounds.Contains(point);
        }

        public virtual bool IsInResizeArea(Point point)
        {
            var resizeHandleRect = new Rectangle(
                bounds.Right - 10, bounds.Bottom - 10, 10, 10);
            return resizeHandleRect.Contains(point);
        }

        public virtual void Move(int deltaX, int deltaY)
        {
            bounds.X += deltaX;
            bounds.Y += deltaY;
        }

        public virtual void Dispose()
        {
            // Default implementation does nothing
            // Child classes should override if they have resources to dispose
        }
    }

    public class TextElement : LabelElement
    {
        private string text;
        private string fontFamily;
        private int fontSize;

        public TextElement(string text, Point location, string fontFamily = "Arial", int fontSize = 12)
            : base(location)
        {
            this.text = text;
            this.fontFamily = fontFamily;
            this.fontSize = fontSize;
        }

        public string GetText() => text;
        public string GetFontFamily() => fontFamily;
        public int GetFontSize() => fontSize;

        public override void Draw(Graphics g)
        {
            using (var font = new Font(fontFamily, fontSize))
            using (var textBrush = new SolidBrush(Color.Black))
            using (var borderPen = new Pen(isSelected ? Color.Blue : Color.DarkGray, 1))
            {
                // Draw the text content
                g.DrawString(text, font, textBrush, bounds);

                // Draw the border
                g.DrawRectangle(borderPen, bounds);

                // Draw the resize handle if selected
                if (isSelected)
                {
                    var resizeHandleRect = new Rectangle(
                        bounds.Right - 10, bounds.Bottom - 10, 10, 10);
                    g.FillRectangle(new SolidBrush(Color.Blue), resizeHandleRect);
                }
            }
        }

        public void SetText(string newText)
        {
            text = newText;
        }

        public void SetFont(string newFontFamily, int newFontSize)
        {
            fontFamily = newFontFamily;
            fontSize = newFontSize;
        }
    }

    public class ImageElement : LabelElement
    {
        private Image image;
        private string imagePath;
        private bool isPlaceholder;

        public ImageElement(string imagePath, Point location) : base(location)
        {
            this.imagePath = imagePath;
            this.isPlaceholder = string.IsNullOrEmpty(imagePath);
            
            if (!isPlaceholder && System.IO.File.Exists(imagePath))
            {
                try
                {
                    image = Image.FromFile(imagePath);
                    bounds.Size = new Size(image.Width, image.Height);
                }
                catch (Exception)
                {
                    isPlaceholder = true;
                }
            }
        }

        public string GetImagePath() => imagePath;

        public bool IsPlaceholder() => isPlaceholder;

        public override void Draw(Graphics g)
        {
            if (isPlaceholder || image == null)
            {
                // Draw placeholder
                using (var placeholderBrush = new SolidBrush(Color.LightGray))
                using (var placeholderPen = new Pen(Color.DarkGray, 1))
                using (var placeholderFont = new Font("Arial", 8))
                using (var textBrush = new SolidBrush(Color.DarkGray))
                {
                    g.FillRectangle(placeholderBrush, bounds);
                    g.DrawRectangle(placeholderPen, bounds);
                    
                    // Draw an X across the placeholder
                    g.DrawLine(placeholderPen, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                    g.DrawLine(placeholderPen, bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
                    
                    // Display "Image" text
                    string placeholder = "Image";
                    SizeF textSize = g.MeasureString(placeholder, placeholderFont);
                    g.DrawString(placeholder, placeholderFont, textBrush, 
                        bounds.X + (bounds.Width - textSize.Width) / 2,
                        bounds.Y + (bounds.Height - textSize.Height) / 2);
                }
            }
            else
            {
                // Draw the actual image
                g.DrawImage(image, bounds);
            }

            // Draw border and resize handle if selected
            using (var borderPen = new Pen(isSelected ? Color.Blue : Color.DarkGray, 1))
            {
                g.DrawRectangle(borderPen, bounds);

                if (isSelected)
                {
                    var resizeHandleRect = new Rectangle(
                        bounds.Right - 10, bounds.Bottom - 10, 10, 10);
                    g.FillRectangle(new SolidBrush(Color.Blue), resizeHandleRect);
                }
            }
        }

        public void UpdateImagePath(string newImagePath)
        {
            if (image != null)
            {
                image.Dispose();
                image = null;
            }

            imagePath = newImagePath;
            isPlaceholder = string.IsNullOrEmpty(newImagePath);
            
            if (!isPlaceholder && System.IO.File.Exists(newImagePath))
            {
                try
                {
                    image = Image.FromFile(newImagePath);
                }
                catch (Exception)
                {
                    isPlaceholder = true;
                }
            }
        }

        public override void Dispose()
        {
            if (image != null)
            {
                image.Dispose();
                image = null;
            }
        }
    }

    public class QRElement : LabelElement
    {
        private Image qrImage;
        private string templateKey;
        private string templateContent;
        private Bitmap originalBitmap; // Store the high-resolution bitmap

        public QRElement(Image qrImage, Point location, string templateKey = null, string templateContent = null)
            : base(location)
        {
            this.templateKey = templateKey;
            this.templateContent = templateContent;
            
            // Store high-resolution bitmap for better quality rendering
            if (qrImage is Bitmap bmp)
            {
                try
                {
                    // Create a deep copy of the bitmap with the same pixel format
                    Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    this.originalBitmap = bmp.Clone(rect, bmp.PixelFormat);
                    this.qrImage = new Bitmap(this.originalBitmap);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error cloning bitmap: {ex.Message}");
                    // Fallback to simple copy
                    this.originalBitmap = new Bitmap(bmp);
                    this.qrImage = bmp;
                }
            }
            else if (qrImage != null)
            {
                try
                {
                    // Convert to bitmap if it isn't already
                    this.originalBitmap = new Bitmap(qrImage);
                    this.qrImage = qrImage;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating bitmap: {ex.Message}");
                    this.qrImage = qrImage;
                }
            }
            
            bounds.Size = new Size(qrImage != null ? qrImage.Width : 150, qrImage != null ? qrImage.Height : 150);
        }

        public string GetTemplateKey() => templateKey;
        public string GetTemplateContent() => templateContent;
        
        // Getter for the high-resolution bitmap
        public Bitmap GetOriginalBitmap() => originalBitmap;

        public override void Draw(Graphics g)
        {
            if (qrImage != null)
            {
                g.DrawImage(qrImage, bounds);
            }
            else
            {
                // Draw placeholder if no QR image
                using (var placeholderBrush = new SolidBrush(Color.LightGray))
                using (var placeholderPen = new Pen(Color.DarkGray, 1))
                using (var placeholderFont = new Font("Arial", 8))
                using (var textBrush = new SolidBrush(Color.DarkGray))
                {
                    g.FillRectangle(placeholderBrush, bounds);
                    g.DrawRectangle(placeholderPen, bounds);
                    
                    // Draw placeholder text
                    string placeholder = "QR Code";
                    SizeF textSize = g.MeasureString(placeholder, placeholderFont);
                    g.DrawString(placeholder, placeholderFont, textBrush, 
                        bounds.X + (bounds.Width - textSize.Width) / 2,
                        bounds.Y + (bounds.Height - textSize.Height) / 2);
                }
            }

            // Draw border and resize handle if selected
            using (var borderPen = new Pen(isSelected ? Color.Blue : Color.DarkGray, 1))
            {
                g.DrawRectangle(borderPen, bounds);

                if (isSelected)
                {
                    var resizeHandleRect = new Rectangle(
                        bounds.Right - 10, bounds.Bottom - 10, 10, 10);
                    g.FillRectangle(new SolidBrush(Color.Blue), resizeHandleRect);
                }
            }
        }

        public override void Resize(Point mouseLocation)
        {
            // Make sure QR codes maintain a square aspect ratio
            int size = Math.Max(50, Math.Max(mouseLocation.X - bounds.X, mouseLocation.Y - bounds.Y));
            bounds.Size = new Size(size, size);
        }

        public override void Dispose()
        {
            if (qrImage != null)
            {
                qrImage.Dispose();
                qrImage = null;
            }
            
            if (originalBitmap != null)
            {
                originalBitmap.Dispose();
                originalBitmap = null;
            }
        }
    }
} 