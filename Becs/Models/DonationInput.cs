namespace Becs.Models;
using System.ComponentModel.DataAnnotations;

public class Donationinput
{
    [Required] public string ABO { get; set; } = "O";
    [Required, RegularExpression(@"[+-]")] public string RhSign { get; set; } = "+";
    [DataType(DataType.Date)] public DateTime DonationDate { get; set; } = DateTime.Today;
    [Required, StringLength(12, MinimumLength = 5)] public string DonorId { get; set; } = "";
    [Required, StringLength(80)] public string DonorName { get; set; } = "";
}