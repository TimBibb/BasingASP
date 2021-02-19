using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using BasingApi.Controllers;
using BasingApi.Data.JSON;
using Newtonsoft.Json;
using RestSharp;

namespace BasingApi.Data
{
    public static class TickerUpdater
    {
        public static Dictionary<string, TickerData> CurrentData = new Dictionary<string, TickerData>();
        private static string _TkPath = "/tks/tk.txt";
        private static string _CachedDataFile = "cache.json";
        private static string _TickerFile = "tickers.csv";
        private static string _Interval = "15m";
        private static string _Period = "1mo";
        private static float _Range = 0.05f;
        private const float _VOLUMESPIKECAP = 0.05f;
        private static Dictionary<string, HistoricalData> _Hist = new Dictionary<string, HistoricalData>();
        private static Dictionary<string, TickerData> _Basing = new Dictionary<string, TickerData>();
        private static Dictionary<string, int> _PreviousVolume = new Dictionary<string, int>();
        private static bool _GottenNewData = false;

        public static void UpdateData()
        {
            TimeSpan start = new TimeSpan(14, 30, 0); // 9:30am 
            TimeSpan end = new TimeSpan(23, 30, 0); // 5:00pm
            TimeSpan now = DateTime.UtcNow.TimeOfDay;
            Console.WriteLine(now.ToString());
            
            if ((now > start) && (now < end))
            {
                if (_Basing.Count < 1 || _Basing == null)
                {
                    if (File.Exists(_CachedDataFile))
                    {
                        Console.WriteLine("Pulling from cached data...");
                        _Basing = JsonConvert.DeserializeObject<Dictionary<string, TickerData>>(File.ReadAllText(_CachedDataFile));
                    }
                    else
                    {
                        DownloadNewHistData();
                    }
                }
                foreach (var (_, value) in _Basing)
                {
                    InstantData instantData = DownloadIEXInstant(value);
                    Console.WriteLine(value + " Instant Data: " + instantData);
                    if (instantData != null)
                    {
                        ProcessIEXInstant(instantData, value);
                        CurrentData[value.Ticker] = value;
                        Console.WriteLine(value.Ticker + " added to current data.");
                    }
                }

                _GottenNewData = false;
            }
            else
            {
                if (!_GottenNewData)
                {
                    if (File.Exists(_CachedDataFile))
                        File.Delete(_CachedDataFile);
                    DownloadNewHistData();
                    _GottenNewData = true;
                }
            }
        }

        private static void DownloadNewHistData()
        {
            string[] tickerList = File.ReadAllLines(_TickerFile);
            Console.WriteLine("Downloading new data...");
            foreach (string ticker in tickerList)
            {
                TickerData tData = null;
                HistoricalData histData = DownloadHistoricalData(ticker);
                Console.WriteLine("Fetched: " + ticker);

                if (histData != null)
                {
                    tData = ProcessHistDataByTicker(histData, ticker);
                        
                    _Hist[ticker] = (histData);
                }
                    
                if (tData != null)
                {
                    Console.WriteLine("Added to basing: " + ticker);
                    _Basing[ticker] = tData;
                    _PreviousVolume[ticker] = 1;
                }
            }
            // Save data to a file so it can be archived / cached for later use
            File.WriteAllText(_CachedDataFile, JsonConvert.SerializeObject(_Basing));
        }
        private static InstantData DownloadIEXInstant(TickerData tickerData)
        {
            string token = File.ReadAllText(_TkPath).Replace("\n", "");
            InstantData jsonData = null;
            
            if (tickerData == null)
                return null;
            
            RestClient client = new RestClient("https://cloud.iexapis.com/stable/stock/" + tickerData.Ticker +
                                                   "/quote?token=" + token);
            RestRequest request = new RestRequest("", Method.GET);

            IRestResponse response = client.Execute(request);
            
            if (!response.IsSuccessful)
                return null;

            try
            {
                jsonData = JsonConvert.DeserializeObject<InstantData>(response.Content);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error encoding data for " + tickerData.Ticker);
                Console.WriteLine(e.ToString());
            }
            

            return jsonData;
        }
        private static void ProcessIEXInstant(InstantData instantData, TickerData tickerData)
        {
            bool volSpiking = false;
            bool priceSpikingUp = false;
            bool priceSpikingDown = false;

            double volDelta = instantData.latestVolume - _PreviousVolume[tickerData.Ticker];
            double volGainPercentage = volDelta / instantData.avgTotalVolume;

            if (volGainPercentage >= _VOLUMESPIKECAP)
                volSpiking = true;

            if (instantData.changePercent >= _VOLUMESPIKECAP)
                priceSpikingUp = true;
            else if (instantData.changePercent < _VOLUMESPIKECAP)
                priceSpikingDown = true;

            if (tickerData.Deviation > -1 && tickerData.Deviation <= 6)
                tickerData.Volatility = "Steady";
            else if (tickerData.Deviation > 6 && tickerData.Deviation <= 12)
                tickerData.Volatility = "Moderate";
            else if (tickerData.Deviation > 12)
                tickerData.Volatility = "Volatile";

            if (volSpiking)
            {
                if (priceSpikingUp)
                    tickerData.Breaking = "Breaking Up!";
                else if (priceSpikingDown)
                    tickerData.Breaking = "Breaking Down!";
                else
                    tickerData.Breaking = "Volume Spike but no price change!";
            } else if (priceSpikingUp)
            {
                tickerData.Breaking = "Breaking Up but no volume change";
            } else if (priceSpikingDown)
            {
                tickerData.Breaking = "Breaking Down but no volume change";
            } else
            {
                tickerData.Breaking = "";
            }
            
            tickerData.Price = (float) instantData.latestPrice;
            _PreviousVolume[tickerData.Ticker] = instantData.latestVolume;
        }
        private static HistoricalData DownloadHistoricalData(string ticker)
        {
            HistoricalData jsonData = null;
            RestClient client = new RestClient("https://query1.finance.yahoo.com/v8/finance/chart/" + ticker);
            RestRequest request = new RestRequest("", Method.GET);
            request.AddParameter("interval", _Interval);
            request.AddParameter("range", _Period);

            IRestResponse response = client.Execute(request);

            try
            {
                jsonData = JsonConvert.DeserializeObject<HistoricalData>(response.Content);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to fetch " + ticker);
                return null;
            }

            return jsonData;
        }

        private static TickerData ProcessHistDataByTicker(HistoricalData data, string quoteSymbol)
        {
            float avgDev = 0;
            bool basing = true;

            if (data?.chart?.result == null)
                return null;
            if (data.chart.result.Count < 1)
                return null;
            
            List<double> close = data.chart.result[0].indicators.quote[0].close;
            
            if (close == null)
                return null;
            
            float avg = (float)close.Average();
            float support = avg - (avg * _Range);
            float resistance = avg + (avg * _Range);
            
            
            foreach(double p in close)
            {
                float newDev = (float)Math.Abs((p - avg) / avg);
                if (p > resistance || p < support)
                    basing = false;
                avgDev += newDev;
            }

            avgDev /= close.Count;

            if (avgDev > 0.05f)
                basing = false;

            if (basing && avgDev != 0.0f)
            {
                TickerData ticker = new TickerData();
                ticker.Deviation = avgDev * 1000;
                ticker.Resistance = resistance;
                ticker.Support = support;
                ticker.Ticker = quoteSymbol;
                return ticker;
            }

            return null;
        }
    }
}