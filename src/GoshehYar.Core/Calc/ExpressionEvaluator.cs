using System.Globalization;
using System.Text;

namespace GoshehYar.Core.Calc;

public enum EvalError
{
    None,
    Empty,
    Syntax,
    DivisionByZero,
    Domain,
    Overflow
}

public readonly record struct EvalResult(bool Ok, decimal Value, EvalError Error)
{
    public static EvalResult Success(decimal value) => new(true, value, EvalError.None);
    public static EvalResult Fail(EvalError error) => new(false, 0m, error);
}

/// <summary>
/// Safe hand-written recursive-descent evaluator. Never throws: problems come back as <see cref="EvalResult"/> with an error code.
/// Supports + - * / (and × ÷), ^, unary minus, parentheses, percent postfix, sqrt, abs, pi and Persian digits.
/// "a + b%" means a + a*b/100, "a - b%" means a - a*b/100; "a * b%" is simply a * (b/100).
/// </summary>
public static class ExpressionEvaluator
{
    private const int MaxDepth = 100;

    public static EvalResult Evaluate(string? input)
    {
        try
        {
            var text = Normalize(input ?? string.Empty);
            if (string.IsNullOrWhiteSpace(text)) return EvalResult.Fail(EvalError.Empty);

            var parser = new Parser(text);
            var value = parser.ParseAll();
            return EvalResult.Success(RoundSignificant(value));
        }
        catch (EvalException ex)
        {
            return EvalResult.Fail(ex.Error);
        }
        catch (OverflowException)
        {
            return EvalResult.Fail(EvalError.Overflow);
        }
        catch (Exception)
        {
            return EvalResult.Fail(EvalError.Syntax);
        }
    }

