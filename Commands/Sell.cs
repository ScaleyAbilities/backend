using System;
using System.Data;
using MySql.Data;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace Titanoboa
{
    public static partial class Commands
    {
        /*
         * - check quote server for current stock price
         * - check stock data table for amount of stocks the user owns
         * - calculate if user has enough to sell, stock amount * current price > selling price
         * - update transaction server
         */
        public static void Sell(string userid, JObject commandParams)
        {
            ParamHelper.ValidateParamsExist(commandParams, "amount", "stockSymbol");

            var sellAmount = (decimal)commandParams["amount"];
            var stockSymbol = commandParams["stockSymbol"].ToString();

            // Get current stock price
            var stockPrice = TransactionHelper.GetStockPrice(stockSymbol);

            // Get amount of stocks user owns.
            var stockAmount = TransactionHelper.GetStocks(userid, stockSymbol);

            // Check that user has enough stocks to sell.
            if (sellAmount > stockAmount * stockPrice)
            {
                throw new InvalidOperationException(
                    $"Insufficient stocks ({stockAmount}) at selling price ({sellAmount}), current stock price: {stockPrice}"
                );
            }

            // Set balance change
            var balanceChange = stockAmount * stockPrice;

            // Set NEGATIVE stockAmount (to remove from stocks table in COMMIT_SELL)
            stockAmount = -stockAmount;

            // TODO: 0 is temporary, we should remove balance from transactions
            TransactionHelper.AddTransaction(userid, 0, stockSymbol, "SELL", balanceChange, stockAmount, true);
        } 
    }
}