using System;
using System.Data;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Titanoboa
{
    public partial class CommandHandler
    {
        /*
            Set Buy Trigger command flow:
            1- Get current user
            2- Find the stock trigger entry in transaction table
            3- Calculate stock amount (only whole stocks, based on user spending and stock price)
            4- Update spending balance, and number of stocks in transactions table
         */
        public async Task SetBuyTrigger()
        {
            CheckParams("price", "stock");

            // Get params
            var user = await databaseHelper.GetUser(username, true);
            var buyPrice = (decimal)commandParams["price"];
            var stockSymbol = commandParams["stock"].ToString();

            // Log 
            logger.LogCommand(user, command, buyPrice, stockSymbol);

            // Can't have buy price of 0
            if (buyPrice == 0)
            {
                throw new InvalidOperationException("Can't have a buy price of 0.");
            }

            var buyTrigger = await databaseHelper.GetTriggerTransaction(user, stockSymbol, "BUY_TRIGGER");
            // Make sure trigger was previously created
            var existingBuyTrigger = await databaseHelper.GetTriggerTransaction(user, stockSymbol, "BUY_TRIGGER");
            if (existingBuyTrigger == null)
            {
                throw new InvalidOperationException("Can't set BUY_TRIGGER: No existing trigger");
            }

            // Make sure the trigger hasn't already been set
            if (existingBuyTrigger.StockPrice != null || existingBuyTrigger.StockAmount != null)
            {
                throw new InvalidOperationException("Can't set BUY_TRIGGER: Trigger was already set!");
            }

            // Find amount in $$ the user wants to buy of the stock
            var buyAmountInDollars = existingBuyTrigger.BalanceChange;
            if (buyAmountInDollars <= 0)
            {
                throw new InvalidOperationException("Can't set BUY_TRIGGER: Trigger dollars amount was never set!");
            }

            // Update the transaction price
            await databaseHelper.SetTransactionStockPrice(existingBuyTrigger, buyPrice);

            // If trigger can be completed right now then do it
            var curStockPrice = await databaseHelper.GetStockPrice(user, stockSymbol);

            if(curStockPrice <= buyPrice) 
            {
                command = "SET_BUY_TRIGGER";
                await CommitBuyTrigger();
                return;
            }


            // Send new trigger to Twig
            JObject twigTrigger = new JObject();
            JObject twigParams = new JObject();
            twigTrigger.Add("usr", username);
            twigTrigger.Add("cmd", "BUY");
            twigParams.Add("stock", existingBuyTrigger.StockSymbol);
            twigParams.Add("price", buyPrice);
            twigTrigger.Add("params", twigParams);
            twigTrigger.Add("tid", logger.TransactionId);
            RabbitHelper.PushTrigger(twigTrigger);
        }
    }
}
