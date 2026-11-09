using GoshehYar.Core.Calc;

namespace GoshehYar.Tests;

public class CalcTests
{
    private static decimal Eval(string text)
    {
        var r = ExpressionEvaluator.Evaluate(text);
        Assert.True(r.Ok, $"'{text}' failed with {r.Error}");
        return r.Value;
    }

    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("10-4-3", 3)]
    [InlineData("100/4/5", 5)]
    [InlineData("2^3^2", 512)]
    [InlineData("-2^2", -4)]
    [InlineData("-(3+4)", -7)]
    [InlineData("2*-3", -6)]
    [InlineData("0.1+0.2", 0.3)]
    [InlineData("sqrt(16)+abs(-3)", 7)]
    [InlineData("7/2", 3.5)]
    public void Precedence(string text, double expected) => Assert.Equal((decimal)expected, Eval(text));

    [Fact]
    public void PercentConventions()
    {
        Assert.Equal(110m, Eval("100+10%"));
        Assert.Equal(90m, Eval("100-10%"));
        Assert.Equal(20m, Eval("200*10%"));
        Assert.Equal(0.5m, Eval("50%"));
        Assert.Equal(50m, Eval("200/4"));
    }

    [Fact]
    public void PersianDigitsAndOperators()
    {
        Assert.Equal(7m, Eval("۱+۲×۳"));
        Assert.Equal(2.5m, Eval("۵÷۲"));
        Assert.Equal(1.5m, Eval("۱٫۵"));
        Assert.Equal(12m, Eval("٣*٤"));
    }

    [Fact]
    public void Errors()
    {
        Assert.Equal(EvalError.Empty, ExpressionEvaluator.Evaluate("  ").Error);
        Assert.Equal(EvalError.Empty, ExpressionEvaluator.Evaluate(null).Error);
        Assert.Equal(EvalError.DivisionByZero, ExpressionEvaluator.Evaluate("1/0").Error);
        Assert.Equal(EvalError.Syntax, ExpressionEvaluator.Evaluate("1+").Error);
        Assert.Equal(EvalError.Syntax, ExpressionEvaluator.Evaluate("(1+2").Error);
        Assert.Equal(EvalError.Syntax, ExpressionEvaluator.Evaluate("1..2+3").Error);
        Assert.Equal(EvalError.Syntax, ExpressionEvaluator.Evaluate("abc").Error);
        Assert.Equal(EvalError.Domain, ExpressionEvaluator.Evaluate("sqrt(-4)").Error);
        Assert.False(ExpressionEvaluator.Evaluate("1 2").Ok);
    }

    [Fact]
    public void DeepNestingDoesNotCrash()
    {
        var text = new string('(', 500) + "1" + new string(')', 500);
        Assert.False(ExpressionEvaluator.Evaluate(text).Ok);
    }

    [Fact]
    public void FormatHasNoNoise()
    {
        Assert.Equal("0.3", ExpressionEvaluator.Format(Eval("0.1+0.2")));
        Assert.Equal("1000000", ExpressionEvaluator.Format(1000000m));
        Assert.Equal("0", ExpressionEvaluator.Format(0m));
    }

    [Fact]
    public void PercentTools()
    {
        Assert.Equal(20m, PercentTools.PercentOf(10, 200));
        Assert.Equal(25m, PercentTools.WhatPercent(50, 200));
        Assert.Equal(110m, PercentTools.Increase(100, 10));
        Assert.Equal(90m, PercentTools.Decrease(100, 10));
        Assert.Null(PercentTools.WhatPercent(5, 0));
    }

    [Fact]
    public void UnitConversionsKnownValues()
    {
        Assert.Equal(1000.0, UnitConverter.Convert("length", "km", "m", 1)!.Value, 6);
        Assert.Equal(2.54, UnitConverter.Convert("length", "in", "cm", 1)!.Value, 6);
        Assert.Equal(1.609344, UnitConverter.Convert("length", "mi", "km", 1)!.Value, 6);
        Assert.Equal(2.2046, UnitConverter.Convert("mass", "kg", "lb", 1)!.Value, 3);
        Assert.Equal(100.0, UnitConverter.Convert("temperature", "c", "f", 37.77777777777778)!.Value, 6);
        Assert.Equal(212.0, UnitConverter.Convert("temperature", "c", "f", 100)!.Value, 6);
        Assert.Equal(273.15, UnitConverter.Convert("temperature", "c", "k", 0)!.Value, 6);
        Assert.Equal(1024.0, UnitConverter.Convert("data", "MB", "KB", 1)!.Value, 6);
        Assert.Equal(36.0, UnitConverter.Convert("speed", "ms", "kmh", 10)!.Value, 6);
        Assert.Equal(120.0, UnitConverter.Convert("time", "hour", "min", 2)!.Value, 6);
        Assert.Equal(10000.0, UnitConverter.Convert("area", "ha", "m2", 1)!.Value, 6);
        Assert.Equal(1000.0, UnitConverter.Convert("volume", "m3", "l", 1)!.Value, 6);
    }

    [Fact]
    public void UnitConverterRejectsUnknown()
    {
        Assert.Null(UnitConverter.Convert("length", "m", "nope", 1));
        Assert.Null(UnitConverter.Convert("nope", "m", "m", 1));
        Assert.Null(UnitConverter.Convert("length", "m", "km", double.NaN));
        Assert.Equal("1.5", UnitConverter.FormatValue(1.5));
        Assert.Equal("0", UnitConverter.FormatValue(0));
    }

    [Fact]
    public void EveryUnitHasConsistentCategory()
    {
        foreach (var category in UnitConverter.Categories)
        {
            Assert.NotEmpty(category.Units);
            foreach (var u in category.Units)
                Assert.NotNull(UnitConverter.Convert(category.Id, u.Id, category.Units[0].Id, 1));
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(64)]
    public void PasswordLength(int length) =>
        Assert.Equal(length, PasswordGenerator.Generate(length, new PasswordOptions()).Length);

    [Fact]
    public void PasswordLengthIsClamped()
    {
        Assert.Equal(PasswordGenerator.MinLength, PasswordGenerator.Generate(1, new PasswordOptions()).Length);
        Assert.Equal(PasswordGenerator.MaxLength, PasswordGenerator.Generate(1000, new PasswordOptions()).Length);
    }

    [Fact]
    public void PasswordContainsEverySelectedClass()
    {
        for (var i = 0; i < 50; i++)
        {
            var p = PasswordGenerator.Generate(8, new PasswordOptions());
            Assert.Contains(p, char.IsAsciiLetterLower);
            Assert.Contains(p, char.IsAsciiLetterUpper);
            Assert.Contains(p, char.IsAsciiDigit);
            Assert.Contains(p, c => !char.IsAsciiLetterOrDigit(c));
        }
    }

    [Fact]
    public void PasswordOnlyUsesSelectedClasses()
    {
        var p = PasswordGenerator.Generate(40, new PasswordOptions(Lowercase: false, Uppercase: false, Digits: true, Symbols: false));
        Assert.All(p, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Fact]
    public void PasswordExcludesLookAlikes()
    {
        var options = new PasswordOptions(ExcludeSimilar: true);
        for (var i = 0; i < 100; i++)
        {
            var p = PasswordGenerator.Generate(64, options);
            Assert.DoesNotContain(p, c => PasswordGenerator.Similar.Contains(c));
        }
    }

    [Fact]
    public void PasswordWithNothingSelectedIsEmpty() =>
        Assert.Equal("", PasswordGenerator.Generate(16, new PasswordOptions(false, false, false, false)));

    [Fact]
    public void DateDifferenceBasics()
    {
        var r = DateDifference.Compute(new DateTime(2026, 1, 15), new DateTime(2027, 3, 20));
        Assert.Equal(1, r.Years);
        Assert.Equal(2, r.Months);
        Assert.Equal(5, r.Days);
        Assert.Equal(429, r.TotalDays);
        Assert.False(r.Reversed);
    }

    [Fact]
    public void DateDifferenceReversedAndSameDay()
    {
        var r = DateDifference.Compute(new DateTime(2026, 3, 1), new DateTime(2026, 2, 1));
        Assert.True(r.Reversed);
        Assert.Equal(28, r.TotalDays);
        Assert.Equal(1, r.Months);
        var same = DateDifference.Compute(new DateTime(2026, 3, 1, 8, 0, 0), new DateTime(2026, 3, 1, 20, 0, 0));
        Assert.Equal(0, same.TotalDays);
    }

    [Fact]
    public void DateDifferenceMonthEndClamps()
    {
        var r = DateDifference.Compute(new DateTime(2026, 1, 31), new DateTime(2026, 2, 28));
        Assert.Equal(0, r.Years);
        Assert.Equal(1, r.Months);
        Assert.Equal(0, r.Days);
        Assert.Equal(28, r.TotalDays);
    }
}
