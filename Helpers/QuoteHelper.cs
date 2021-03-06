using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace Titanoboa
{
    public static class QuoteHelper
    {
        private static string quoteApi = Environment.GetEnvironmentVariable("QUOTE_API") ?? "http://localhost:5588";

        private static ConcurrentDictionary<string, Tuple<decimal, DateTime>> quoteCache = new ConcurrentDictionary<string, Tuple<decimal, DateTime>>();

        public static async Task<decimal> GetQuote(User user, string stockSymbol, string transactionId) {
            Tuple<decimal, DateTime> cachedQuote = null;
            quoteCache.TryGetValue(stockSymbol, out cachedQuote);
            
            if (cachedQuote != null && cachedQuote.Item2.AddMinutes(1) >= DateTime.Now) {
                return cachedQuote.Item1;
            }

            // Get value from Cobra
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"{quoteApi}/quote/{user.Username}/{stockSymbol}/{transactionId}");
                response.EnsureSuccessStatusCode();
                var json = JObject.Parse(await response.Content.ReadAsStringAsync());

                var amount = (decimal)json["amount"];
                quoteCache[stockSymbol] = new Tuple<decimal, DateTime>(amount, DateTime.Now);

                return amount;
            }
        }
    }
}
