using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

class Response
{
    public long ChatId { get; set; }
    public required string FIO { get; set; }
    public required string OfficeChoice { get; set; }
    public required string LunchChoice { get; set; }
    public DateTime Date { get; set; }
}

class Program
{
    static List<Response> employees = new();
    const string DataFile = "responses.csv";
    const long AdminChannelId = -1003112040803;

    static string Token = "8345872765:AAFCkGFu7Hlx0KG9r3lRIkjeTFQ5aPL15kU";
    static TelegramBotClient bot = new(Token);

    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        LoadResponses();

        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        // Endpoint для webhook
        app.MapPost("/webhook", async (HttpRequest request) =>
        {
            var update = await request.ReadFromJsonAsync<Update>();
            if (update != null)
                await HandleUpdateAsync(update);
            return Results.Ok();
        });

        // Порт берём из Render
        var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
        var url = $"https://{Environment.GetEnvironmentVariable("RENDER_EXTERNAL_HOSTNAME")}";
        await bot.SetWebhookAsync($"{url}/webhook");

        // Запуск ASP.NET сервера
        Console.WriteLine($"✅ Бот запущен и слушает вебхук на /webhook");
        app.Run($"http://0.0.0.0:{port}");
    }

    static async Task HandleUpdateAsync(Update update)
    {
        if (update.Type == UpdateType.Message && update.Message != null)
        {
            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text;

            if (chatId == AdminChannelId) return;

            if (text == "/start")
            {
                var existingUser = employees.FirstOrDefault(e => e.ChatId == chatId && e.Date.Date == DateTime.Today);
                if (existingUser != null)
                {
                    await bot.SendTextMessageAsync(chatId, "⚠️ Вы уже прошли опрос сегодня. Повторное прохождение недоступно 😊");
                    return;
                }

                await bot.SendTextMessageAsync(chatId, "Привет! Введи, пожалуйста, своё ФИО:");
                return;
            }

            if (!employees.Any(e => e.ChatId == chatId && e.Date.Date == DateTime.Today))
            {
                employees.Add(new Response
                {
                    ChatId = chatId,
                    FIO = text,
                    OfficeChoice = "",
                    LunchChoice = "",
                    Date = DateTime.Today
                });
                SaveResponses();
                await SendOfficePoll(chatId);
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            var callback = update.CallbackQuery!;
            var chatId = callback.Message!.Chat.Id;
            var user = employees.FirstOrDefault(e => e.ChatId == chatId && e.Date.Date == DateTime.Today);

            if (user == null) return;

            if (callback.Data!.StartsWith("office_"))
            {
                string choice = callback.Data.Replace("office_", "");
                user.OfficeChoice = choice;

                await bot.EditMessageReplyMarkupAsync(chatId, callback.Message.MessageId, replyMarkup: null);
                await bot.SendTextMessageAsync(chatId, "Спасибо! Теперь выбери, нужен ли тебе обед:", replyMarkup: GetLunchKeyboard());
                SaveResponses();
            }
            else if (callback.Data.StartsWith("lunch_"))
            {
                string choice = callback.Data.Replace("lunch_", "");
                user.LunchChoice = choice;

                await bot.EditMessageReplyMarkupAsync(chatId, callback.Message.MessageId, replyMarkup: null);
                await bot.SendTextMessageAsync(chatId, "Отлично, твой ответ записан ✅");
                SaveResponses();
            }
        }
    }

    static InlineKeyboardMarkup GetLunchKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🍱 Да, нужен обед", "lunch_Yes"),
                InlineKeyboardButton.WithCallbackData("🥪 Нет, не нужен", "lunch_No")
            }
        });
    }

    static async Task SendOfficePoll(long chatId)
    {
        InlineKeyboardMarkup keyboard = new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🏢 Front офис", "office_Front"),
                InlineKeyboardButton.WithCallbackData("💻 Back офис", "office_Back")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🚫 Не приду", "office_No")
            }
        });

        await bot.SendTextMessageAsync(chatId, "Ты сегодня работаешь из офиса или нет?", replyMarkup: keyboard);
    }

    static void SaveResponses()
    {
        try
        {
            using var sw = new System.IO.StreamWriter(DataFile, false, Encoding.UTF8);
            sw.WriteLine("ChatId,FIO,OfficeChoice,LunchChoice,Date");
            foreach (var e in employees)
                sw.WriteLine($"{e.ChatId},{e.FIO},{e.OfficeChoice},{e.LunchChoice},{e.Date:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения CSV: {ex.Message}");
        }
    }

    static void LoadResponses()
    {
        if (!System.IO.File.Exists(DataFile)) return;

        try
        {
            var lines = System.IO.File.ReadAllLines(DataFile).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                employees.Add(new Response
                {
                    ChatId = long.Parse(parts[0]),
                    FIO = parts[1],
                    OfficeChoice = parts[2],
                    LunchChoice = parts[3],
                    Date = DateTime.Parse(parts[4])
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки CSV: {ex.Message}");
        }
    }
}
