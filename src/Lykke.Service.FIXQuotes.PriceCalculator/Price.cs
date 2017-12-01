using System;

namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    [Serializable]
    public sealed class Price
    {
        private readonly int _nDecimals;
        private const int NDecimals = 5;

        public long Ask { get; }

        public long Bid { get; }

        public long Time { get; }

        public double Mid => (Bid + Ask) / 2f;

        public long Spread => Ask - Bid;

        public double FloatAsk => Ask * Math.Pow(10, NDecimals * -1);

        public double FloatBid => Bid * Math.Pow(10, NDecimals * -1);

        public double FloatMid => Mid * Math.Pow(10, NDecimals * -1);

        public Price(long bid, long ask, long time, int nDecimals)
        {
            Bid = bid;
            Ask = ask;
            Time = time;
            _nDecimals = nDecimals;
        }

        public Price(double bid, double ask, DateTime time) : this((long)(bid * Math.Pow(10, NDecimals)), (long)(ask * Math.Pow(10, NDecimals)), time.Ticks, NDecimals)
        {

        }


    }
}