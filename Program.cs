using System.Net.Http.Json;
using System.Text.Json;

namespace MessengerClient;

public class Program
{
    private static readonly HttpClient client = new();
    private static string? serverUrl = "http://localhost:5267";
    private static string? jwtToken;
    private static DateTime tokenExpires;
    private static string? currentUserEmail;
    private static string? currentUserId;

    public static async Task Main(string[] args)
    {
        Console.Title = "Messenger JWT Client";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Messenger JWT Authentication Client ===\n");
        Console.ResetColor();

        while (true)
        {
            ShowMenu();
            var choice = Console.ReadKey(true).KeyChar;

            Console.Clear();
            try
            {
                switch (choice)
                {
                    case '1':
                        await RegisterAsync();
                        break;
                    case '2':
                        await LoginAsync();
                        break;
                    case '3':
                        await TestProtectedEndpointAsync();
                        break;
                    case '4':
                        await TestPublicEndpointAsync();
                        break;
                    case '5':
                        ShowTokenInfo();
                        break;
                    case '0':
                        Console.WriteLine("Выход из программы...");
                        return;
                    default:
                        Console.WriteLine("Неверный выбор. Попробуйте снова.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nОшибка: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey(true);
            Console.Clear();
        }
    }

    private static void ShowMenu()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("=== Меню ===");
        Console.ResetColor();
        
        if (string.IsNullOrEmpty(jwtToken))
        {
            Console.WriteLine("1. Регистрация");
            Console.WriteLine("2. Вход");
        }
        else
        {
            Console.WriteLine($"[Авторизован как: {currentUserEmail} (ID: {currentUserId})]");
            Console.WriteLine("3. Тест защищенного эндпоинта");
        }
        
        Console.WriteLine("4. Тест публичного эндпоинта");
        Console.WriteLine("5. Показать информацию о токене");
        Console.WriteLine("0. Выход");
        Console.Write("\nВыберите действие: ");
    }

    private static async Task RegisterAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Регистрация ===\n");
        Console.ResetColor();

        Console.Write("Email: ");
        var email = Console.ReadLine()?.Trim();

        Console.Write("Пароль (минимум 6 символов): ");
        var password = ReadPassword();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Email и пароль обязательны!");
            Console.ResetColor();
            return;
        }

        var registerData = new
        {
            email = email,
            password = password
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"{serverUrl}/api/auth/register", 
                registerData
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                jwtToken = result.GetProperty("token").GetString();
                tokenExpires = result.GetProperty("expires").GetDateTime();
                currentUserEmail = result.GetProperty("email").GetString();
                currentUserId = result.GetProperty("userId").GetString();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Регистрация успешна!");
                Console.WriteLine($"User ID: {currentUserId}");
                Console.WriteLine($"Email: {currentUserEmail}");
                Console.WriteLine($"Token: {jwtToken.Substring(0, 50)}...");
                Console.WriteLine($"Expires: {tokenExpires}");
                Console.ResetColor();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Ошибка регистрации: {response.StatusCode}");
                Console.WriteLine($"Детали: {error}");
                Console.ResetColor();
            }
        }
        catch (HttpRequestException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Не удалось подключиться к серверу: {ex.Message}");
            Console.WriteLine($"Убедитесь, что сервер запущен на {serverUrl}");
            Console.ResetColor();
        }
    }

    private static async Task LoginAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Вход ===\n");
        Console.ResetColor();

        Console.Write("Email: ");
        var email = Console.ReadLine()?.Trim();

        Console.Write("Пароль: ");
        var password = ReadPassword();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Email и пароль обязательны!");
            Console.ResetColor();
            return;
        }

        var loginData = new
        {
            email = email,
            password = password
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"{serverUrl}/api/auth/login", 
                loginData
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                jwtToken = result.GetProperty("token").GetString();
                tokenExpires = result.GetProperty("expires").GetDateTime();
                currentUserEmail = result.GetProperty("email").GetString();
                currentUserId = result.GetProperty("userId").GetString();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Вход выполнен успешно!");
                Console.WriteLine($"User ID: {currentUserId}");
                Console.WriteLine($"Email: {currentUserEmail}");
                Console.WriteLine($"Token: {jwtToken.Substring(0, 50)}...");
                Console.WriteLine($"Expires: {tokenExpires}");
                Console.ResetColor();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Ошибка входа: {response.StatusCode}");
                Console.WriteLine($"Детали: {error}");
                Console.ResetColor();
            }
        }
        catch (HttpRequestException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n✗ Не удалось подключиться к серверу: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task TestProtectedEndpointAsync()
    {
       

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Запрос к защищенному эндпоинту ===\n");
        Console.ResetColor();

        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.GetAsync($"{serverUrl}/api/test/protected");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Запрос успешен!");
                Console.WriteLine($"Message: {result.GetProperty("message").GetString()}");
                Console.WriteLine($"User ID: {result.GetProperty("userId").GetString()}");
                Console.WriteLine($"Email: {result.GetProperty("email").GetString()}");
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Токен недействителен или истек. Необходимо войти заново.");
                jwtToken = null;
                currentUserEmail = null;
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode}");
                Console.ResetColor();
            }
        }
        catch (HttpRequestException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Ошибка подключения: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static async Task TestPublicEndpointAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Запрос к публичному эндпоинту ===\n");
        Console.ResetColor();

        try
        {
            var response = await client.GetAsync($"{serverUrl}/api/test/public");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Запрос успешен!");
                Console.WriteLine($"Message: {result.GetProperty("message").GetString()}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode}");
                Console.ResetColor();
            }
        }
        catch (HttpRequestException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Ошибка подключения: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void ShowTokenInfo()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Информация о токене ===\n");
        Console.ResetColor();

        if (string.IsNullOrEmpty(jwtToken))
        {
            Console.WriteLine("Токен отсутствует. Необходимо войти или зарегистрироваться.");
        }
        else
        {
            Console.WriteLine($"Token: {jwtToken}");
            Console.WriteLine($"Expires: {tokenExpires}");
            Console.WriteLine($"Time left: {(tokenExpires - DateTime.UtcNow).TotalHours:F1} hours");
            Console.WriteLine($"User: {currentUserEmail} (ID: {currentUserId})");
        }
    }

    private static string ReadPassword()
    {
        var password = string.Empty;
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(intercept: true);
            
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                Console.Write("\b \b");
                password = password[0..^1];
            }
            else if (!char.IsControl(key.KeyChar))
            {
                Console.Write("*");
                password += key.KeyChar;
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password;
    }
}
