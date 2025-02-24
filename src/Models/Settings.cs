using System;

namespace PrintSystem.Models
{
    public class Settings
    {
        public double LabelWidth { get; set; } = 50.0;  // Default width in mm
        public double LabelHeight { get; set; } = 30.0;  // Default height in mm
        public double LabelMargin { get; set; } = 2.0;  // Default margin in mm
    }
} 