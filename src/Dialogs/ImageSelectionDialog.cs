using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

public class ImageSelectionDialog : Form
{
    private FlowLayoutPanel imagePanel;
    private Button okButton;
    private Button cancelButton;
    private List<string> imagePaths;
    private string selectedImagePath;
    private List<PictureBox> pictureBoxes = new List<PictureBox>();

    public string SelectedImagePath => selectedImagePath;

    public ImageSelectionDialog(List<string> imagePaths)
    {
        this.imagePaths = imagePaths;
        InitializeComponents();
        LoadImages();
    }

    private void InitializeComponents()
    {
        this.Text = "Select Product Image";
        this.Size = new Size(800, 600);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;

        // Create image panel
        imagePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(10)
        };

        // Create button panel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50
        };

        okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Enabled = false,
            Location = new Point(buttonPanel.Width - 170, 10),
            Width = 75
        };

        cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(buttonPanel.Width - 85, 10),
            Width = 75
        };

        buttonPanel.Controls.AddRange(new Control[] { okButton, cancelButton });

        this.Controls.AddRange(new Control[] { imagePanel, buttonPanel });
    }

    private void LoadImages()
    {
        foreach (var imagePath in imagePaths)
        {
            try
            {
                var pictureBox = new PictureBox
                {
                    Size = new Size(200, 200),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(5),
                    Tag = imagePath
                };

                using (var image = Image.FromFile(imagePath))
                {
                    pictureBox.Image = new Bitmap(image);
                }

                pictureBox.Click += PictureBox_Click;
                imagePanel.Controls.Add(pictureBox);
                pictureBoxes.Add(pictureBox);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image {imagePath}: {ex.Message}", "Error");
            }
        }
    }

    private void PictureBox_Click(object sender, EventArgs e)
    {
        var clickedPictureBox = (PictureBox)sender;

        // Reset border for all picture boxes
        foreach (var pb in pictureBoxes)
        {
            pb.BorderStyle = BorderStyle.FixedSingle;
        }

        // Highlight selected picture box
        clickedPictureBox.BorderStyle = BorderStyle.Fixed3D;
        selectedImagePath = (string)clickedPictureBox.Tag;
        okButton.Enabled = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        
        // Dispose all images
        foreach (var pictureBox in pictureBoxes)
        {
            if (pictureBox.Image != null)
            {
                pictureBox.Image.Dispose();
                pictureBox.Image = null;
            }
        }
    }
} 