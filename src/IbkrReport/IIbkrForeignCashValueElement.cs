namespace IbkrToEtax.IbkrReport
{
    public interface IIbkrForeignCashValueElement
    {
        string Currency { get; }
        decimal FxRateToBase { get; }
        decimal Amount { get; }
    }
}