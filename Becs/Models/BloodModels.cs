namespace Becs.Models;

public enum Rh { Neg, Pos }

public record BloodType(string ABO, Rh Rh);  // ABO: "O","A","B","AB"

public class BloodUnit
{
    public string Id { get; set; } = default!;
    public BloodType Type { get; set; } = new("O", Rh.Pos);
    public DateTime DonationDate { get; set; }
    public string DonorId { get; set; } = "";
    public string DonorName { get; set; } = "";
}

public class DonationInput
{
    public string ABO { get; set; } = "O";
    public string RhSign { get; set; } = "+";
    public DateTime DonationDate { get; set; } = DateTime.Today;
    public string DonorId { get; set; } = "";
    public string DonorName { get; set; } = "";
}

public class RoutineIssueInput
{
    public string ABO { get; set; } = "A";
    public string RhSign { get; set; } = "+";
    public int Quantity { get; set; } = 2;
}