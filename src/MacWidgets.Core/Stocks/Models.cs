namespace MacWidgets.Core.Stocks;

public sealed record Quote(string Symbol, string Name, double Price, double PrevClose, double Change, double ChangePct, string Time, string Source);

public sealed record Intraday(string Symbol, double PrevClose, double[] Points, int ExpectedPoints, string Source);

public enum Market { CN, HK, US, Unknown }
