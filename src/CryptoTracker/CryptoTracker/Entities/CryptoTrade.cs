using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTracker.Entities
{

    public class CryptoTrade
    {
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public string CoinSymbolFrom { get; set; }
        public string CoinSymbolTo { get; set; }
        public double Price { get; set; }
        public double Quantity { get; set; }
        public string Wallet { get; set; }
        public double Fee { get; set; }
        public string Comment { get; set; }
    }
}