    /// <summary>Plain invariant text of a value (no exponent, no trailing zeros), Western digits.</summary>
    public static string Format(decimal value)
    {
        var text = RoundSignificant(value).ToString("0.############################", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    /// <summary>Rounds to 12 significant digits to hide binary-ish noise.</summary>
    public static decimal RoundSignificant(decimal value)
    {
        if (value == 0m) return 0m;
        var magnitude = (int)Math.Floor(Math.Log10((double)Math.Abs(value)));
        var decimals = 11 - magnitude;
        if (decimals < 0) return value;
        if (decimals > 28) decimals = 28;
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>Maps Persian/Arabic digits and look-alike operators to plain ASCII.</summary>
    public static string Normalize(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            switch (ch)
            {
                case >= '۰' and <= '۹': sb.Append((char)('0' + (ch - '۰'))); break;
                case >= '٠' and <= '٩': sb.Append((char)('0' + (ch - '٠'))); break;
                case '×' or '✕' or '·': sb.Append('*'); break;
                case '÷' or '٭': sb.Append(ch == '÷' ? '/' : '*'); break;
                case '−' or '–': sb.Append('-'); break;
                case '٪': sb.Append('%'); break;
                case '٫' or ',' or '،': sb.Append('.'); break;
                case '（': sb.Append('('); break;
                case '）': sb.Append(')'); break;
                case '√': sb.Append("sqrt"); break;
                case 'π': sb.Append("pi"); break;
                default: sb.Append(ch); break;
            }
        }

        return sb.ToString();
    }

    private sealed class EvalException : Exception
    {
        public EvalException(EvalError error) => Error = error;
        public EvalError Error { get; }
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _pos;
        private int _depth;

        public Parser(string s) => _s = s;

        public decimal ParseAll()
        {
            var (value, _) = ParseExpression();
            SkipSpace();
            if (_pos < _s.Length) throw new EvalException(EvalError.Syntax);
            return value;
        }

        private void SkipSpace()
        {
            while (_pos < _s.Length && char.IsWhiteSpace(_s[_pos])) _pos++;
        }

        private char Peek()
        {
            SkipSpace();
            return _pos < _s.Length ? _s[_pos] : '\0';
        }

        // expression := term (('+' | '-') term)*
        private (decimal Value, bool Percent) ParseExpression()
        {
            if (++_depth > MaxDepth) throw new EvalException(EvalError.Syntax);
            var (left, leftPercent) = ParseTerm();
            var any = false;
            while (true)
            {
                var c = Peek();
                if (c != '+' && c != '-') break;
                _pos++;
                var (right, rightPercent) = ParseTerm();
                if (rightPercent) right = left * right; // "100 + 10%" -> 100 + 10
                left = c == '+' ? left + right : left - right;
                any = true;
            }

            _depth--;
            return (left, !any && leftPercent);
        }

        // term := unary (('*' | '/') unary)*
        private (decimal Value, bool Percent) ParseTerm()
        {
            var (left, leftPercent) = ParseUnary();
            var any = false;
            while (true)
            {
                var c = Peek();
                if (c != '*' && c != '/') break;
                _pos++;
                var (right, _) = ParseUnary();
                if (c == '*')
                {
                    left *= right;
                }
                else
                {
                    if (right == 0m) throw new EvalException(EvalError.DivisionByZero);
                    left /= right;
                }

                any = true;
            }

            return (left, !any && leftPercent);
        }

        // unary := ('-' | '+') unary | power
        private (decimal Value, bool Percent) ParseUnary()
        {
            var c = Peek();
            if (c == '-' || c == '+')
            {
                _pos++;
                if (++_depth > MaxDepth) throw new EvalException(EvalError.Syntax);
                var (v, p) = ParseUnary();
                _depth--;
                return (c == '-' ? -v : v, p);
            }

            return ParsePower();
        }

        // power := postfix ('^' unary)?   (right associative)
        private (decimal Value, bool Percent) ParsePower()
        {
            var (baseValue, percent) = ParsePostfix();
            if (Peek() == '^')
            {
                _pos++;
                if (++_depth > MaxDepth) throw new EvalException(EvalError.Syntax);
                var (exponent, _) = ParseUnary();
                _depth--;
                return (Power(baseValue, exponent), false);
            }

            return (baseValue, percent);
        }

        // postfix := primary '%'*
        private (decimal Value, bool Percent) ParsePostfix()
        {
            var value = ParsePrimary();
            var percent = false;
            while (Peek() == '%')
            {
                _pos++;
                value /= 100m;
                percent = true;
            }

            return (value, percent);
        }

        private decimal ParsePrimary()
        {
            var c = Peek();
            if (c == '(')
            {
                _pos++;
                var (v, _) = ParseExpression();
                if (Peek() != ')') throw new EvalException(EvalError.Syntax);
                _pos++;
                return v;
            }

            if (char.IsAsciiDigit(c) || c == '.') return ParseNumber();

            if (char.IsAsciiLetter(c))
            {
                var start = _pos;
                while (_pos < _s.Length && char.IsAsciiLetter(_s[_pos])) _pos++;
                var name = _s[start.._pos].ToLowerInvariant();
                if (name == "pi") return 3.14159265358979323846m;
                if (name is "sqrt" or "abs")
                {
                    if (Peek() != '(') throw new EvalException(EvalError.Syntax);
                    _pos++;
                    var (arg, _) = ParseExpression();
                    if (Peek() != ')') throw new EvalException(EvalError.Syntax);
                    _pos++;
                    if (name == "abs") return Math.Abs(arg);
                    if (arg < 0m) throw new EvalException(EvalError.Domain);
                    return (decimal)Math.Sqrt((double)arg);
                }

                throw new EvalException(EvalError.Syntax);
            }

            throw new EvalException(EvalError.Syntax);
        }

        private decimal ParseNumber()
        {
            var start = _pos;
            var dots = 0;
            var digits = 0;
            while (_pos < _s.Length && (char.IsAsciiDigit(_s[_pos]) || _s[_pos] == '.'))
            {
                if (_s[_pos] == '.') dots++; else digits++;
                _pos++;
            }

            if (dots > 1 || digits == 0) throw new EvalException(EvalError.Syntax);
            if (!decimal.TryParse(_s.AsSpan(start, _pos - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                throw new EvalException(EvalError.Overflow);
            return value;
        }

        private static decimal Power(decimal b, decimal e)
        {
            if (e == Math.Floor(e) && Math.Abs(e) <= 100m)
            {
                var n = (int)Math.Abs(e);
                var result = 1m;
                for (var i = 0; i < n; i++) result *= b;
                if (e < 0m)
                {
                    if (result == 0m) throw new EvalException(EvalError.DivisionByZero);
                    return 1m / result;
                }

                return result;
            }

            if (b < 0m) throw new EvalException(EvalError.Domain);
            if (b == 0m)
            {
                if (e < 0m) throw new EvalException(EvalError.DivisionByZero);
                return 0m;
            }

            var d = Math.Pow((double)b, (double)e);
            if (double.IsNaN(d)) throw new EvalException(EvalError.Domain);
            if (double.IsInfinity(d) || Math.Abs(d) > 7.9e28) throw new EvalException(EvalError.Overflow);
            return (decimal)d;
        }
    }
}
