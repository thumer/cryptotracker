using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTracker.Entities
{
    public class CryptoTransaction
    {
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public string TransactionType { get; set; }
        public string Wallet { get; set; }
        public string CoinSymbol { get; set; }
        public double Quantity { get; set; }
        public double Fee { get; set; }
        public string TransactionId { get; set; }
        public string Address { get; set; }
        public string Network { get; set; }
        public string Comment { get; set; }
    }
}
