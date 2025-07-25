using System;
using System.IO;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();

    private static string onlineUrl;
    private static string localUrl;
    private static string activeModel;
    private static bool online;
    private static string localApiKey;
    private static string onlineApiKey;

    static async Task Main(string[] args)
    {
        LoadSettings();
        CancellationTokenSource cts = new CancellationTokenSource();

        while (true)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("You: ");
            Console.ResetColor();
            string message = Console.ReadLine();

            string result = await SendMessageToAI(message, cts.Token);
            Console.ResetColor();
            Console.WriteLine();
        }
    }

    private static void LoadSettings()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var config = builder.Build().GetSection("AiSettings");

        onlineUrl = config["OnlineUrl"];
        localUrl = config["LocalUrl"];
        activeModel = config["ActiveModel"];
        online = bool.Parse(config["Online"]!);
        localApiKey = config["LocalApiKey"];
        onlineApiKey = config["OnlineApiKey"];
    }

    public static async Task<string> SendMessageToAI(string message, CancellationToken cancellationToken)
    {
        string systemPrompt = "You are an AI assistant. Respond concisely.";
        string prompt = message;

        var requestBody = new
        {
            model = activeModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = prompt }
            },
            max_tokens = 500,
            stream = true
        };

        string jsonRequest = JsonConvert.SerializeObject(requestBody);
        string url = online ? onlineUrl : localUrl;
        string apiKey = online ? onlineApiKey : localApiKey;

        if (httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            httpClient.DefaultRequestHeaders.Remove("Authorization");
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var responseStream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(responseStream);

                string result = "";
                string line;

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("AI: ");
                Console.ResetColor();
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (line.StartsWith("data:"))
                    {
                        string jsonLine = line.Substring(5).Trim();
                        if (jsonLine == "[DONE]") break;

                        if (!string.IsNullOrWhiteSpace(jsonLine))
                        {
                            dynamic responseData = JsonConvert.DeserializeObject(jsonLine);
                            string text = responseData?.choices[0]?.delta?.content?.ToString() ?? "";


                            if (!string.IsNullOrEmpty(text))
                            {
                                result += text;
                                Console.Write(text); // نمایش تدریجی
                            }
                        }
                    }
                }

                return result.Trim();
            }
            else
            {
                string errorMessage = await response.Content.ReadAsStringAsync();
                return $"Error: {response.StatusCode}\n{errorMessage}";
            }
        }
        catch (Exception ex)
        {
            return $"Exception: {ex.Message}";
        }
    }
}
