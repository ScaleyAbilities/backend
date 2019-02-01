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
            Buy command flow:
            1- Get current user
            2- Find the stock trigger entry in transaction table
            3- Calculate stock amount (only whole stocks, based on user spending and stock price)
            3- Update spending balance, and number of stocks in transactions table
         */
        public static void SetSellTrigger(string username, JObject commandParams) 
        {
            ParamHelper.ValidateParamsExist(commandParams, "price", "stock");

            // Unpack JObject
            var sellPrice = (decimal)commandParams["price"];
            var stockSymbol = commandParams["stock"].ToString();

            // Get users current balance
            var user = TransactionHelper.GetUser(username, true);

            //Get the existing trigger to find amount in $$ the user wants to sell of their stock
            var existingTrigger = TransactionHelper.GetTrigger(user, stockSymbol, "SELL_TRIGGER");
            var sellAmount = existingTrigger.BalanceChange;

            // Check if they have enough to sell
            var userStockAmount = TransactionHelper.GetStocks(user, stockSymbol, true);
            var numStockToSell = (int)Math.Floor(sellAmount / sellPrice);
            if (userStockAmount < numStockToSell)
            {
                throw new InvalidOperationException("Insufficient stock for SET_SELL_TRIGGER, need to choose a higher price point.");
            }

            // Subtract stocks from user account
            // any extra $$ will be refunded upon trigger point being hit / cancel trigger events
            var newUserStockAmount = userStockAmount - numStockToSell;
            TransactionHelper.UpdateStocks(user, stockSymbol, newUserStockAmount);

            TransactionHelper.UpdateTriggerTransaction(user, stockSymbol, "SELL_TRIGGER", sellPrice);
        } 
    }
}
