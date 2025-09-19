// Models/DonationInput.cs
using System.ComponentModel.DataAnnotations;

namespace Becs.Models
{
    public class DonationInput
    {
        [Required, RegularExpression("O|A|B|AB")]
        public string ABO { get; set; } = "";

        [Display(Name="Rh")]
        [Required, RegularExpression(@"\+|\-")]
        public string RhSign { get; set; } = "+"; // "+" or "-"

        [Required]
        [DataType(DataType.Date)]
        public DateTime DonationDate { get; set; }

        [Required, StringLength(20, MinimumLength = 5)]
        public string DonorId { get; set; } = "";

        [Required, StringLength(100)]
        public string DonorName { get; set; } = "";
    }

    // A simple view model for the grid
    public class BloodUnitVm
    {
        public string Id { get; set; }                 // from UUID column
        public string ABO { get; set; } = "";
        public string Rh { get; set; } = "";
        public DateTime DonationDate { get; set; }
        public string DonorId { get; set; } = "";
        public string DonorName { get; set; } = "";
        public string Status { get; set; } = "";
    }
    public class RoutineIssueInput
    {
        public string? ABO { get; set; }
        public string RhSign { get; set; } = "+";
        public int Quantity { get; set; } = 1;
    }
}