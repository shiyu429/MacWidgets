using System.Windows.Threading;
using MacWidgets.Core.Stocks;
using MacWidgets.Data;

namespace MacWidgets.Services;

/// <summary>所有股票组件共享的行情服务：合并代码批量请求、交易时段感知、失败退避、本地缓存。</summary>
public static class StockHub
{
    public static event Action? Updated;

    private static readonly Dictionary<object, IReadOnlyList<string>> _regs = [];
    private static readonly QuoteClient _client = new();
    private static DispatcherTimer? _timer;
    private static DateTime _nextQuotes = DateTime.MinValue;
    private static readonly Dictionary<string, DateTime> _nextIntraday = [];
    private static bool _busy;
    private static int _backoff = 15;

    public static DateTime? LastUpdate => Store.Cache.QuotesAt;
    public static string? LastSource => _client.LastSource;
    public static string? LastError { get; private set; }

    public static Quote? GetQuote(string sym) => Store.Cache.Quotes.TryGetValue(sym, out var q) ? q : null;
    public static Intraday? GetIntraday(string sym) => Store.Cache.Intraday.TryGetValue(sym, out var i) ? i : null;

    public static void Start()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (_, e) =>
        {
            if (e.IsAvailable) _nextQuotes = DateTime.MinValue;
        };
    }

    public static void Register(object owner, IReadOnlyList<string> symbols)
    {
        _regs[owner] = symbols;
        _nextQuotes = DateTime.MinValue;
    }

    public static void Unregister(object owner) => _regs.Remove(owner);

    public static void RefreshNow()
    {
        _nextQuotes = DateTime.MinValue;
        _nextIntraday.Clear();
    }

    private static List<string> AllSymbols() => _regs.Values.SelectMany(s => s).Distinct().ToList();

    private static bool AnyTrading(IEnumerable<string> syms)
    {
        var bj = TradingCalendar.BeijingNow();
        return syms.Any(s => TradingCalendar.IsTrading(Symbols.MarketOf(s), bj));
    }

    private static async Task TickAsync()
    {
        if (_busy) return;
        var syms = AllSymbols();
        if (syms.Count == 0) return;
        var now = DateTime.Now;
        _busy = true;
        try
        {
            if (now >= _nextQuotes)
            {
                try
                {
                    var quotes = await _client.GetQuotesAsync(syms);
                    foreach (var q in quotes) Store.Cache.Quotes[q.Symbol] = q;
                    Store.Cache.QuotesAt = DateTime.Now;
                    LastError = null;
                    _backoff = 15;
                    int interval = AnyTrading(syms) ? Math.Max(5, Store.Settings.StockRefreshSeconds) : 600;
                    _nextQuotes = DateTime.Now.AddSeconds(interval);
                    Store.SaveCacheSoon();
                    Updated?.Invoke();
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    _nextQuotes = DateTime.Now.AddSeconds(_backoff);
                    _backoff = Math.Min(_backoff * 2, 300);
                    Updated?.Invoke();
                }
            }

            foreach (var s in syms)
            {
                if (_nextIntraday.TryGetValue(s, out var due) && DateTime.Now < due) continue;
                var prev = GetQuote(s)?.PrevClose ?? double.NaN;
                var intra = await _client.GetIntradayAsync(s, prev);
                bool trading = TradingCalendar.IsTrading(Symbols.MarketOf(s), TradingCalendar.BeijingNow());
                _nextIntraday[s] = DateTime.Now.AddSeconds(intra == null ? 120 : trading ? 60 : 1800);
                if (intra != null)
                {
                    if (double.IsNaN(intra.PrevClose) && !double.IsNaN(prev)) intra = intra with { PrevClose = prev };
                    Store.Cache.Intraday[s] = intra;
                    Store.SaveCacheSoon();
                    Updated?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            Store.Log("StockHub: " + ex);
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>编辑面板里校验代码用。</summary>
    public static Task<List<Quote>> ValidateAsync(IReadOnlyCollection<string> syms) => _client.GetQuotesAsync(syms);
}
