﻿using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace Titanoboa
{
    class Program
    {
        internal static readonly string ServerName = Environment.GetEnvironmentVariable("SERVER_NAME") ?? "Titanoboa";
        internal static Logger Logger = null;
        internal static string CurrentCommand = null;

        static void RunCommands(JObject json)
        {
            try
            {
                ParamHelper.ValidateParamsExist(json, "cmd", "usr");
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error in Queue JSON: {ex.Message}");
                return;
            }

            CurrentCommand = json["cmd"].ToString().ToUpper();
            string username = json["usr"].ToString();
            JObject commandParams = (JObject)json["params"];

            try
            {
                // Set up a logger for this unit of work
                Logger = new Logger();
            }
            catch (MySqlException ex)
            {
                Console.Error.WriteLine($"Unable to create logger due to SQL error: {ex.Message}");
                return;
            }

            using (var transaction = SqlHelper.StartTransaction())
            {
                string error = null;
                try
                {
                    switch (CurrentCommand)
                    {
                        case "QUOTE":
                            Commands.Quote(username, commandParams);
                            break;
                        case "ADD":
                            Commands.Add(username, commandParams);
                            break;
                        case "BUY":
                            Commands.Buy(username, commandParams);
                            break;
                        case "COMMIT_BUY":
                            Commands.CommitBuy(username);
                            break;
                        case "CANCEL_BUY":
                            Commands.CancelBuy(username);
                            break;
                        case "SELL":
                            Commands.Sell(username, commandParams);
                            break;
                        case "COMMIT_SELL":
                            Commands.CommitSell(username);
                            break;
                        case "CANCEL_SELL":
                            Commands.CancelSell(username);
                            break;
                        case "SET_BUY_AMOUNT":
                            Commands.SetBuyAmount(username, commandParams);
                            break;
                        case "SET_BUY_TRIGGER":
                            Commands.SetBuyTrigger(username, commandParams);
                            break;
                        case "CANCEL_SET_BUY":
                            Commands.CancelSetBuy(username, commandParams);
                            break;
                        case "COMMIT_BUY_TRIGGER":
                            Commands.CommitBuyTrigger(username, commandParams);
                            break;
                        case "SET_SELL_AMOUNT":
                            Commands.SetSellAmount(username, commandParams);
                            break;
                        case "SET_SELL_TRIGGER":
                            Commands.SetSellTrigger(username, commandParams);
                            break;
                        case "CANCEL_SET_SELL":
                            Commands.CancelSetSell(username, commandParams);
                            break;
                        case "COMMIT_SELL_TRIGGER":
                            Commands.CommitSellTrigger(username, commandParams);
                            break;
                        case "DUMPLOG":
                            Commands.Dumplog(username, commandParams);
                            break;
                        case "DISPLAY_SUMMARY":
                            Commands.DisplaySummary(username);
                            break;
                        default:
                            Console.Error.WriteLine($"Unknown command '{CurrentCommand}'");
                            break;
                    }

                    Logger.CommitLogs();
                    transaction.Commit();
                }
                catch (ArgumentException ex)
                {
                    error = $"Invalid parameters for command '{CurrentCommand}': {ex.Message}";
                }
                catch (InvalidOperationException ex)
                {
                    error = $"Command '{CurrentCommand}' could not be run: {ex.Message}";
                }
                catch (MySqlException ex)
                {
                    error = $"!!!SQL ERROR!!! {ex.Message}";
                }
                catch (Exception ex)
                {
                    error = $"!!!UNEXPECTED ERROR!!! {ex.Message}";
                }

                if (error != null)
                {
                    Console.Error.WriteLine(error);
                    transaction.Rollback();
                    Logger = new Logger();
                    Logger.LogEvent(Logger.EventType.Error, error);
                    Logger.CommitLogs();
                }
            }

            // Clear the logger now that we are done this unit of work
            Logger = null;
            CurrentCommand = null;
        }

        static async Task Main(string[] args)
        {
            SqlHelper.OpenSqlConnection();
            RabbitHelper.CreateConsumer(RunCommands, RabbitHelper.rabbitCommandQueue);
            RabbitHelper.CreateConsumer(RunCommands, RabbitHelper.rabbitTriggerRxQueue);

            Console.WriteLine("Titanoboa running...");

            if (args.Contains("--no-input"))
            {
                while (true)
                {
                    await Task.Delay(int.MaxValue);
                }
            }
            else
            {
                Console.WriteLine("Press [enter] to exit.");
                Console.ReadLine();
            }

            //Close connection
            SqlHelper.CloseSqlConnection();
        }
    }
}
