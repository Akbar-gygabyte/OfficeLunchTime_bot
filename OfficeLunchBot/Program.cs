using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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

static async Task Main()
{
    Console.OutputEncoding = Encoding.UTF8;
    LoadResponses();

    string token = Environment.GetEnvironmentVariable("8345872765:AAFCkGFu7Hlx0KG9r3lRIkjeTFQ5aPL15kU") ?? "";
    if (string.IsNullOrWhiteSpace(token))
    {
        Console.WriteLine("❌ Ошибка: переменная окружения BOT_TOKEN не задана!");
        return;
    }

    var bot = new TelegramBotClient(token);

    var me = await bot.GetMeAsync();
    Console.WriteLine($"✅ Бот @{me.Username} запущен и слушает обновления...");

    using var cts = new CancellationTokenSource();

    var receiverOptions = new ReceiverOptions
    {
        AllowedUpdates = null // важно! принимает все типы обновлений
    };

    bot.StartReceiving(
        HandleUpdateAsync,
        HandleErrorAsync,
        receiverOptions,
        cts.Token
    );

    _ = ScheduleDailyReminder(bot);
    _ = ScheduleDailyReport(bot);

    // бесконечный цикл — Railway теперь не завершает процесс
    await Task.Delay(-1);
}




    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message != null)
        {
            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text;

            // Игнорируем сообщения из канала админов
            if (chatId == AdminChannelId) return;

            if (text == "/start")
            {
                // Проверяем, не проходил ли пользователь опрос сегодня
                var existingUser = employees.FirstOrDefault(e => e.ChatId == chatId && e.Date.Date == DateTime.Today);
                if (existingUser != null)
                {
                    await bot.SendTextMessageAsync(chatId, "⚠️ Вы уже прошли опрос сегодня. Повторное прохождение недоступно 😊");
                    return;
                }

                await bot.SendTextMessageAsync(chatId, "Привет! Введи, пожалуйста, своё ФИО:");
                return;
            }

            // Если пользователь ввёл ФИО
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
                await SendOfficePoll(bot, chatId);
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            var callback = update.CallbackQuery!;
            var chatId = callback.Message!.Chat.Id;
            var user = employees.FirstOrDefault(e => e.ChatId == chatId && e.Date.Date == DateTime.Today);

            if (user == null) return; // безопасность

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

    static async Task SendOfficePoll(ITelegramBotClient bot, long chatId)
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

    // Напоминание в 10:00 всем пользователям
    static async Task ScheduleDailyReminder(ITelegramBotClient bot)
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");

        while (true)
        {
            DateTime now = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
            DateTime next10 = DateTime.Today.AddHours(10);
            if (now.TimeOfDay >= TimeSpan.FromHours(10))
                next10 = next10.AddDays(1);

            TimeSpan delay = next10 - now;
            await Task.Delay(delay);

            foreach (var emp in employees)
                await bot.SendTextMessageAsync(emp.ChatId, "🕙 Напоминание: пожалуйста, пройди ежедневный опрос!");
        }
    }

    // Ежедневный отчёт в 11:00
    static async Task ScheduleDailyReport(ITelegramBotClient bot)
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");

        while (true)
        {
            DateTime now = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
            DateTime next11 = DateTime.Today.AddHours(11);
            if (now.TimeOfDay >= TimeSpan.FromHours(11))
                next11 = next11.AddDays(1);

            TimeSpan delay = next11 - now;
            await Task.Delay(delay);

            await SendDailyReport(bot);

            employees.Clear();
            SaveResponses();
            Console.WriteLine("♻️ Ответы пользователей сброшены для нового дня.");
        }
    }

    static async Task SendDailyReport(ITelegramBotClient bot)
    {
        var todayResponses = employees.Where(e => e.Date.Date == DateTime.Today).ToList();

        var front = todayResponses.Where(e => e.OfficeChoice == "Front").Select(e => e.FIO).ToList();
        var back = todayResponses.Where(e => e.OfficeChoice == "Back").Select(e => e.FIO).ToList();
        var no = todayResponses.Where(e => e.OfficeChoice == "No").Select(e => e.FIO).ToList();

        string report =
            $"📊 *Отчёт за {DateTime.Today:dd.MM.yyyy}*\n\n" +
            $"🏢 *Front офис* ({front.Count}): {string.Join(", ", front)}\n" +
            $"💻 *Back офис* ({back.Count}): {string.Join(", ", back)}\n" +
            $"🚫 *Не придут* ({no.Count}): {string.Join(", ", no)}\n";

        await bot.SendTextMessageAsync(AdminChannelId, report, parseMode: ParseMode.Markdown);
        Console.WriteLine("✅ Отчёт отправлен в канал админов");
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

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}
