using System.Linq;
using System.Security.AccessControl;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace IbkrToEtax.IbkrReport
{
    public class IbkrFlexReport
    {
        private ILogger _logger;
        public string AccountId { get; }
        public string AccountName { get; }
        private DateTime? _accountFundedDate;
        public DateTime StartDate { get; }
        public DateTime EndDate { get; }
        public int TaxYear { get; }
        public List<IbkrOpenPosition> OpenPositions { get; }
        public List<IbkrTrade> Trades { get; }
        public List<IbkrCashTransaction> DividendList { get; }
        public List<IbkrCashTransaction> WithholdingTaxList { get; }
        public List<IbkrEquitySummary> EquitySummaryList { get; }
        public List<IbkrCashTransaction> DepositWithdrawalList { get; }
        public List<IbkrCashTransaction> FeesList { get; }
        public List<IbkrFifoPerformanceSummary> FifoTransactions { get; }
        public List<IbkrSecurityInfo> SecurityInfoList { get; }
        public List<IbkrSummaryPerPosition> SummaryPerPositionList { get; } = [];
        public string Canton { get; }
        public string BaseCurrency { get; }

        public IbkrFlexReport(XDocument flexDocument, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<IbkrFlexReport>();

            // load list of flexStatement elements
            var flexStatements = flexDocument.Descendants("FlexStatement").ToList() ?? throw new Exception("No FlexStatement elements found in XML.");

            // check if there is more than one flexStatement (setting in IBKR export)
            if (flexStatements.Count > 1)
            {
                throw new Exception("Please deselect 'BreakOutByDay' in the IBKR Flex-Report export settings. Multiple FlexStatement elements found.");
            }

            var flexStatement = flexStatements.First();

            // Extract account ID from XML
            var accountInfo = flexStatement.Descendants("AccountInformation").FirstOrDefault();
            AccountId = accountInfo?.Attribute("accountId")?.Value ?? "";
            AccountName = accountInfo?.Attribute("name")?.Value ?? "";

            // Extract date range
            StartDate = DateTime.Parse(flexStatement.Attribute("fromDate")?.Value ?? DateTime.MinValue.ToString());
            EndDate = DateTime.Parse(flexStatement.Attribute("toDate")?.Value ?? DateTime.MinValue.ToString());
            TaxYear = EndDate.Year;

            // Extract account opened date
            _accountFundedDate = DateTime.Parse(accountInfo?.Attribute("dateFunded")?.Value ?? DateTime.MinValue.ToString());

            // Extract base currency
            BaseCurrency = accountInfo?.Attribute("currency")?.Value ?? "";

            // Extract canton from state attribute (format: "CH-ZH")
            string state = accountInfo?.Attribute("state")?.Value ?? "";

            // Extract "ZH" from "CH-ZH"
            Canton = state?.StartsWith("CH-") ?? false
                ? state[3..]
                : "";

            OpenPositions = flexStatement.Element("OpenPositions")?
                .Elements("OpenPosition")
                .Select(op => new IbkrOpenPosition(op))
                .Where(op => op.LevelOfDetail == "SUMMARY")
                .ToList() ?? [];
            _logger.LogInformation("Loaded {Count} open positions from XML", OpenPositions.Count);

            Trades = flexStatement.Element("Trades")?
                .Elements("Trade")
                .Select(t => new IbkrTrade(t))
                .ToList() ?? [];
            _logger.LogInformation("Loaded {Count} trades from XML", Trades.Count);

            var cashTransactions = flexStatement.Element("CashTransactions")?
                .Elements("CashTransaction")
                .Select(ct => new IbkrCashTransaction(ct))
                .ToList() ?? [];
            _logger.LogInformation("Loaded {Count} cash transactions from XML", cashTransactions.Count);

            EquitySummaryList = flexStatement.Element("EquitySummaryInBase")?
                .Elements("EquitySummaryByReportDateInBase")
                .Select(es => new IbkrEquitySummary(es))
                .ToList() ?? [];
            _logger.LogInformation("Loaded {Count} equity summary entries from XML", EquitySummaryList.Count);

            FifoTransactions = flexStatement.Element("FIFOPerformanceSummaryInBase")?
                .Elements("FIFOPerformanceSummaryUnderlying")
                .Select(ft => new IbkrFifoPerformanceSummary(ft))
                .ToList() ?? [];
            _logger.LogInformation("Loaded {Count} FIFO performance summary entries from XML", FifoTransactions.Count);

            SecurityInfoList = flexStatement.Element("SecuritiesInfo")?
                .Elements("SecurityInfo")
                .Select(si => new IbkrSecurityInfo(si))
                .ToList() ?? [];
            _logger.LogInformation("Loaded {Count} security info entries from XML", SecurityInfoList.Count);

            // Split CashTransactions by types
            DividendList = [.. cashTransactions.Where(ct => ct.Type == "Dividends" && ct.LevelOfDetail == "DETAIL")];
            WithholdingTaxList = [.. cashTransactions.Where(ct => ct.Type == "Withholding Tax" && ct.LevelOfDetail == "SUMMARY")];
            DepositWithdrawalList = [.. cashTransactions.Where(ct => ct.Type == "Deposits/Withdrawals")];
            FeesList = [.. cashTransactions.Where(ct => ct.Type == "Other Fees")];


            // Create summary per position
            SummarizePerPosition();

            // Validate input data
            ValidateInputData();
        }

        private void ValidateInputData()
        {
            bool isValid = true;
            // check that there is a account id in the xml
            if (string.IsNullOrEmpty(AccountId))
            {
                _logger.LogError("Account ID not found in XML");
                isValid = false;
            }

            // Validate that base currency is CHF
            if (string.IsNullOrEmpty(BaseCurrency))
            {
                _logger.LogError("Base currency not found in account information");
                isValid = false;
            }

            if (string.IsNullOrEmpty(Canton))
            {
                _logger.LogWarning("Canton not found in account information. Please ensure your IBKR account is configured with the correct state.");
                isValid = false;
            }

            if (BaseCurrency != "CHF")
            {
                _logger.LogError("Base currency must be CHF. Current base currency is {BaseCurrency}", BaseCurrency);
                _logger.LogError("Please ensure your IBKR account is configured with CHF as the base currency");
                isValid = false;
            }

            // Validate date range (max 1 year)
            if (Math.Abs((StartDate - EndDate).TotalDays) > 365)
            {
                _logger.LogError("Date range exceeds one year: From {StartDate} to {EndDate}", StartDate.ToShortDateString(), EndDate.ToShortDateString());
                isValid = false;
            }

            // Validate date range (min 1 day)
            if (Math.Abs((StartDate - EndDate).TotalDays) < 1)
            {
                _logger.LogError("Date range is less than one day: From {StartDate} to {EndDate}", StartDate.ToShortDateString(), EndDate.ToShortDateString());
                isValid = false;
            }

            // Warn if end date is not Dec 31st
            if (EndDate.Month != 12 || EndDate.Day != 31)
            {
                _logger.LogWarning("End date {EndDate} should be December 31st. Please verify if this is correct for your tax declaration!", EndDate.ToShortDateString());
            }

            bool isFirstYearOfAccount = _accountFundedDate.HasValue && _accountFundedDate?.Year == StartDate.Year;

            if (isFirstYearOfAccount && _accountFundedDate?.Year == StartDate.Year && Math.Abs((StartDate - _accountFundedDate.Value).TotalDays) > 7)
            {
                _logger.LogError("Start date {StartDate} should be withing 7 days of account opening date {accountOpenDate}", StartDate.ToShortDateString(), _accountFundedDate?.ToShortDateString());
                isValid = false;
            }

            // check start date is after account opening date and should be january 1st
            if (!isFirstYearOfAccount && StartDate.Month != 1 && StartDate.Day != 1)
            {
                _logger.LogError("Start date {StartDate} should be January 1st! Please check your export settings.", StartDate.ToShortDateString());
                isValid = false;
            }

            // check start and end date in same year
            if (StartDate.Year != EndDate.Year)
            {
                _logger.LogError("Start date {StartDate} and end date {EndDate} are not in the same year!", StartDate.ToShortDateString(), EndDate.ToShortDateString());
                isValid = false;
            }

            if (SecurityInfoList.Count == 0)
            {
                _logger.LogError("SecurityInfo list is empty, please include \"Financial Instrument Information\" in your IBKR Flex Report export settings.");
                isValid = false;
            }

            if (Trades.Count == 0)
            {
                _logger.LogWarning("No trades found in the report. Please check your statement if this is ok.");
                _logger.LogWarning("-> Otherwise, please check if your IBKR Flex Report export settings include \"Trades\".");
            }

            if (DividendList.Count == 0)
            {
                _logger.LogWarning("No dividends found in the report. Please check your statement if this is ok.");
                _logger.LogWarning("-> If you do have dividend entries in your statement but they are not showing up in the report, please check that your IBKR Flex Report export settings include \"Cash Transactions\" with type 'Dividends'.");
            }

            if (WithholdingTaxList.Count == 0)
            {
                _logger.LogWarning("No withholding tax entries found in the report. Please check your statement if this is ok.");
                _logger.LogWarning("-> If you do have withholding tax entries in your statement but they are not showing up in the report, please check that your IBKR Flex Report export settings include \"Cash Transactions\" with type 'Withholding Tax'");
            }

            if (!isValid)
            {
                throw new Exception("Input data validation failed.");
            }
        }

        public void PrintDataLoadSummary()
        {
            _logger.LogInformation("Loaded IBKR data: {PositionCount} positions, {TradeCount} trades, {DividendCount} dividends, {WithholdingTaxCount} withholding tax entries",
                OpenPositions.Count, Trades.Count, DividendList.Count, WithholdingTaxList.Count);

            _logger.LogInformation("Account: {AccountId} - {AccountName}", AccountId, AccountName);

        }


        public decimal GetTotalTaxValue()
        {
            var cashValue = EquitySummaryList.OrderByDescending(e => e.ReportDate).First().Cash;
            var securitiesValue = OpenPositions.Sum(op => op.GetPositionValue(_logger));
            return cashValue + securitiesValue;
        }

        private void SummarizePerPosition()
        {
            foreach (var si in SecurityInfoList)
            {
                var isin = si.Isin;
                var openPosition = OpenPositions.FirstOrDefault(op => op.Isin == isin);
                var trades = Trades.Where(t => t.Isin == isin).ToList();
                var dividends = DividendList.Where(d => d.Isin == isin).ToList();
                var withholdingTaxes = WithholdingTaxList.Where(wt => wt.Isin == isin).ToList();
                var fifoTransactions = FifoTransactions.Where(ft => ft.Isin == isin).ToList();

                var summary = new IbkrSummaryPerPosition(
                    isin, openPosition, trades, dividends, withholdingTaxes, fifoTransactions, si
                );
                SummaryPerPositionList.Add(summary);
            }
        }
    }
}