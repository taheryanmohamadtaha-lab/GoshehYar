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
}
