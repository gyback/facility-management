using FacilityManagementService.Application.Validation;

namespace FacilityManagementService.Application.Tests.Validation;

public sealed class ValidationErrorTests
{
    [Fact]
    public void Required_has_no_parameters()
    {
        var error = ValidationError.Required("Name");

        Assert.Equal(new ValidationError("Name", "required", "Is required."), error);
    }

    [Fact]
    public void Too_long_carries_the_maximum_length()
    {
        var error = ValidationError.TooLong("Body", 4000);

        Assert.Equal("Body", error.PropertyName);
        Assert.Equal("too-long", error.Code);
        Assert.Equal("Must be at most 4000 characters.", error.Message);
        Assert.Equal(new Dictionary<string, object> { ["maxLength"] = 4000 }, error.Parameters);
    }

    [Fact]
    public void Out_of_range_carries_the_minimum_and_maximum()
    {
        var error = ValidationError.OutOfRange("Capacity", 1, 100);

        Assert.Equal("out-of-range", error.Code);
        Assert.Equal("Must be between 1 and 100.", error.Message);
        Assert.Equal(new Dictionary<string, object> { ["minimum"] = 1, ["maximum"] = 100 }, error.Parameters);
    }

    [Fact]
    public void Must_be_after_carries_the_other_propertys_wire_name()
    {
        var error = ValidationError.MustBeAfter("To", "From");

        Assert.Equal("must-be-after", error.Code);
        Assert.Equal("Must be after from.", error.Message);
        Assert.Equal(new Dictionary<string, object> { ["otherProperty"] = "from" }, error.Parameters);
    }

    [Fact]
    public void All_holds_every_shared_code() =>
        Assert.Equal(
            ["must-be-after", "out-of-range", "required", "too-long"],
            ValidationCodes.All.Order());
}
