using System;

namespace PrintSystem.Models
{
    public class Settings
    {
        public string DefaultLabelType { get; set; } = "Basic Label";  // Default label type
        public double LabelMargin { get; set; } = 2.0;  // Default margin in mm
    }
} 