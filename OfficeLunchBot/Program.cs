using System;
using System.Collections.Generic;
using System.IO;
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
    public string FIO { get; set; } = "";
    public string OfficeChoice { get; set; } = "";
    public string LunchChoice { get; set; } = "";
    public DateTime Date { get; set; }
}

class Program
{
    const string DataFile = "responses.csv";
    const long AdminChannelId = -1003112040803; // сюда отправляем отчёт

    static List<Response> employees = new();

static async Task Main()
{
    Console.OutputEncoding = Encoding.UTF8;

    LoadResponses(); // загружаем CSV

    string token = Environment.GetEnvironmentVariable("BOT_TOKEN")!;
    if (string.IsNullOrEmpty(token))
    {
        Console.WriteLine("❌ BOT_TOKEN is missing!");
        return;
    }

    var bot = new TelegramBotClient(token);

    // -------------------- Удаляем старый webhook --------------------
    try
    {
        await bot.DeleteWebhook(); // теперь await
        Console.WriteLine("✅ Старый webhook удалён.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Не удалось удалить webhook: {ex.Message}");
    }

    // -------------------- Настройка long polling --------------------
    using CancellationTokenSource cts = new();
    ReceiverOptions receiverOptions = new()
    {
        AllowedUpdates = Array.Empty<UpdateType>()
    };

    bot.StartReceiving(
        updateHandler: HandleUpdateAsync,
        errorHandler: HandleErrorAsync,
        receiverOptions: receiverOptions,
        cancellationToken: cts.Token
    );

    var me = await bot.GetMe(); // await нужен, иначе вернется Task<User>
    Console.WriteLine($"✅ Бот @{me.Username} запущен на Render.");

    // -------------------- Планировщики --------------------
    _ = ScheduleDailyPoll(bot);
    _ = ScheduleDailyReport(bot);

    // -------------------- Держим процесс живым --------------------
    await Task.Delay(-1);
}




    // ======================== UPDATE HANDLER ========================

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancel)
    {
        if (update.Message is { } message && message.Text is string text)
        {
            long chatId = message.Chat.Id;
            var user = employees.FirstOrDefault(e => e.ChatId == chatId);

            if (text == "/start")
            {
                if (user != null && user.Date.Date == DateTime.Today && user.OfficeChoice != "")
                {
                    await bot.SendMessage(chatId, "❗ Вы уже прошли опрос сегодня.");
                    return;
                }

                await bot.SendMessage(chatId, "Введите своё ФИО:");
                return;
            }

            if (user == null)
            {
                employees.Add(new Response
                {
                    ChatId = chatId,
                    FIO = text,
                    Date = DateTime.Today
                });

                SaveResponses();

                await SendOfficePoll(bot, chatId);
            }
        }

        if (update.CallbackQuery is { } cb)
        {
            long chatId = cb.Message!.Chat.Id;
            var user = employees.FirstOrDefault(e => e.ChatId == chatId);
            if (user == null) return;

            if (user.OfficeChoice != "" && user.Date.Date == DateTime.Today)
            {
                await bot.SendMessage(chatId, "✅ Вы уже ответили сегодня.");
                return;
            }

            if (cb.Data!.StartsWith("office_"))
            {
                string choice = cb.Data.Replace("office_", "");
                user.OfficeChoice = choice;
                SaveResponses();

                await bot.EditMessageReplyMarkup(chatId, cb.Message.MessageId, null);
                await bot.SendMessage(chatId, "Теперь выберите обед:", replyMarkup: GetLunchKeyboard());
            }

            if (cb.Data.StartsWith("lunch_"))
            {
                string choice = cb.Data.Replace("lunch_", "");
                user.LunchChoice = choice;
                user.Date = DateTime.Today;
                SaveResponses();

                await bot.EditMessageReplyMarkup(chatId, cb.Message.MessageId, null);
                await bot.SendMessage(chatId, "✅ Ответ записан!");
            }
        }
    }

    // ======================== BUTTONS ========================

    static InlineKeyboardMarkup GetLunchKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("🍱 Да", "lunch_Yes"),
                InlineKeyboardButton.WithCallbackData("🥪 Нет", "lunch_No")
            }
        });
    }

    static async Task SendOfficePoll(ITelegramBotClient bot, long chatId)
    {
        InlineKeyboardMarkup kb = new(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("Front", "office_Front"),
                InlineKeyboardButton.WithCallbackData("Back", "office_Back")
            },
            new []
            {
                InlineKeyboardButton.WithCallbackData("Не приду", "office_No")
            }
        });

        await bot.SendMessage(chatId, "Где вы сегодня работаете?", replyMarkup: kb);
    }

    // ======================== CSV SAVE / LOAD ========================

    static void SaveResponses()
    {
        using var sw = new StreamWriter(DataFile, false, Encoding.UTF8);
        sw.WriteLine("ChatId,FIO,OfficeChoice,LunchChoice,Date");

        foreach (var e in employees)
            sw.WriteLine($"{e.ChatId},{e.FIO},{e.OfficeChoice},{e.LunchChoice},{e.Date:yyyy-MM-dd}");
    }

    static void LoadResponses()
    {
        if (!System.IO.File.Exists(DataFile)) return;

        var lines = System.IO.File.ReadAllLines(DataFile).Skip(1);
        foreach (var line in lines)
        {
            var p = line.Split(',');
            if (p.Length < 5) continue;

            employees.Add(new Response
            {
                ChatId = long.Parse(p[0]),
                FIO = p[1],
                OfficeChoice = p[2],
                LunchChoice = p[3],
                Date = DateTime.Parse(p[4])
            });
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken token)
    {
        Console.WriteLine($"Ошибка Telegram: {ex.Message}");
        return Task.CompletedTask;
    }

    // ======================== SCHEDULERS ========================

    static async Task ScheduleDailyPoll(ITelegramBotClient bot)
    {
        while (true)
        {
            try
            {
                var now = DateTime.UtcNow.AddHours(5); // Tashkent
                var next = new DateTime(now.Year, now.Month, now.Day, 9, 0, 0);
                if (next < now) next = next.AddDays(1);

                var delay = next - now;
                await Task.Delay(delay);

                foreach (var e in employees)
                {
                    await SendOfficePoll(bot, e.ChatId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в ScheduleDailyPoll: {ex.Message}");
            }
        }
    }

    static async Task ScheduleDailyReport(ITelegramBotClient bot)
    {
        while (true)
        {
            try
            {
                var now = DateTime.UtcNow.AddHours(5); // Tashkent
                var next = new DateTime(now.Year, now.Month, now.Day, 11, 0, 0);
                if (next < now) next = next.AddDays(1);

                var delay = next - now;
                await Task.Delay(delay);

                var report = new StringBuilder("📊 Отчёт по сотрудникам:\n");
                foreach (var e in employees)
                {
                    report.AppendLine($"{e.FIO}: Office={e.OfficeChoice}, Lunch={e.LunchChoice}");
                }

                await bot.SendMessage(AdminChannelId, report.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в ScheduleDailyReport: {ex.Message}");
            }
        }
    }
}
