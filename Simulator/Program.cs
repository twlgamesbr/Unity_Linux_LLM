using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

namespace Simulator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Simulating NPCDialogueManager Loop...");
            var logPath = "../dialogue_simulation.log";
            var logBuilder = new StringBuilder();
            Action<string> Log = (msg) => {
                Console.WriteLine(msg);
                logBuilder.AppendLine(msg);
            };

            Log("--- NPC DIALOGUE SIMULATION ---");

            var systemPrompt = @"You are the Welltodo mansion's AI Butler.
Stay in character as a dry, sharp, highly observant butler with a sarcastic edge.
Be articulate, concise when appropriate, and slightly condescending without becoming hostile.
Answer from the Butler's point of view, never as a generic assistant.
If you do not know something, say so in character instead of inventing facts.
Treat retrieved mansion facts as the authoritative source of truth.";

            var playerMessage = "Hello Butler, what should I investigate first?";
            
            Log($"[System Prompt]: {systemPrompt}");
            Log($"[Player]: {playerMessage}");
            Log("[LLM Processing...]");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            var requestBody = new {
                model = "llama3.1:8b",
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = playerMessage }
                },
                stream = false,
                options = new {
                    temperature = 0.72,
                    top_p = 0.9,
                    top_k = 40,
                    num_predict = 180
                }
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("http://127.0.0.1:11434/api/chat", content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var responseJson = JsonSerializer.Deserialize<JsonElement>(responseString);
                var botMessage = responseJson.GetProperty("message").GetProperty("content").GetString();

                Log($"[Butler]: {botMessage}");
                Log("--- SIMULATION COMPLETE ---");
                
                File.WriteAllText(logPath, logBuilder.ToString());
            }
            catch (Exception ex)
            {
                Log($"[Error]: {ex.Message}");
            }
        }
    }
}
