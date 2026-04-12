using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Snowflake.Data.Client;

namespace RfcWorker
{
    /// <summary>
    /// Unified RFC Worker — replaces all 48 individual console applications.
    /// Reads configuration from Snowflake (RFC_MASTER, RFC_PARAM, RFC_SELECT_OPTION),
    /// executes SAP RFC calls via SAP NCo 3.0, and writes results back to Snowflake.
    ///
    /// Usage:
    ///   RfcWorker.exe RFC_CODE [--env PROD|UAT] [--date-from YYYY-MM-DD] [--date-to YYYY-MM-DD] [--stores DH01,DH02] [--triggered-by user]
    ///
    /// Example:
    ///   RfcWorker.exe RFC_Sales_Data --env PROD --date-from 2026-04-10 --date-to 2026-04-10 --triggered-by "Portal User"
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: RfcWorker.exe RFC_CODE [--env PROD|UAT] [--date-from YYYY-MM-DD] [--date-to YYYY-MM-DD] [--stores code1,code2] [--triggered-by user]");
                return 1;
            }

            string rfcCode = args[0];
            string environment = GetArg(args, "--env", "PROD");
            string dateFrom = GetArg(args, "--date-from", DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"));
            string dateTo = GetArg(args, "--date-to", DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"));
            string storeFilter = GetArg(args, "--stores", null);
            string triggeredBy = GetArg(args, "--triggered-by", "RfcWorker");

            Console.WriteLine($"=== RFC Worker Started ===");
            Console.WriteLine($"RFC Code:    {rfcCode}");
            Console.WriteLine($"Environment: {environment}");
            Console.WriteLine($"Date Range:  {dateFrom} to {dateTo}");
            Console.WriteLine($"Triggered By:{triggeredBy}");
            Console.WriteLine();

            try
            {
                var engine = new RfcEngine();
                return engine.Execute(rfcCode, environment, dateFrom, dateTo, storeFilter, triggeredBy);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                var inner = ex.InnerException;
                while (inner != null)
                {
                    Console.WriteLine($"  Inner: {inner.GetType().Name}: {inner.Message}");
                    inner = inner.InnerException;
                }
                Console.WriteLine(ex.StackTrace);
                return 2;
            }
        }

        static string GetArg(string[] args, string name, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return defaultValue;
        }
    }
}
