using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GoshehYar.Core.Calc;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Localization;

namespace GoshehYar.App.ViewModels;

public sealed record HistoryItem(string Expression, string Result)
{
    public string Text => Expression + " = " + Result;
}

public sealed record UnitOption(string Id, string Label);

public sealed record CategoryOption(string Id, string Label);

/// <summary>Calculator, percent tools, unit converter, password generator and date tools.</summary>
public sealed class CalcViewModel : LocalizedViewModel
{
    private const int MaxHistory = 20;

    // calculator
    private string _expression = "";
    private string _error = "";
    private string _status = "";
    private decimal? _lastValue;

    // percent
    private string _pctAPercent = "", _pctANumber = "";
    private string _pctBPart = "", _pctBWhole = "";
    private string _pctCValue = "", _pctCPercent = "";

    // units
    private string _category = "length";
    private string _fromUnit = "mm";
    private string _toUnit = "m";
    private string _unitValue = "1";
    private IReadOnlyList<UnitOption> _units = Array.Empty<UnitOption>();

    // password
    private double _pwLength = 16;
    private bool _pwLower = true, _pwUpper = true, _pwDigits = true, _pwSymbols = true, _pwSimilar = true;
    private string _password = "";

    // dates
    private DateTime? _fromDate = DateTime.Today;
    private DateTime? _toDate = DateTime.Today.AddDays(30);

    public CalcViewModel(AppController controller) : base(controller)
    {
        InputCommand = new RelayCommand(p => Input(Convert.ToString(p, CultureInfo.InvariantCulture) ?? ""));
        CopyResultCommand = new RelayCommand(() => CopyText(ResultRaw, "calc_copied"));
        ClearHistoryCommand = new RelayCommand(() => History.Clear());
        SwapUnitsCommand = new RelayCommand(SwapUnits);
        CopyUnitResultCommand = new RelayCommand(() => CopyText(UnitResultRaw, "calc_copied"));
        GeneratePasswordCommand = new RelayCommand(GeneratePassword);
        CopyPasswordCommand = new RelayCommand(() => CopyText(_password, "calc_copied"));

        Categories = UnitConverter.Categories.Select(c => new CategoryOption(c.Id, this["calc_cat_" + c.Id])).ToList();
        RebuildUnits(resetSelection: true);
        GeneratePassword();
        InitDateConverter();
    }

