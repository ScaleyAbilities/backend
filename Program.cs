﻿using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace Titanoboa
{
    class Program
    {
        internal static readonly string ServerName = Environment.GetEnvironmentVariable("SERVER_NAME") ?? "Titanoboa";
        
        internal static ConcurrentQueue<(long id, Task task)> runningTasks = new ConcurrentQueue<(long id, Task task)>();
        internal static ConcurrentDictionary<string, SemaphoreSlim> userLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        static async Task RunCommands(long taskId, JObject json)
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

            string command = json["cmd"].ToString().ToUpper();
            string username = json["usr"].ToString();
            JObject commandParams = (JObject)json["params"];
            Logger logger;

            try
            {
                // Set up a logger for this unit of work
                logger = new Logger();
                await logger.Init(command);
            }
            catch (DbException ex)
            {
                Console.Error.WriteLine($"Unable to create logger due to SQL error: {ex.Message}");
                return;
            }

            using (var connection = await SqlHelper.GetConnection())
            using (var dbHelper = new DatabaseHelper(connection, logger))
            {
                string error = null;
                int errorLevel = 0; // 0 = lowest, 2 = highest
                var commandHandler = new CommandHandler(username, command, commandParams, dbHelper, logger, taskId);
                try 
                {
                    await commandHandler.Run();
                    await logger.CommitLogs();
                }
                catch (ArgumentException ex)
                {
                    error = $"Invalid parameters for command '{command}': {ex.Message}";
                    errorLevel = 1;
                }
                catch (InvalidOperationException ex)
                {
                    error = $"Command '{command}' could not be run: {ex.Message}";
                    errorLevel = 0;
                }
                catch (DbException ex)
                {
                    error = $"!!!SQL ERROR!!! {ex.Message}";
                    errorLevel = 2;
                }
                catch (Exception ex)
                {
                    error = $"!!!UNEXPECTED ERROR!!! {ex.Message}";
                    errorLevel = 2;
                }

                if (error != null)
                {
                    if (errorLevel > 0) // Only print unexpected errors
                        Console.Error.WriteLine(error);

                    logger = new Logger();
                    await logger.Init(command);
                    logger.LogEvent(Logger.EventType.Error, error);
                    await logger.CommitLogs();
                }
            }
        }

        static async Task Main(string[] args)
        {
            var quitSignalled = new TaskCompletionSource<bool>();
            Console.CancelKeyPress += new ConsoleCancelEventHandler((sender, eventArgs) => {
                quitSignalled.SetResult(true);
                eventArgs.Cancel = true; // Prevent program from quitting right away
            });

            long nextTaskId = 0;
            
            RabbitHelper.CreateConsumer((json) => {
                runningTasks.Append((nextTaskId, RunCommands(nextTaskId++, json)));
            });

            // TODO: Need to make rabbit queue for sending triggers to Twig
            
            Console.WriteLine("Titanoboa running...");
            Console.WriteLine("Press Ctrl-C to exit.");

            while (true) {
                var completed = await Task.WhenAny(quitSignalled.Task, Task.Delay(5000));

                if (completed == quitSignalled.Task)
                    break;

                // We clean up finished tasks every 5 seconds
                CleanupFinishedTasks();
            }

            Console.WriteLine("Quitting...");
            Console.WriteLine("Waiting for running tasks to complete...");
            
            while (!runningTasks.IsEmpty)
            {
                (long id, Task task) taskEntry = (0, null);
                runningTasks.TryDequeue(out taskEntry);
                if (taskEntry.task != null)
                    await taskEntry.task;
            }

            Console.WriteLine("Done.");
        }

        public static async Task WaitForTasksUpTo(long id)
        {
            (long id, Task task) taskEntry = (0, null);
            while (runningTasks.TryPeek(out taskEntry) && taskEntry.id != id)
            {
                runningTasks.TryDequeue(out taskEntry);
                await taskEntry.task;
            }
        }

        private static void CleanupFinishedTasks() {
            (long id, Task task) taskEntry = (0, null);
            while (runningTasks.TryPeek(out taskEntry) && taskEntry.task.IsCompleted) {
                runningTasks.TryDequeue(out taskEntry);
            }
        }
    }
}
