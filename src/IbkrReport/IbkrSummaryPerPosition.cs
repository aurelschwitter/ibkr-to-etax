namespace IbkrToEtax.IbkrReport
{
    public class IbkrSummaryPerPosition(string isin, IbkrOpenPosition? openPosition, List<IbkrTrade> trades, List<IbkrCashTransaction> dividends, List<IbkrCashTransaction> withholdingTaxes, List<IbkrFifoPerformanceSummary> fifoTransactions, IbkrSecurityInfo si)
    {
        public string Isin { get; } = isin;
        public IbkrOpenPosition? OpenPosition { get; } = openPosition;
        public List<IbkrTrade> Trades { get; } = trades;
        public List<IbkrCashTransaction> DividendList { get; } = dividends;
        public List<IbkrCashTransaction> WithholdingTaxList { get; } = withholdingTaxes;
        public List<IbkrFifoPerformanceSummary> FifoTransactions { get; } = fifoTransactions;
        public IbkrSecurityInfo SecurityInfo { get; } = si;
    }
}