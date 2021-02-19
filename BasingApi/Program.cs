using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BasingApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BasingApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            
            //Background updater thread
            new Thread(() =>
            {

                while (true)
                {
                    TickerUpdater.UpdateData();
                    
                    Thread.Sleep(30 * 1000);
                }

            }).Start();
            
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://*:80");
                });
    }
}
