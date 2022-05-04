using CsvHelper.Configuration.Attributes;

namespace CurrencyConversion
{
    public class CurrencyConversionMapping
    {
        public double exchangeRate { get; set; }
        public string fromCurrencyCode { get; set; }
        public string fromCurrencyName { get; set; }
        public string toCurrencyCode { get; set; }
        public string toCurrencyName { get; set; }
    }

    public class PossibleConversionRate
    {
        [Name("Currency Code")]
        public string CurrencyCode { get; set; }

        [Name("Country")]
        public string Country { get; set; }

        [Name("Amount of Currency")]
        public double ConversionRate { get; set; }

        [Name("Path for the best conversion rate")]
        public string ConversionPath { get; set; }
    }
}