    public ICommand InputCommand { get; }
    public ICommand CopyResultCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand SwapUnitsCommand { get; }
    public ICommand CopyUnitResultCommand { get; }
    public ICommand GeneratePasswordCommand { get; }
    public ICommand CopyPasswordCommand { get; }

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    private void CopyText(string text, string okKey)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            System.Windows.Clipboard.SetText(text);
            Status = this[okKey];
        }
        catch (Exception ex)
        {
            Services.Logger.Error("Copy to clipboard failed", ex);
            Status = this["calc_copy_failed"];
        }
    }

    // ================================================================ calculator

    public ObservableCollection<HistoryItem> History { get; } = new();

    public HistoryItem? SelectedHistory
    {
        get => null;
        set
        {
            if (value is null) return;
            Expression = value.Expression;
        }
    }

    public string Expression
    {
        get => _expression;
        set
        {
            _expression = value ?? "";
            _error = "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultText));
            OnPropertyChanged(nameof(ErrorText));
        }
    }

    /// <summary>Live result while typing; empty when the expression is incomplete.</summary>
    public string ResultText => ResultRaw.Length == 0 ? "" : Num(ResultRaw);

    private string ResultRaw
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_expression)) return "";
            var r = ExpressionEvaluator.Evaluate(_expression);
            return r.Ok ? ExpressionEvaluator.Format(r.Value) : "";
        }
    }

    public string ErrorText => _error;

    private void Input(string key)
    {
        switch (key)
        {
            case "C":
                Expression = "";
                _lastValue = null;
                break;
            case "BACK":
                if (_expression.Length > 0) Expression = _expression[..^1];
                break;
            case "NEG":
                Expression = _expression.Length == 0 ? "-" : "-(" + _expression + ")";
                break;
            case "EQ":
                Evaluate();
                break;
            case "SQRT":
                Expression = _expression + "sqrt(";
                break;
            default:
                // After "=" a digit starts a new calculation; an operator continues with the result.
                Expression = _expression + key;
                break;
        }
    }

    /// <summary>Called by the Enter key and the "=" button.</summary>
    public void Evaluate()
    {
        if (string.IsNullOrWhiteSpace(_expression)) return;

        var r = ExpressionEvaluator.Evaluate(_expression);
        if (!r.Ok)
        {
            _error = this[r.Error switch
            {
                EvalError.DivisionByZero => "calc_err_div0",
                EvalError.Domain => "calc_err_domain",
                EvalError.Overflow => "calc_err_overflow",
                _ => "calc_err_syntax",
            }];
            OnPropertyChanged(nameof(ErrorText));
            return;
        }

        var text = ExpressionEvaluator.Format(r.Value);
        History.Insert(0, new HistoryItem(_expression, Num(text)));
        while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);

        _lastValue = r.Value;
        _expression = text;
        _error = "";
        OnPropertyChanged(nameof(Expression));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(ErrorText));
    }

    // ================================================================ percent

    private static decimal? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var r = ExpressionEvaluator.Evaluate(text);
        return r.Ok ? r.Value : null;
    }

    private string Show(decimal? value) => value is null ? "" : Num(ExpressionEvaluator.Format(value.Value));

    public string PctAPercent { get => _pctAPercent; set { _pctAPercent = value ?? ""; OnPctChanged(); } }
    public string PctANumber { get => _pctANumber; set { _pctANumber = value ?? ""; OnPctChanged(); } }
    public string PctBPart { get => _pctBPart; set { _pctBPart = value ?? ""; OnPctChanged(); } }
    public string PctBWhole { get => _pctBWhole; set { _pctBWhole = value ?? ""; OnPctChanged(); } }
    public string PctCValue { get => _pctCValue; set { _pctCValue = value ?? ""; OnPctChanged(); } }
    public string PctCPercent { get => _pctCPercent; set { _pctCPercent = value ?? ""; OnPctChanged(); } }

    private void OnPctChanged()
    {
        OnPropertyChanged(nameof(PctAResult));
        OnPropertyChanged(nameof(PctBResult));
        OnPropertyChanged(nameof(PctCIncrease));
        OnPropertyChanged(nameof(PctCDecrease));
    }

    public string PctAResult
    {
        get
        {
            var p = Parse(_pctAPercent);
            var n = Parse(_pctANumber);
            return p is null || n is null ? "" : Show(PercentTools.PercentOf(p.Value, n.Value));
        }
    }

    public string PctBResult
    {
        get
        {
            var part = Parse(_pctBPart);
            var whole = Parse(_pctBWhole);
            var v = part is null || whole is null ? null : PercentTools.WhatPercent(part.Value, whole.Value);
            return v is null ? "" : Show(Math.Round(v.Value, 6)) + "%";
        }
    }

    public string PctCIncrease
    {
        get
        {
            var v = Parse(_pctCValue);
            var p = Parse(_pctCPercent);
            return v is null || p is null ? "" : Show(PercentTools.Increase(v.Value, p.Value));
        }
    }

    public string PctCDecrease
    {
        get
        {
            var v = Parse(_pctCValue);
            var p = Parse(_pctCPercent);
            return v is null || p is null ? "" : Show(PercentTools.Decrease(v.Value, p.Value));
        }
    }

    // ================================================================ units

    public IReadOnlyList<CategoryOption> Categories { get; }

    public IReadOnlyList<UnitOption> Units => _units;

    public string Category
    {
        get => _category;
        set
        {
            // The box may report null while its list is refilled; ignore that.
            if (string.IsNullOrEmpty(value) || value == _category) return;
            _category = value;
            RebuildUnits(resetSelection: true);
            OnPropertyChanged();
        }
    }

    public string FromUnit
    {
        get => _fromUnit;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _fromUnit) return;
            _fromUnit = value;
            OnPropertyChanged();
            OnUnitResultChanged();
        }
    }

    public string ToUnit
    {
        get => _toUnit;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _toUnit) return;
            _toUnit = value;
            OnPropertyChanged();
            OnUnitResultChanged();
        }
    }

    public string UnitValue
    {
        get => _unitValue;
        set
        {
            _unitValue = value ?? "";
            OnPropertyChanged();
            OnUnitResultChanged();
        }
    }

    private void RebuildUnits(bool resetSelection)
    {
        var category = UnitConverter.Find(_category);
        if (category is null) return;

        _units = category.Units.Select(u => new UnitOption(u.Id, UnitLabel(u.Id))).ToList();
        if (resetSelection)
        {
            _fromUnit = category.Units[0].Id;
            _toUnit = category.Units[Math.Min(1, category.Units.Count - 1)].Id;
        }

        OnPropertyChanged(nameof(Units));
        OnPropertyChanged(nameof(FromUnit));
        OnPropertyChanged(nameof(ToUnit));
        OnPropertyChanged(nameof(ShowDataNote));
        OnUnitResultChanged();
    }

    private string UnitLabel(string id)
    {
        var key = "calc_u_" + id.ToLowerInvariant();
        var label = this[key];
        return label == key ? id : label;
    }

    public bool ShowDataNote => _category == "data";

    private string UnitResultRaw
    {
        get
        {
            var value = Parse(_unitValue);
            if (value is null) return "";
            var r = UnitConverter.Convert(_category, _fromUnit, _toUnit, (double)value.Value);
            return r is null ? "" : UnitConverter.FormatValue(r.Value);
        }
    }

    public string UnitResult => UnitResultRaw.Length == 0 ? "" : Num(UnitResultRaw);

    private void OnUnitResultChanged() => OnPropertyChanged(nameof(UnitResult));

    private void SwapUnits()
    {
        (_fromUnit, _toUnit) = (_toUnit, _fromUnit);
        OnPropertyChanged(nameof(FromUnit));
        OnPropertyChanged(nameof(ToUnit));
        OnUnitResultChanged();
    }

    // ================================================================ password (never saved or logged)

    public double PasswordLength
    {
        get => _pwLength;
        set
        {
            var v = Math.Clamp(Math.Round(value), PasswordGenerator.MinLength, PasswordGenerator.MaxLength);
            if (v == _pwLength) return;
            _pwLength = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PasswordLengthText));
            GeneratePassword();
        }
    }

    public string PasswordLengthText => Num(((int)_pwLength).ToString(CultureInfo.InvariantCulture));

    public bool PwLower { get => _pwLower; set { _pwLower = value; OnPropertyChanged(); GeneratePassword(); } }
    public bool PwUpper { get => _pwUpper; set { _pwUpper = value; OnPropertyChanged(); GeneratePassword(); } }
    public bool PwDigits { get => _pwDigits; set { _pwDigits = value; OnPropertyChanged(); GeneratePassword(); } }
    public bool PwSymbols { get => _pwSymbols; set { _pwSymbols = value; OnPropertyChanged(); GeneratePassword(); } }
    public bool PwSimilar { get => _pwSimilar; set { _pwSimilar = value; OnPropertyChanged(); GeneratePassword(); } }

    public string Password
    {
        get => _password;
        private set { _password = value; OnPropertyChanged(); }
    }

    public bool PasswordOptionsMissing => !(_pwLower || _pwUpper || _pwDigits || _pwSymbols);

    private void GeneratePassword()
    {
        OnPropertyChanged(nameof(PasswordOptionsMissing));
        if (PasswordOptionsMissing)
        {
            Password = "";
            return;
        }

        try
        {
            Password = PasswordGenerator.Generate((int)_pwLength, new PasswordOptions(_pwLower, _pwUpper, _pwDigits, _pwSymbols, _pwSimilar));
        }
        catch (Exception ex)
        {
            // Only the error type is logged, never a password.
            Services.Logger.Error("Password generation failed: " + ex.GetType().Name);
            Password = "";
        }
    }

    // ================================================================ dates

    public string TodayText
    {
        get
        {
            var now = DateTime.Now;
            var date = DateNames.WeekdayName(now.DayOfWeek, S.Language) + "، " + DateNames.LongDate(now, S.CalendarKind, S.Language);
            return Num(date + "  " + now.ToString("HH:mm", CultureInfo.InvariantCulture));
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set { _fromDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(DateResult)); OnPropertyChanged(nameof(DateTotal)); }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set { _toDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(DateResult)); OnPropertyChanged(nameof(DateTotal)); }
    }

    public string DateResult
    {
        get
        {
            if (_fromDate is null || _toDate is null) return this["calc_date_pick"];
            var d = DateDifference.Compute(_fromDate.Value, _toDate.Value);
            return Fmt("calc_date_result", d.Years, d.Months, d.Days);
        }
    }

    public string DateTotal
    {
        get
        {
            if (_fromDate is null || _toDate is null) return "";
            return Fmt("calc_date_total", DateDifference.Compute(_fromDate.Value, _toDate.Value).TotalDays);
        }
    }

    // ================================================================ date converter (Jalali <-> Gregorian)

    private string _jYear = "", _jMonth = "", _jDay = "";
    private string _gYear = "", _gMonth = "", _gDay = "";
    private string _yearJ = "", _yearG = "";

    private void InitDateConverter()
    {
        var now = DateTime.Today;
        var j = DateConverter.GregorianToJalali(now.Year, now.Month, now.Day);
        if (j is { } t)
        {
            _jYear = t.Year.ToString(CultureInfo.InvariantCulture);
            _jMonth = t.Month.ToString(CultureInfo.InvariantCulture);
            _jDay = t.Day.ToString(CultureInfo.InvariantCulture);
            _yearJ = _jYear;
        }

        _gYear = now.Year.ToString(CultureInfo.InvariantCulture);
        _gMonth = now.Month.ToString(CultureInfo.InvariantCulture);
        _gDay = now.Day.ToString(CultureInfo.InvariantCulture);
        _yearG = _gYear;
    }

    private void OnDateConvChanged()
    {
        OnPropertyChanged(nameof(JalaliToGregorianResult));
        OnPropertyChanged(nameof(GregorianToJalaliResult));
        OnPropertyChanged(nameof(YearJResult));
        OnPropertyChanged(nameof(YearGResult));
    }

    public string JYear { get => _jYear; set { _jYear = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }
    public string JMonth { get => _jMonth; set { _jMonth = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }
    public string JDay { get => _jDay; set { _jDay = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }
    public string GYear { get => _gYear; set { _gYear = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }
    public string GMonth { get => _gMonth; set { _gMonth = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }
    public string GDay { get => _gDay; set { _gDay = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }
    public string YearJ { get => _yearJ; set { _yearJ = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }
    public string YearG { get => _yearG; set { _yearG = value ?? ""; OnPropertyChanged(); OnDateConvChanged(); } }

    private bool Persian => S.Language == GoshehYar.Core.Settings.AppLanguage.Persian;

    private string GregorianText(DateTime d) =>
        Num(d.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)) + " — " + (Persian
            ? DateNames.GregorianMonthsFa[d.Month - 1]
            : DateNames.GregorianMonthsEn[d.Month - 1]);

    private string JalaliText((int Year, int Month, int Day) j) =>
        Num(string.Format(CultureInfo.InvariantCulture, "{0}/{1:00}/{2:00}", j.Year, j.Month, j.Day)) + " — " + (Persian
            ? DateNames.JalaliMonthsFa[j.Month - 1]
            : DateNames.JalaliMonthsEn[j.Month - 1]);

    public string JalaliToGregorianResult
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_jYear) && string.IsNullOrWhiteSpace(_jMonth) && string.IsNullOrWhiteSpace(_jDay)) return "";
            if (!DateConverter.TryParseInt(_jYear, out var y) || !DateConverter.TryParseInt(_jMonth, out var m) || !DateConverter.TryParseInt(_jDay, out var d)) return this["dc_invalid"];
            var g = DateConverter.JalaliToGregorian(y, m, d);
            if (g is null) return this["dc_invalid"];
            return Fmt("dc_result_j2g", GregorianText(g.Value), DateNames.WeekdayName(g.Value.DayOfWeek, S.Language));
        }
    }

    public string GregorianToJalaliResult
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_gYear) && string.IsNullOrWhiteSpace(_gMonth) && string.IsNullOrWhiteSpace(_gDay)) return "";
            if (!DateConverter.TryParseInt(_gYear, out var y) || !DateConverter.TryParseInt(_gMonth, out var m) || !DateConverter.TryParseInt(_gDay, out var d)) return this["dc_invalid"];
            var j = DateConverter.GregorianToJalali(y, m, d);
            if (j is null) return this["dc_invalid"];
            var weekday = new DateTime(y, m, d).DayOfWeek;
            return Fmt("dc_result_g2j", JalaliText(j.Value), DateNames.WeekdayName(weekday, S.Language));
        }
    }

    public string YearJResult
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_yearJ)) return "";
            if (!DateConverter.TryParseInt(_yearJ, out var y)) return this["dc_invalid"];
            var r = DateConverter.JalaliYearToGregorian(y);
            if (r is null) return this["dc_invalid"];
            return Fmt("dc_year_j2g", Num(y.ToString(CultureInfo.InvariantCulture)),
                Num(r.Value.Start.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                Num(r.Value.End.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)),
                DateConverter.IsJalaliLeap(y) ? this["dc_leap"] : "");
        }
    }

    public string YearGResult
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_yearG)) return "";
            if (!DateConverter.TryParseInt(_yearG, out var y)) return this["dc_invalid"];
            var r = DateConverter.GregorianYearToJalali(y);
            if (r is null) return this["dc_invalid"];
            string F((int Year, int Month, int Day) j) =>
                Num(string.Format(CultureInfo.InvariantCulture, "{0}/{1:00}/{2:00}", j.Year, j.Month, j.Day));
            return Fmt("dc_year_g2j", Num(y.ToString(CultureInfo.InvariantCulture)), F(r.Value.Start), F(r.Value.End));
        }
    }

    // ================================================================ bank / finance

    private string _finPrincipal = "100000000", _finRate = "23", _finDays = "30", _finYears = "1";
    private int _finBasis = 365, _finFreq = 12;
    private string _rateAnnual = "23", _rateDaily = "", _rateMonthly = "";
    private string _savInitial = "0", _savDeposit = "10000000", _savRate = "23", _savMonths = "12";
    private string _loanAmount = "500000000", _loanRate = "23", _loanMonths = "24";
    private IReadOnlyList<OptionItem<int>>? _basisOptions, _freqOptions;

    public IReadOnlyList<OptionItem<int>> BasisOptions => _basisOptions ??= new[]
    {
        new OptionItem<int>(365, this["fin_basis_365"]),
        new OptionItem<int>(360, this["fin_basis_360"]),
    };

    public IReadOnlyList<OptionItem<int>> FreqOptions => _freqOptions ??= new[]
    {
        new OptionItem<int>(1, this["fin_freq_1"]),
        new OptionItem<int>(4, this["fin_freq_4"]),
        new OptionItem<int>(12, this["fin_freq_12"]),
        new OptionItem<int>(365, this["fin_freq_365"]),
    };

    private static decimal? ParseMoney(string text) =>
        Parse((text ?? "").Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", ""));

    private string Money(decimal? v) =>
        v is null ? "" : Num(Math.Round(v.Value, 2, MidpointRounding.AwayFromZero).ToString("#,##0.##", CultureInfo.InvariantCulture));

    private string Pct(decimal? v) =>
        v is null ? "" : Num(Math.Round(v.Value, 4, MidpointRounding.AwayFromZero).ToString("0.####", CultureInfo.InvariantCulture));

    private string Line(string key, string value) => value.Length == 0 ? "" : Fmt(key, value);

    private void NotifySimple()
    {
        OnPropertyChanged(nameof(SimpleProfit));
        OnPropertyChanged(nameof(SimpleTotal));
        OnPropertyChanged(nameof(SimpleDay));
        OnPropertyChanged(nameof(SimpleMonth));
        OnPropertyChanged(nameof(SimpleYear));
        OnPropertyChanged(nameof(CompoundTotal));
        OnPropertyChanged(nameof(CompoundProfit));
        OnPropertyChanged(nameof(CompoundEffective));
    }

    public string FinPrincipal { get => _finPrincipal; set { _finPrincipal = value ?? ""; OnPropertyChanged(); NotifySimple(); } }
    public string FinRate { get => _finRate; set { _finRate = value ?? ""; OnPropertyChanged(); NotifySimple(); } }
    public string FinDays { get => _finDays; set { _finDays = value ?? ""; OnPropertyChanged(); NotifySimple(); } }
    public string FinYears { get => _finYears; set { _finYears = value ?? ""; OnPropertyChanged(); NotifySimple(); } }

    public int FinBasis
    {
        get => _finBasis;
        set { if (value <= 0) return; _finBasis = value; OnPropertyChanged(); NotifySimple(); OnRatesChanged(); }
    }

    public int FinFreq
    {
        get => _finFreq;
        set { if (value <= 0) return; _finFreq = value; OnPropertyChanged(); NotifySimple(); }
    }

    private decimal? SimpleInterestValue()
    {
        var p = ParseMoney(_finPrincipal);
        var r = ParseMoney(_finRate);
        var d = ParseMoney(_finDays);
        return p is null || r is null || d is null ? null : FinanceTools.SimpleInterest(p.Value, r.Value, d.Value, _finBasis);
    }

    public string SimpleProfit => Line("fin_simple_profit", Money(SimpleInterestValue()));

    public string SimpleTotal
    {
        get
        {
            var p = ParseMoney(_finPrincipal);
            var i = SimpleInterestValue();
            return p is null || i is null ? "" : Line("fin_simple_total", Money(p.Value + i.Value));
        }
    }

    private decimal? InterestForDays(decimal days)
    {
        var p = ParseMoney(_finPrincipal);
        var r = ParseMoney(_finRate);
        return p is null || r is null ? null : FinanceTools.SimpleInterest(p.Value, r.Value, days, _finBasis);
    }

    public string SimpleDay => Line("fin_simple_day", Money(InterestForDays(1)));
    public string SimpleMonth => Line("fin_simple_month", Money(InterestForDays(_finBasis / 12m)));
    public string SimpleYear => Line("fin_simple_year", Money(InterestForDays(_finBasis)));

    private decimal? CompoundValue()
    {
        var p = ParseMoney(_finPrincipal);
        var r = ParseMoney(_finRate);
        var y = ParseMoney(_finYears);
        return p is null || r is null || y is null ? null : FinanceTools.CompoundFinal(p.Value, r.Value, y.Value, _finFreq);
    }

    public string CompoundTotal => Line("fin_compound_total", Money(CompoundValue()));

    public string CompoundProfit
    {
        get
        {
            var p = ParseMoney(_finPrincipal);
            var total = CompoundValue();
            return p is null || total is null ? "" : Line("fin_compound_profit", Money(total.Value - p.Value));
        }
    }

    public string CompoundEffective
    {
        get
        {
            var r = ParseMoney(_finRate);
            return r is null ? "" : Line("fin_compound_effective", Pct(FinanceTools.EffectiveAnnualPercent(r.Value, _finFreq)));
        }
    }

    // ---- rate converter

    private void OnRatesChanged()
    {
        OnPropertyChanged(nameof(RatesFromAnnual));
        OnPropertyChanged(nameof(RatesFromDaily));
        OnPropertyChanged(nameof(RatesFromMonthly));
    }

    public string RateAnnual { get => _rateAnnual; set { _rateAnnual = value ?? ""; OnPropertyChanged(); OnRatesChanged(); } }
    public string RateDaily { get => _rateDaily; set { _rateDaily = value ?? ""; OnPropertyChanged(); OnRatesChanged(); } }
    public string RateMonthly { get => _rateMonthly; set { _rateMonthly = value ?? ""; OnPropertyChanged(); OnRatesChanged(); } }

    public string RatesFromAnnual
    {
        get
        {
            var a = ParseMoney(_rateAnnual);
            if (a is null) return "";
            var d = FinanceTools.DailyPercent(a.Value, _finBasis);
            var m = FinanceTools.MonthlyPercent(a.Value);
            return d is null || m is null ? "" : Fmt("fin_rates_from_annual", Pct(d), Pct(m));
        }
    }

    public string RatesFromDaily
    {
        get
        {
            var d = ParseMoney(_rateDaily);
            return d is null ? "" : Line("fin_rates_from_daily", Pct(FinanceTools.AnnualFromDailyPercent(d.Value, _finBasis)));
        }
    }

    public string RatesFromMonthly
    {
        get
        {
            var m = ParseMoney(_rateMonthly);
            return m is null ? "" : Line("fin_rates_from_monthly", Pct(FinanceTools.AnnualFromMonthlyPercent(m.Value)));
        }
    }

    // ---- savings

    private void OnSavingsChanged()
    {
        OnPropertyChanged(nameof(SavingsTotal));
        OnPropertyChanged(nameof(SavingsPaid));
        OnPropertyChanged(nameof(SavingsProfit));
    }

    public string SavInitial { get => _savInitial; set { _savInitial = value ?? ""; OnPropertyChanged(); OnSavingsChanged(); } }
    public string SavDeposit { get => _savDeposit; set { _savDeposit = value ?? ""; OnPropertyChanged(); OnSavingsChanged(); } }
    public string SavRate { get => _savRate; set { _savRate = value ?? ""; OnPropertyChanged(); OnSavingsChanged(); } }
    public string SavMonths { get => _savMonths; set { _savMonths = value ?? ""; OnPropertyChanged(); OnSavingsChanged(); } }

    private decimal? SavingsValue()
    {
        var i = ParseMoney(_savInitial);
        var d = ParseMoney(_savDeposit);
        var r = ParseMoney(_savRate);
        var m = ParseMoney(_savMonths);
        if (i is null || d is null || r is null || m is null || m.Value != Math.Floor(m.Value) || m.Value > 1200) return null;
        return FinanceTools.SavingsFinal(i.Value, d.Value, r.Value, (int)m.Value);
    }

    private decimal? SavingsPaidValue()
    {
        var i = ParseMoney(_savInitial);
        var d = ParseMoney(_savDeposit);
        var m = ParseMoney(_savMonths);
        return i is null || d is null || m is null ? null : i.Value + d.Value * m.Value;
    }

    public string SavingsTotal => Line("fin_savings_total", Money(SavingsValue()));
    public string SavingsPaid => Line("fin_savings_paid", Money(SavingsPaidValue()));

    public string SavingsProfit
    {
        get
        {
            var total = SavingsValue();
            var paid = SavingsPaidValue();
            return total is null || paid is null ? "" : Line("fin_savings_profit", Money(total.Value - paid.Value));
        }
    }

    // ---- loan

    private void OnLoanChanged()
    {
        OnPropertyChanged(nameof(LoanPaymentText));
        OnPropertyChanged(nameof(LoanTotalText));
        OnPropertyChanged(nameof(LoanInterestText));
    }

    public string LoanAmount { get => _loanAmount; set { _loanAmount = value ?? ""; OnPropertyChanged(); OnLoanChanged(); } }
    public string LoanRate { get => _loanRate; set { _loanRate = value ?? ""; OnPropertyChanged(); OnLoanChanged(); } }
    public string LoanMonths { get => _loanMonths; set { _loanMonths = value ?? ""; OnPropertyChanged(); OnLoanChanged(); } }

    private decimal? LoanPaymentValue()
    {
        var a = ParseMoney(_loanAmount);
        var r = ParseMoney(_loanRate);
        var m = ParseMoney(_loanMonths);
        if (a is null || r is null || m is null || m.Value != Math.Floor(m.Value) || m.Value > 1200) return null;
        return FinanceTools.LoanPayment(a.Value, r.Value, (int)m.Value);
    }

    public string LoanPaymentText => Line("fin_loan_payment", Money(LoanPaymentValue()));

    public string LoanTotalText
    {
        get
        {
            var pay = LoanPaymentValue();
            var m = ParseMoney(_loanMonths);
            return pay is null || m is null ? "" : Line("fin_loan_total", Money(pay.Value * m.Value));
        }
    }

    public string LoanInterestText
    {
        get
        {
            var pay = LoanPaymentValue();
            var m = ParseMoney(_loanMonths);
            var a = ParseMoney(_loanAmount);
            return pay is null || m is null || a is null ? "" : Line("fin_loan_interest", Money(pay.Value * m.Value - a.Value));
        }
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        _basisOptions = null;
        _freqOptions = null;
        base.OnSettingsChanged(sender, e);
    }
}
