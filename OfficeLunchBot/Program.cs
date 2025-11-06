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

        // Порт и URL для Render
        var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
        var url = $"https://{Environment.GetEnvironmentVariable("RENDER_EXTERNAL_HOSTNAME")}";
        await bot.SetWebhookAsync($"{url}/webhook");

        // Запуск таймеров: опрос 9:00, напоминание 10:00, отчёт 11:00
        StartScheduledTasks();

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
                await bot.SendTextMessageAsync(chatId,
                    "Привет! 👋\nЯ бот для ежедневного опроса о приходе в офис и обеде.\n" +
                    "Опрос можно пройти только в 9:00. Если вы уже прошли опрос сегодня — бот об этом сообщит автоматически.");
                return;
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            var callback = update.CallbackQuery!;
            var chatId = callback.Message!.Chat.Id;
            var user = employees.FirstOrDefault(e => e.ChatId == chatId && e.Date.Date == DateTime.Today);

            if (user == null)
            {
                // Пользователь пытается пройти опрос вне 9:00
                await bot.SendTextMessageAsync(chatId, "⏰ Опрос можно пройти только в 9:00!");
                return;
            }

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

    static void StartScheduledTasks()
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");

        // 9:00 — ежедневный опрос
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var now = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
                var next9 = DateTime.Today.AddHours(9);
                if (now.TimeOfDay >= TimeSpan.FromHours(9))
                    next9 = next9.AddDays(1);

                var delay = next9 - now;
                await Task.Delay(delay);

                foreach (var emp in employees)
                {
                    if (!employees.Any(e => e.ChatId == emp.ChatId && e.Date.Date == DateTime.Today))
                    {
                        employees.Add(new Response
                        {
                            ChatId = emp.ChatId,
                            FIO = "", // можно заполнить заранее
                            OfficeChoice = "",
                            LunchChoice = "",
                            Date = DateTime.Today
                        });
                        await SendOfficePoll(emp.ChatId);
                    }
                }
                Console.WriteLine("📝 Ежедневный опрос отправлен всем сотрудникам в 9:00");
            }
        });

        // 10:00 — напоминание
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var now = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
                var next10 = DateTime.Today.AddHours(10);
                if (now.TimeOfDay >= TimeSpan.FromHours(10))
                    next10 = next10.AddDays(1);

                var delay = next10 - now;
                await Task.Delay(delay);

                foreach (var emp in employees)
                    await bot.SendTextMessageAsync(emp.ChatId, "🕙 Напоминание: пожалуйста, пройди ежедневный опрос!");
            }
        });

        // 11:00 — отчёт
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var now = TimeZoneInfo.ConvertTime(DateTime.Now, tz);
                var next11 = DateTime.Today.AddHours(11);
                if (now.TimeOfDay >= TimeSpan.FromHours(11))
                    next11 = next11.AddDays(1);

                var delay = next11 - now;
                await Task.Delay(delay);

                await SendDailyReport();

                employees.Clear();
                SaveResponses();
                Console.WriteLine("♻️ Ответы пользователей сброшены для нового дня.");
            }
        });
    }

    static async Task SendDailyReport()
    {
        var todayResponses = employees.Where(e => e.Date.Date == DateTime.Today).ToList();

        var front = todayResponses.Where(e => e.OfficeChoice == "Front").ToList();
        var back = todayResponses.Where(e => e.OfficeChoice == "Back").ToList();
        var no = todayResponses.Where(e => e.OfficeChoice == "No").ToList();

        string FormatUser(Response e) => $"{e.FIO} ({(e.LunchChoice == "Yes" ? "🍱 обед" : "❌ без обеда")})";

        string report =
            $"📊 *Отчёт за {DateTime.Today:dd.MM.yyyy}*\n\n" +
            $"🏢 *Front офис* ({front.Count}): {string.Join(", ", front.Select(FormatUser))}\n" +
            $"💻 *Back офис* ({back.Count}): {string.Join(", ", back.Select(FormatUser))}\n" +
            $"🚫 *Не придут* ({no.Count}): {string.Join(", ", no.Select(FormatUser))}\n";

        await bot.SendTextMessageAsync(AdminChannelId, report, parseMode: ParseMode.Markdown);
        Console.WriteLine("✅ Отчёт с обедом отправлен в канал админов");
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
