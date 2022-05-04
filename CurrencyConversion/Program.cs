using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CurrencyConversion
{
    internal class Program
    {
        static string url = "https://api-coding-challenge.neofinancial.com/currency-conversion?seed=17420";
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("Neo Financial Currency Conversion!");

            double amountToConvert = 100;
            string currencyToConvertFrom = "CAD";

            //1. Get data from the API
            List<CurrencyConversionMapping> currencyConversionMappings = await GetCurrencyConversionMapping();

            //2. Get all currencies code that we can convert into exclude the one we want to to convert from 
            HashSet<string> currenciesToConvertInto = GetConvertingCurrencyCode(currencyToConvertFrom, currencyConversionMappings);

            //3. Generate a mapping of the Data using relationship flow
            Dictionary<string, Dictionary<string, CurrencyConversionMapping>> mapConversionRate = MapConversionRate(currencyConversionMappings);

            //4. Get the list of Best conversion rates 
            var output = BestPossibleConversionRates(currencyToConvertFrom, amountToConvert, currenciesToConvertInto, mapConversionRate);

            //4. Loop thought and output the list 
            using var writer = new StreamWriter("currencyconversion.csv");

            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(output);
        }

        private static List<PossibleConversionRate> BestPossibleConversionRates(string currencyToConvertFrom, double amountToConvert, HashSet<string> currenciesToConvertInto, Dictionary<string, Dictionary<string, CurrencyConversionMapping>> mapConversionRate)
        {
            var result = new List<PossibleConversionRate>();

            foreach (var item in currenciesToConvertInto)
            {
                var possibleConversionRates = GetConversionRate(currencyToConvertFrom, item, mapConversionRate);
                if (possibleConversionRates.Any())
                {
                    //Get the Best conversion rate from the generated rates
                    var bestPossibleConversion = possibleConversionRates.OrderByDescending(x => x.ConversionRate).FirstOrDefault();
                    bestPossibleConversion.ConversionRate *= amountToConvert;
                    result.Add(bestPossibleConversion);
                }
            }

            return result;
        }

        //This handle all possible conversions that can occur to generate the conversion between the currencies
        public static List<PossibleConversionRate> GetConversionRate(string currencyStart, string currencyEnd, Dictionary<string, Dictionary<string, CurrencyConversionMapping>> map)
        {
            List<PossibleConversionRate> possibleConversionRatio = new List<PossibleConversionRate>();

            Queue<string> queue = new Queue<string>();
            Queue<double> queueRatio = new Queue<double>();
            string pipeDelimited = currencyStart;

            queue.Enqueue(currencyStart);
            queueRatio.Enqueue(1.0);
            
            HashSet<string> visited = new();

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                double currentRatio = queueRatio.Dequeue();
                
                if (visited.Contains(current)) continue;

                visited.Add(current);

                if (map.ContainsKey(current))
                {
                    Dictionary<string, CurrencyConversionMapping> currentMap = map[current];
                    foreach (string key in currentMap.Keys)
                    {
                        if (!visited.Contains(key))
                        {
                            if (key.Equals(currencyEnd))
                            {
                                possibleConversionRatio.Add(new PossibleConversionRate
                                {
                                    CurrencyCode = currentMap[key].toCurrencyCode,
                                    Country = ExtractCountryNameFromCurrencyName(currentMap[key].toCurrencyName),
                                    ConversionRate = currentRatio * currentMap[key].exchangeRate,
                                    ConversionPath = $"{pipeDelimited}|{key}"
                                });

                                queue.Enqueue(currencyStart);
                                queueRatio.Enqueue(1.0);
                                pipeDelimited = currencyStart;
                                continue;
                            }

                            queue.Enqueue(key);
                            queueRatio.Enqueue(currentRatio * currentMap[key].exchangeRate);
                            pipeDelimited = $"{pipeDelimited}|{key}";
                        }
                    }
                }
            }
            return possibleConversionRatio;
        }

        //Handle the external API call to get the currency conversion mapping
        public static async Task<List<CurrencyConversionMapping>> GetCurrencyConversionMapping()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(url)
            };
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                string responseResult = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<CurrencyConversionMapping>>(responseResult);
            }

            return new List<CurrencyConversionMapping>();
        }

        /*
         * This method handle different conversions that can occur between different countries 
         * by building a mapping relationship (e.g tree like) between the conversion rates
         */
        public static Dictionary<string, Dictionary<string, CurrencyConversionMapping>> MapConversionRate(List<CurrencyConversionMapping> data)
        {
            Dictionary<string, Dictionary<string, CurrencyConversionMapping>> map = new();

            foreach (var item in data)
            {
                if (!map.ContainsKey(item.fromCurrencyCode))
                {
                    map.Add(item.fromCurrencyCode, new Dictionary<string, CurrencyConversionMapping>());
                }
                map[item.fromCurrencyCode].Add(item.toCurrencyCode, item);

                if (!map.ContainsKey(item.toCurrencyCode))
                {
                    map.Add(item.toCurrencyCode, new Dictionary<string, CurrencyConversionMapping>());
                }

                //swapping of data occurred here to handle the reverse conversion that can occur between them
                var newItem = new CurrencyConversionMapping
                {
                    exchangeRate = 1.0 / item.exchangeRate,
                    fromCurrencyCode = item.toCurrencyCode,
                    fromCurrencyName = item.toCurrencyName,
                    toCurrencyCode = item.fromCurrencyCode,
                    toCurrencyName = item.fromCurrencyName
                };

                map[item.toCurrencyCode].Add(item.fromCurrencyCode, newItem);
            }

            return map;
        }

        /* 
         * currencyCodeToConvert is the fromCurrencyCode we want to convert to other currencies from the data
         * This method can be remove in case we want to supply the fromCurrencyCode and toCurrencyCode ourselves
        */
        public static HashSet<string> GetConvertingCurrencyCode(string currencyCodeToConvert, List<CurrencyConversionMapping> currencyConversionMappings)
        {
           HashSet<string> result = new HashSet<string>();

            foreach (var currencyConversionMapping in currencyConversionMappings)
            {
                if (!result.Contains(currencyConversionMapping.fromCurrencyCode))
                {
                    result.Add(currencyConversionMapping.fromCurrencyCode);
                }

                if (!result.Contains(currencyConversionMapping.toCurrencyCode))
                {
                    result.Add(currencyConversionMapping.toCurrencyCode);
                }
            }

            result.Remove(currencyCodeToConvert);
            return result;
        }

        /** The method was used to extract the country name from the currency name **/
        public static string ExtractCountryNameFromCurrencyName(string currencyName)
        {
            var names = currencyName.Split(" ");
            if (names.Length > 1)
            {
                currencyName = string.Join(" ", names.SkipLast(1));
            }

            return currencyName;
        }
    }
}
