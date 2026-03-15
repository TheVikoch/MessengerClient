using System.Net.Http.Json;
using System.Text.Json;
using MessengerClient.Models.DTOs;

namespace MessengerClient;

public class Program
{
    private static readonly HttpClient client = new();
    private static string? serverUrl = "http://127.0.0.1:5267";
    private static string? jwtToken;
    private static DateTime tokenExpires;
    private static string? currentUserEmail;
    private static string? currentUserId;
    private static string? refreshToken;
    private static string? currentSessionId;

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
                    case '6':
                        await ShowSessionsAsync();
                        break;
                    case '7':
                        await RevokeSessionInteractiveAsync();
                        break;
                    case '8':
                        await ShowChatMenuAsync();
                        break;
                    case '9':
                        await LogoutAsync();
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
            Console.WriteLine("3. Тест защищенного эндпоинта");
            Console.WriteLine("4. Тест публичного эндпоинта");
            Console.WriteLine("5. Показать информацию о токене");
            Console.WriteLine("0. Выход");
        }
        else
        {
            Console.WriteLine($"[Авторизован как: {currentUserEmail} (ID: {currentUserId})]");
            Console.WriteLine("3. Тест защищенного эндпоинта");
            Console.WriteLine("4. Тест публичного эндпоинта");
            Console.WriteLine("5. Показать информацию о токене");
            Console.WriteLine("6. Показать сессии");
            Console.WriteLine("7. Отозвать сессию");
            Console.WriteLine("8. Меню чатов");
            Console.WriteLine("9. Выйти (revoke текущую сессию)");
            Console.WriteLine("0. Выход");
        }

        
        Console.Write("\nВыберите действие: ");
    }

    private static async Task ShowSessionsAsync()
    {
        if (string.IsNullOrEmpty(jwtToken) || string.IsNullOrEmpty(currentUserId))
        {
            Console.WriteLine("Необходимо войти, чтобы просмотреть сессии.");
            return;
        }

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.GetAsync($"{serverUrl}/api/auth/sessions/{currentUserId}");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Ошибка получения сессий: {response.StatusCode}");
                return;
            }

            var sessions = await response.Content.ReadFromJsonAsync<JsonElement[]>();
            Console.WriteLine($"Найдено сессий: {sessions?.Length ?? 0}");
            if (sessions == null || sessions.Length == 0) return;

            for (int i = 0; i < sessions.Length; i++)
            {
                var s = sessions[i];
                var id = s.GetProperty("id").GetString();
                var dev = s.TryGetProperty("deviceInfo", out var d) ? d.GetString() : string.Empty;
                var ip = s.TryGetProperty("ip", out var ipr) ? ipr.GetString() : string.Empty;
                var expires = s.TryGetProperty("expiresAt", out var ex) ? ex.GetDateTime() : DateTime.MinValue;
                var revoked = s.TryGetProperty("isRevoked", out var rv) ? rv.GetBoolean() : false;
                var created = s.TryGetProperty("createdAt", out var ca) ? ca.GetDateTime() : DateTime.MinValue;
                Console.WriteLine($"[{i}] Id: {id}\n    Device: {dev}\n    IP: {ip}\n    Created: {created}\n    Expires: {expires}\n    Revoked: {revoked}\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static async Task RevokeSessionInteractiveAsync()
    {
        await ShowSessionsAsync();
        Console.Write("Введите индекс сессии для отзыва (или пусто чтобы отменить): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) return;
        if (!int.TryParse(input, out var idx))
        {
            Console.WriteLine("Неверный индекс");
            return;
        }

        // get sessions again to pick id
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        try
        {
            var response = await client.GetAsync($"{serverUrl}/api/auth/sessions/{currentUserId}");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Ошибка получения сессий: {response.StatusCode}");
                return;
            }

            var sessions = await response.Content.ReadFromJsonAsync<JsonElement[]>();
            if (sessions == null || idx < 0 || idx >= sessions.Length)
            {
                Console.WriteLine("Индекс вне диапазона");
                return;
            }

            var sessionId = sessions[idx].GetProperty("id").GetString();
            if (string.IsNullOrEmpty(sessionId))
            {
                Console.WriteLine("Не удалось получить id сессии");
                return;
            }

            var payload = new
            {
                sessionId = Guid.Parse(sessionId),
                userId = Guid.Parse(currentUserId!)
            };

            var revokeResp = await client.PostAsJsonAsync($"{serverUrl}/api/auth/sessions/revoke", payload);
            if (revokeResp.IsSuccessStatusCode)
            {
                Console.WriteLine("Сессия успешно отозвана.");
                // if revoked current session, clear local auth
                if (currentSessionId == sessionId)
                {
                    ClearLocalAuth();
                }
            }
            else
            {
                var err = await revokeResp.Content.ReadAsStringAsync();
                Console.WriteLine($"Ошибка отзыва сессии: {revokeResp.StatusCode} - {err}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static async Task LogoutAsync()
    {
        if (string.IsNullOrEmpty(jwtToken))
        {
            Console.WriteLine("Вы не авторизованы.");
            return;
        }

        if (string.IsNullOrEmpty(currentSessionId))
        {
            // just clear local
            ClearLocalAuth();
            Console.WriteLine("Локальный выход выполнен (сессия не найдена на клиенте).");
            return;
        }

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        try
        {
            var payload = new
            {
                sessionId = Guid.Parse(currentSessionId),
                userId = Guid.Parse(currentUserId!)
            };

            var revokeResp = await client.PostAsJsonAsync($"{serverUrl}/api/auth/sessions/revoke", payload);
            if (revokeResp.IsSuccessStatusCode)
            {
                Console.WriteLine("Текущая сессия отозвана на сервере. Выход выполнен.");
            }
            else
            {
                Console.WriteLine($"Не удалось отозвать сессию на сервере: {revokeResp.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            ClearLocalAuth();
            client.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static void ClearLocalAuth()
    {
        jwtToken = null;
        tokenExpires = default;
        currentUserEmail = null;
        currentUserId = null;
        refreshToken = null;
        currentSessionId = null;
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
                // new server returns refresh token and session id on register
                refreshToken = result.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
                currentSessionId = result.TryGetProperty("sessionId", out var sid) ? sid.GetString() : null;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Регистрация успешна!");
                Console.WriteLine($"User ID: {currentUserId}");
                Console.WriteLine($"Email: {currentUserEmail}");
                Console.WriteLine($"Token: {(jwtToken?.Length > 50 ? jwtToken.Substring(0, 50) : jwtToken)}...");
                Console.WriteLine($"Expires: {tokenExpires}");
                if (!string.IsNullOrEmpty(refreshToken)) Console.WriteLine($"RefreshToken: {refreshToken.Substring(0, 50)}...");
                if (!string.IsNullOrEmpty(currentSessionId)) Console.WriteLine($"SessionId: {currentSessionId}");
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
                // include device info and ip so server can create a descriptive session
                new
                {
                    email = email,
                    password = password,
                    deviceInfo = Environment.MachineName + " / " + Environment.OSVersion.VersionString,
                    ip = GetLocalIpAddress()
                }
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                jwtToken = result.GetProperty("token").GetString();
                tokenExpires = result.GetProperty("expires").GetDateTime();
                currentUserEmail = result.GetProperty("email").GetString();
                currentUserId = result.GetProperty("userId").GetString();
                // server now returns refresh token and session id on login
                refreshToken = result.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
                currentSessionId = result.TryGetProperty("sessionId", out var sid) ? sid.GetString() : null;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Вход выполнен успешно!");
                Console.WriteLine($"User ID: {currentUserId}");
                Console.WriteLine($"Email: {currentUserEmail}");
                Console.WriteLine($"Token: {(jwtToken?.Length > 50 ? jwtToken.Substring(0, 50) : jwtToken)}...");
                Console.WriteLine($"Expires: {tokenExpires}");
                if (!string.IsNullOrEmpty(refreshToken)) Console.WriteLine($"RefreshToken: {refreshToken.Substring(0, 50)}...");
                if (!string.IsNullOrEmpty(currentSessionId)) Console.WriteLine($"SessionId: {currentSessionId}");
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

    private static string GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var addr in host.AddressList)
            {
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return addr.ToString();
            }
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    // ============ Chat Methods ============

    private static async Task ShowChatMenuAsync()
    {
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== Меню чатов ===\n");
            Console.ResetColor();

            if (string.IsNullOrEmpty(jwtToken))
            {
                Console.WriteLine("Необходимо войти для доступа к чатам.");
                return;
            }

            Console.WriteLine("1. Создать личный чат");
            Console.WriteLine("2. Создать групповой чат");
            Console.WriteLine("3. Просмотреть все чаты");
            Console.WriteLine("4. Просмотреть чат по ID");
            Console.WriteLine("5. Добавить участника в группу");
            Console.WriteLine("6. Удалить участника из группы");
            Console.WriteLine("0. Назад в главное меню");

            Console.Write("\nВыберите действие: ");
            var choice = Console.ReadKey(true).KeyChar;
            Console.Clear();

            try
            {
                switch (choice)
                {
                    case '1':
                        await CreatePersonalChatAsync();
                        break;
                    case '2':
                        await CreateGroupChatAsync();
                        break;
                    case '3':
                        await GetAllConversationsAsync();
                        break;
                    case '4':
                        await GetConversationAsync();
                        break;
                    case '5':
                        await AddMemberToGroupAsync();
                        break;
                    case '6':
                        await RemoveMemberFromGroupAsync();
                        break;
                    case '0':
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
        }
    }

    private static async Task CreatePersonalChatAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Создание личного чата ===\n");
        Console.ResetColor();

        Console.Write("Введите email пользователя: ");
        var userEmail = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(userEmail))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Email обязателен!");
            Console.ResetColor();
            return;
        }

        var dto = new CreatePersonalChatDto
        {
            UserEmail = userEmail
        };

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.PostAsJsonAsync($"{serverUrl}/api/chat/personal", dto);

            if (response.IsSuccessStatusCode)
            {
                var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Личный чат создан (или уже существует)!");
                PrintConversation(conversation);
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Пользователь не найден: {error}");
                Console.ResetColor();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode} - {error}");
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

    private static async Task CreateGroupChatAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Создание группового чата ===\n");
        Console.ResetColor();

        Console.Write("Введите название группы: ");
        var name = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Название группы обязательно!");
            Console.ResetColor();
            return;
        }

        Console.Write("Введите email участников (через запятую): ");
        var emailsInput = Console.ReadLine()?.Trim();
        var memberEmails = new List<string>();

        if (!string.IsNullOrEmpty(emailsInput))
        {
            memberEmails = emailsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var dto = new CreateGroupChatDto
        {
            Name = name,
            MemberEmails = memberEmails
        };

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.PostAsJsonAsync($"{serverUrl}/api/chat/group", dto);

            if (response.IsSuccessStatusCode)
            {
                var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Групповой чат создан!");
                PrintConversation(conversation);
                Console.ResetColor();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode} - {error}");
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

    private static async Task GetAllConversationsAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Все ваши чаты ===\n");
        Console.ResetColor();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.GetAsync($"{serverUrl}/api/chat");

            if (response.IsSuccessStatusCode)
            {
                var conversations = await response.Content.ReadFromJsonAsync<List<ConversationDto>>();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Найдено чатов: {conversations?.Count ?? 0}\n");
                Console.ResetColor();

                if (conversations != null && conversations.Count > 0)
                {
                    foreach (var conv in conversations)
                    {
                        PrintConversation(conv);
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("У вас пока нет чатов.");
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode} - {error}");
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

    private static async Task GetConversationAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Просмотр чата ===\n");
        Console.ResetColor();

        Console.Write("Введите ID чата: ");
        var conversationId = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(conversationId) || !Guid.TryParse(conversationId, out _))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Неверный ID чата!");
            Console.ResetColor();
            return;
        }

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.GetAsync($"{serverUrl}/api/chat/{conversationId}");

            if (response.IsSuccessStatusCode)
            {
                var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Чат найден!");
                PrintConversation(conversation);
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Чат не найден.");
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ У вас нет доступа к этому чату.");
                Console.ResetColor();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode} - {error}");
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

    private static async Task AddMemberToGroupAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Добавление участника в группу ===\n");
        Console.ResetColor();

        Console.Write("Введите ID чата (группы): ");
        var conversationId = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(conversationId) || !Guid.TryParse(conversationId, out _))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Неверный ID чата!");
            Console.ResetColor();
            return;
        }

        Console.Write("Введите email пользователя для добавления: ");
        var userEmail = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(userEmail))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Email обязателен!");
            Console.ResetColor();
            return;
        }

        var dto = new AddMemberDto
        {
            UserEmail = userEmail
        };

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.PostAsJsonAsync($"{serverUrl}/api/chat/{conversationId}/members", dto);

            if (response.IsSuccessStatusCode)
            {
                var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Участник добавлен!");
                PrintConversation(conversation);
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Чат или пользователь не найден: {error}");
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ У вас нет прав для добавления участников (только создатель/админ).");
                Console.ResetColor();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode} - {error}");
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

    private static async Task RemoveMemberFromGroupAsync()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Удаление участника из группы ===\n");
        Console.ResetColor();

        Console.Write("Введите ID чата (группы): ");
        var conversationId = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(conversationId) || !Guid.TryParse(conversationId, out _))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Неверный ID чата!");
            Console.ResetColor();
            return;
        }

        Console.Write("Введите ID пользователя для удаления: ");
        var userIdToRemove = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(userIdToRemove) || !Guid.TryParse(userIdToRemove, out _))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Неверный ID пользователя!");
            Console.ResetColor();
            return;
        }

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);

        try
        {
            var response = await client.DeleteAsync($"{serverUrl}/api/chat/{conversationId}/members/{userIdToRemove}");

            if (response.IsSuccessStatusCode)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Участник удален из группы!");
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Чат или участник не найден.");
                Console.ResetColor();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ У вас нет прав для удаления участников (только создатель).");
                Console.ResetColor();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка: {response.StatusCode} - {error}");
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

    private static void PrintConversation(ConversationDto? conversation)
    {
        if (conversation == null)
        {
            Console.WriteLine("Чат не найден.");
            return;
        }

        Console.WriteLine($"ID: {conversation.Id}");
        Console.WriteLine($"Тип: {conversation.Type}");
        if (!string.IsNullOrEmpty(conversation.Name))
            Console.WriteLine($"Название: {conversation.Name}");
        Console.WriteLine($"Создан: {conversation.CreatedAt}");
        if (conversation.LastMessageAt.HasValue)
            Console.WriteLine($"Последнее сообщение: {conversation.LastMessageAt.Value}");
        Console.WriteLine($"Удален: {(conversation.IsDeleted ? "Да" : "Нет")}");
        Console.WriteLine($"Участников: {conversation.Members?.Count ?? 0}");

        if (conversation.Members != null && conversation.Members.Count > 0)
        {
            Console.WriteLine("\nУчастники:");
            foreach (var member in conversation.Members)
            {
                Console.WriteLine($"  - {member.User?.Email ?? "N/A"} (ID: {member.UserId}, Роль: {member.Role}, Присоединился: {member.JoinedAt})");
            }
        }
    }
}
