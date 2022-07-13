using System.Net.Http;
using System.Text.Json;
using Task11;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

Config? config;
using (StreamReader reader = new StreamReader("/Users/PC-316/Source/Repos/projectbot/Task11/Task11/jsconfig.json"))
{
    string text = await reader.ReadToEndAsync();
    config = JsonSerializer.Deserialize<Config>(text);
}


var botClient = new TelegramBotClient(config.Token);

using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { }
};

botClient.StartReceiving(
    HandleUpdatesAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listen: @{me.Username}");
Console.ReadLine();

cts.Cancel();



async Task HandleUpdatesAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.Message && update?.Message?.Text != null)
    {
        await HandleMessage(botClient, update.Message);
        return;
    }
}

async Task HandleMessage(ITelegramBotClient botClient, Message message)
{
    if (message.Text == "/start")
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "This bot can showing the exchange rate of foreince currency to UAH\nPlease, send me date and currency like:\n" +
            "dd.mm.yyyy USD(code currency)");
        return;
    }
    if (message.Text == "/help")
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, config.Supported);
        return;
    }

    string[] dateAndCurrency = ParseInput(message.Text);

    if(dateAndCurrency.Length != 2)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Incorrect input");
        return;
    }

    bool dateResult = CheckDate(dateAndCurrency[0]);
    bool currencyResult = CheckCurrency(dateAndCurrency[1]);

    if (dateResult && currencyResult)
    {
        List<Exchange> exchangeList = await GetJsonData(message.Text);
        foreach (Exchange exchange in exchangeList)
        {
            if (exchange.currency == dateAndCurrency[1] && exchange.saleRate != 0)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, $"{exchange.currency}\nSale: {exchange.saleRate}\nPurchase: {exchange.purchaseRate}");
                return;
            }
            if (exchange.currency == dateAndCurrency[1] && exchange.saleRate == 0)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, $"No data for this currency for this date");
                return;
            }
        }
    }

    if (!dateResult && !currencyResult)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Incorrect date and currency");
        return;
    }

    if (!dateResult)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Incorrect date");
        return;
    }

    if (!currencyResult)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "Incorrect currency");
        return;
    }

    return;
}


Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API ERROR:\n{apiRequestException.ErrorCode}\n{apiRequestException.Message}",
        _ => exception.ToString()
    };
    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

static string[] ParseInput(string messageText)
{
    string date = "";
    string currency = "";
    string[] dateAndCurrency = messageText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    return dateAndCurrency;

}

static async Task<List<Exchange>> GetJsonData(string messageText)
{
    HttpClient client = new HttpClient();
    List<Exchange> exchangeList = new List<Exchange>();

    using (Stream stream = await client.GetStreamAsync($"https://api.privatbank.ua/p24api/exchange_rates?json&date={messageText}"))
    {
        using (StreamReader reader = new StreamReader(stream))
        {
            string line = reader.ReadLine();

            ExchangeRate? exchangeRate = JsonSerializer.Deserialize<ExchangeRate>(line);

            foreach (JsonElement row in exchangeRate.exchangeRate)
            {
                Exchange? exchange = JsonSerializer.Deserialize<Exchange>(row);
                exchangeList.Add(exchange);
            }

            return exchangeList;
        }
    }
}

bool CheckCurrency(string currency)
{
    string[] currencies = config.Currencies;
    if (currencies.Contains(currency))
    {
        return true;
    }

    return false;
}

static bool CheckDate(string date)
{
    DateTime dateTimeNow = DateTime.Now;
    bool res = DateTime.TryParse(date, out DateTime resultDate);
    if (res)
    {
        DateTime minDate = new DateTime(2014, 12, 1);
        if (resultDate < minDate)
        {
            return false;
        }

        if (resultDate > dateTimeNow)
        {
            return false;
        }

        return true;
    }
    return false;
}
