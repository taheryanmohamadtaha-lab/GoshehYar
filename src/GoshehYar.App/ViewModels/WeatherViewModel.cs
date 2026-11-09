using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Weather;

namespace GoshehYar.App.ViewModels;

public sealed record ForecastItem(string Weekday, string Icon, string Condition, string Range);

public sealed class WeatherViewModel : LocalizedViewModel
{
    private static readonly TimeSpan AutoRefresh = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private WeatherSnapshot? _snapshot;
    private bool _offline;
    private bool _busy;
    private bool _disposed;
    private DateTime _lastAttemptUtc = DateTime.MinValue;
    private string _query = "";
    private string _status = "";
    private bool _showResults;
    private bool _searchingCity;

    public WeatherViewModel(AppController controller) : base(controller)
    {
        SearchCommand = new RelayCommand(() => _ = SearchAsync());
        RefreshCommand = new RelayCommand(() => _ = RefreshAsync(true));
        ChangeCityCommand = new RelayCommand(() =>
        {
            _searchingCity = true;
            OnPropertyChanged(nameof(ShowSearch));
            OnPropertyChanged(nameof(ShowWeather));
            OnPropertyChanged(nameof(ShowNoLocation));
        });

        _timer = new DispatcherTimer { Interval = AutoRefresh };
        _timer.Tick += (_, _) => _ = RefreshAsync(false);

        if (S.WeatherHasLocation)
        {
            var cached = WeatherCache.Load(WeatherService.CachePath);
            if (cached is not null && WeatherCache.IsSamePlace(cached, S.WeatherLatitude, S.WeatherLongitude)) _snapshot = cached;
            if (_snapshot is null || DateTime.UtcNow - _snapshot.FetchedAtUtc >= AutoRefresh) _ = RefreshAsync(false);
            else _lastAttemptUtc = _snapshot.FetchedAtUtc;
            _timer.Start();
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ChangeCityCommand { get; }

    public ObservableCollection<GeoPlace> Results { get; } = new();

    public ObservableCollection<ForecastItem> Forecast { get; } = new();

    public string Query
    {
        get => _query;
        set
        {
            _query = value ?? "";
            OnPropertyChanged();
        }
    }

    public bool ShowResults => _showResults && Results.Count > 0;

    public GeoPlace? SelectedPlace
    {
        get => null;
        set
        {
            if (value is null) return;
            PickPlace(value);
        }
    }

    public bool ShowSearch => !S.WeatherHasLocation || _searchingCity;
    public bool ShowNoLocation => !S.WeatherHasLocation;
    public bool ShowWeather => S.WeatherHasLocation && !_searchingCity && _snapshot is not null;

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public bool IsFahrenheit
    {
        get => S.WeatherFahrenheit;
        set
        {
            if (S.WeatherFahrenheit == value) return;
            Controller.Settings.Update(s => s.WeatherFahrenheit = value);
        }
    }

    public string CityText => string.IsNullOrWhiteSpace(S.WeatherCountry) ? S.WeatherCity : S.WeatherCity + ", " + S.WeatherCountry;

    public string Icon => WeatherCodes.Icon(_snapshot?.Code, _snapshot?.IsDay ?? true);

    public string Condition => WeatherCodes.Text(_snapshot?.Code, S.Language);

    public string Temperature => Num(TemperatureUnits.Format(_snapshot?.TemperatureC, S.WeatherFahrenheit));

    public string FeelsLike => Num(TemperatureUnits.Format(_snapshot?.FeelsLikeC, S.WeatherFahrenheit));

    public string Humidity => _snapshot?.Humidity is { } h ? Num(Math.Round(h).ToString("0") + "%") : "--";

    public string Wind => _snapshot?.WindKmh is { } w ? Fmt("wx_wind_unit", Math.Round(w).ToString("0")) : "--";

    public string TodayRange
    {
        get
        {
            var today = _snapshot?.Days.FirstOrDefault();
            if (today is null) return "";
            return Fmt("wx_today_range",
                Num(TemperatureUnits.Format(today.MinC, S.WeatherFahrenheit)),
                Num(TemperatureUnits.Format(today.MaxC, S.WeatherFahrenheit)));
        }
    }

    public string UpdatedText
    {
        get
        {
            if (_snapshot is null) return "";
            var time = Num(_snapshot.FetchedAtUtc.ToLocalTime().ToString("HH:mm"));
            return Fmt(_offline ? "wx_offline" : "wx_updated", time);
        }
    }

    private void BuildForecast()
    {
        Forecast.Clear();
        if (_snapshot is null) return;
        foreach (var day in _snapshot.Days.Skip(1).Take(5))
        {
            var name = DateNames.WeekdayName(day.Date.DayOfWeek, S.Language);
            var range = Num(TemperatureUnits.Format(day.MinC, S.WeatherFahrenheit) + " / " + TemperatureUnits.Format(day.MaxC, S.WeatherFahrenheit));
            Forecast.Add(new ForecastItem(name, WeatherCodes.Icon(day.Code), WeatherCodes.Text(day.Code, S.Language), range));
        }
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        BuildForecast();
        base.OnSettingsChanged(sender, e);
    }

    private async Task SearchAsync()
    {
        var q = _query.Trim();
        if (q.Length < 2 || _disposed) return;
        Status = this["wx_searching"];
        Results.Clear();
        _showResults = false;
        OnPropertyChanged(nameof(ShowResults));
        var found = await WeatherService.SearchAsync(q, S.Language, _cts.Token);
        if (_disposed) return;
        if (found is null)
        {
            Status = this["wx_search_failed"];
            return;
        }

        foreach (var p in found) Results.Add(p);
        _showResults = true;
        Status = found.Count == 0 ? this["wx_no_results"] : this["wx_pick"];
        OnPropertyChanged(nameof(ShowResults));
    }

    private void PickPlace(GeoPlace place)
    {
        _searchingCity = false;
        _snapshot = null;
        _offline = false;
        _showResults = false;
        Results.Clear();
        Forecast.Clear();
        _query = "";
        Status = "";
        _lastAttemptUtc = DateTime.MinValue;
        Controller.Settings.Update(s =>
        {
            s.WeatherHasLocation = true;
            s.WeatherCity = place.Name;
            s.WeatherCountry = place.Country;
            s.WeatherLatitude = place.Latitude;
            s.WeatherLongitude = place.Longitude;
        });
        _timer.Start();
        _ = RefreshAsync(false);
    }

    private async Task RefreshAsync(bool manual)
    {
        if (_disposed || _busy || !S.WeatherHasLocation) return;
        if (manual && DateTime.UtcNow - _lastAttemptUtc < Cooldown)
        {
            Status = this["wx_wait"];
            return;
        }

        _busy = true;
        _lastAttemptUtc = DateTime.UtcNow;
        Status = this["wx_refreshing"];
        try
        {
            var result = await WeatherService.FetchAsync(S.WeatherLatitude, S.WeatherLongitude, _cts.Token);
            if (_disposed) return;
            if (result is not null)
            {
                _snapshot = result;
                _offline = false;
                Status = "";
            }
            else
            {
                if (_snapshot is null)
                {
                    var cached = WeatherCache.Load(WeatherService.CachePath);
                    if (cached is not null && WeatherCache.IsSamePlace(cached, S.WeatherLatitude, S.WeatherLongitude)) _snapshot = cached;
                }

                _offline = _snapshot is not null;
                Status = this["wx_error"];
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Weather refresh failed", ex);
            Status = this["wx_error"];
        }
        finally
        {
            _busy = false;
        }

        if (_disposed) return;
        BuildForecast();
        OnPropertyChanged(string.Empty);
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _cts.Cancel();
        _cts.Dispose();
        base.Dispose();
    }
}
