using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    static ITelegramBotClient botClient;

    static async Task Main()
    {
        botClient = new TelegramBotClient(Environment.GetEnvironmentVariable("BOT_TOKEN"));

        // Удаляем старый webhook, если есть
        await botClient.DeleteWebhook();

        var cts = new CancellationTokenSource();

        // Настройки для получения обновлений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // все типы обновлений
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandlePollingErrorAsync,
            receiverOptions,
            cts.Token
        );

        var me = await botClient.GetMe();
        Console.WriteLine($"✅ Бот @{me.Username} запущен. Нажми Ctrl+C для выхода.");

        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("Остановка бота...");
            cts.Cancel();
        };

        // Просто держим приложение запущенным
        await Task.Delay(-1);
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message.Text == null) return;

        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text;

        if (text == "/start")
        {
            await bot.SendMessage(chatId, "Привет! Выберите действие:", cancellationToken: cancellationToken,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Я иду в офис", "office"),
                        InlineKeyboardButton.WithCallbackData("Не иду", "home")
                    }
                }));
        }
    }

    static Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка Telegram: {exception.Message}");
        return Task.CompletedTask;
    }

    // Метод для безопасного чтения файлов
    static byte[] ReadFileBytes(string path)
    {
        return File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>();
    }
}
