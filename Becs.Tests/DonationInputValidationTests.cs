//dotnet test Becs.Tests/Becs.Tests.csproj
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Becs.Models;
using FluentAssertions;
using Xunit;

public class DonationInputValidationTests
{
    [Fact]
    public void Valid_Model_Passes_Validation()
    {
        var model = new DonationInput
        {
            ABO = "A",
            RhSign = "+",
            DonationDate = DateTime.Today,
            DonorId = "123456789",
            DonorName = "John Doe"
        };

        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);

        isValid.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData("X")]   // invalid ABO
    [InlineData("")]    // empty
    public void Invalid_ABO_Fails_Validation(string abo)
    {
        var model = new DonationInput
        {
            ABO = abo,
            RhSign = "+",
            DonationDate = DateTime.Today,
            DonorId = "123456789",
            DonorName = "Jane Doe"
        };

        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, ctx, results, true);

        isValid.Should().BeFalse();
        results.Should().ContainSingle();
    }
}