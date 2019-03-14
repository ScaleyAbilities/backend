using System;
using System.Data;
using Newtonsoft.Json.Linq;

namespace Titanoboa
{
    public static partial class Commands
    {
        public static void CommitSellTrigger(string username, JObject commandParams)
        {
            // Sanity check
            ParamHelper.ValidateParamsExist(commandParams, "price", "stock");

            // Get params
            var user = TransactionHelper.GetUser(username, true);
            var committedSellPrice = (decimal)commandParams["price"];
            var stockSymbol = commandParams["stock"].ToString();

            // Log command
            Program.Logger.LogCommand(user, committedSellPrice, stockSymbol);

            // Can't have sell price of 0
            if (committedSellPrice == 0)
            {
                throw new InvalidOperationException("Can't have a sell price of 0.");
            }

            // Get the existing trigger transaction to be committed
            var existingSellTrigger = TransactionHelper.GetTriggerTransaction(user, stockSymbol, "SELL_TRIGGER");
            if (existingSellTrigger == null)
            {
                throw new InvalidOperationException("Can't commit SELL_TRIGGER: Trigger may have been cancelled!");
            }

            // Make sure that the max amount of stocks to be sold was set
            var numStocksToSell = existingSellTrigger.StockAmount ?? 0;
            if (numStocksToSell <= 0)
            {
                throw new InvalidOperationException("Can't commit sell of less than 1 stock.");
            }

            // Double check that the trigger worked properly
            var minSellPrice = existingSellTrigger.StockPrice;
            if(minSellPrice > committedSellPrice)
            {
                throw new InvalidProgramException("Program Error! Trigger sold for less than the min price");
            }

            // Calculate + update new user balance
            var moneyMade = committedSellPrice * numStocksToSell;
            var newUserBalance = user.Balance + moneyMade;
            TransactionHelper.UpdateUserBalance(ref user, moneyMade);
    
            // Set transaction StockAmount and StockPrice, mark as completed
            TransactionHelper.SetTransactionStockPrice(ref existingSellTrigger, committedSellPrice);
            TransactionHelper.SetTransactionBalanceChange(ref existingSellTrigger, moneyMade); 
            TransactionHelper.CommitTransaction(ref existingSellTrigger);      
        }
    }
}