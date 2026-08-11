using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Students;

namespace AdminPortal.UnitTests;

public sealed class ValidationRulesTests
{
    [Fact]
    public void PasswordPolicyAcceptsStrongPassword() =>
        PasswordPolicy.Validate("StrongPassword1!");

    [Fact]
    public void PasswordPolicyRejectsWeakPassword() =>
        Assert.Throws<AppValidationException>(() => PasswordPolicy.Validate("weak"));

    [Fact]
    public void StudentDateOfBirthRejectsMissingValue() =>
        Assert.Throws<AppValidationException>(() => StudentRules.ValidateDateOfBirth(default, new DateOnly(2026, 8, 11)));

    [Fact]
    public void StudentDateOfBirthRejectsFutureValue() =>
        Assert.Throws<AppValidationException>(() =>
            StudentRules.ValidateDateOfBirth(new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 11)));
}
