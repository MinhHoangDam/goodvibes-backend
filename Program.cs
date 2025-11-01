using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Linq;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault if in production
var keyVaultUrl = builder.Configuration["KeyVaultUrl"];
if (!string.IsNullOrEmpty(keyVaultUrl))
{
    Console.WriteLine($"🔐 Configuring Key Vault: {keyVaultUrl}");

    // Use ManagedIdentityCredential for Azure (works with both system-assigned and user-assigned)
    // For local development, falls back to DefaultAzureCredential (uses Azure CLI, VS, etc.)
    var credential = new DefaultAzureCredential();

    Console.WriteLine("✓ Using System-Assigned Managed Identity in Azure (or DefaultAzureCredential locally)");

    var secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
    builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());

    Console.WriteLine("✓ Key Vault configuration completed");
}

// Configure port for deployment - OVERRIDE default behavior
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
Console.WriteLine($"Starting server on port: {port}");
Console.WriteLine($"PORT environment variable: {Environment.GetEnvironmentVariable("PORT")}");

// Force Kestrel to listen on the correct port and interface
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(int.Parse(port));
});

// Also set via UseUrls as backup
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<GoodVibesCacheService>();
builder.Services.AddHostedService<GoodVibesCacheService>(provider => provider.GetRequiredService<GoodVibesCacheService>());
builder.Services.AddScoped<UserCacheService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Get API key from configuration (Key Vault in production, appsettings in development)
// In Azure Key Vault, the secret is named "WorkleapCOToken"
// Locally, we use "OfficevibeApiKey" from appsettings.json
var OFFICEVIBE_API_KEY = app.Configuration["WorkleapCOToken"] ?? app.Configuration["OfficevibeApiKey"] ?? "1dfdd5f52b3c4829adc15e794485eea6";
const string OFFICEVIBE_API_URL = "https://api.workleap.com/officevibe/goodvibes";

Console.WriteLine($"Using Officevibe API Key: {OFFICEVIBE_API_KEY[..Math.Min(8, OFFICEVIBE_API_KEY.Length)]}...");

// Health check endpoint
app.MapGet("/health", () => 
{
    return Results.Ok(new { status = "ok", message = "Server is running" });
});

// Debug endpoint to see environment
app.MapGet("/debug", () =>
{
    return Results.Ok(new {
        port = Environment.GetEnvironmentVariable("PORT") ?? "not set",
        environment = app.Environment.EnvironmentName,
        urls = string.Join(", ", app.Urls)
    });
});

// Key Vault validation endpoint - Test if we can access Key Vault secrets
app.MapGet("/api/validate-keyvault", () =>
{
    try
    {
        var keyVaultUrl = app.Configuration["KeyVaultUrl"];
        var hasKeyVault = !string.IsNullOrEmpty(keyVaultUrl);

        // Try to read the API key from configuration
        // If Key Vault is configured, this will attempt to read from it
        var apiKeyFromConfig = app.Configuration["WorkleapCOToken"];
        var hasApiKey = !string.IsNullOrEmpty(apiKeyFromConfig);

        // Fallback check
        var fallbackApiKey = app.Configuration["OfficevibeApiKey"];
        var hasFallback = !string.IsNullOrEmpty(fallbackApiKey);

        var result = new
        {
            success = hasApiKey || hasFallback,
            keyVaultConfigured = hasKeyVault,
            keyVaultUrl = keyVaultUrl ?? "not configured",
            apiKeySource = hasApiKey ? "WorkleapCOToken (Key Vault)" :
                          hasFallback ? "OfficevibeApiKey (local config)" :
                          "none - API key not found!",
            apiKeyFound = hasApiKey || hasFallback,
            apiKeyPreview = (hasApiKey || hasFallback)
                ? $"{(apiKeyFromConfig ?? fallbackApiKey)![..Math.Min(8, (apiKeyFromConfig ?? fallbackApiKey)!.Length)]}..."
                : "NOT FOUND",
            environment = app.Environment.EnvironmentName,
            timestamp = DateTime.UtcNow
        };

        if (!result.success)
        {
            return Results.Json(result, statusCode: 500);
        }

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            success = false,
            error = ex.Message,
            errorType = ex.GetType().Name,
            stackTrace = ex.StackTrace,
            timestamp = DateTime.UtcNow
        }, statusCode: 500);
    }
});

// Good Vibes list endpoint
app.MapGet("/api/good-vibes", async (HttpClient httpClient, UserCacheService userCache, bool? isPublic, int? limit, string? continuationToken) =>
{
    try
    {
        // Build URL with optional query parameters
        var url = OFFICEVIBE_API_URL;
        var queryParams = new List<string>();
        
        if (isPublic.HasValue)
        {
            queryParams.Add($"isPublic={isPublic.Value.ToString().ToLower()}");
        }
        
        if (limit.HasValue)
        {
            queryParams.Add($"limit={limit.Value}");
        }
        
        if (!string.IsNullOrEmpty(continuationToken))
        {
            queryParams.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        }
        
        if (queryParams.Any())
        {
            url += "?" + string.Join("&", queryParams);
        }
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("workleap-subscription-key", OFFICEVIBE_API_KEY);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        var response = await httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return Results.Problem(
                detail: $"Officevibe API error: {response.StatusCode}. {errorContent}",
                statusCode: (int)response.StatusCode
            );
        }

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDocument = JsonDocument.Parse(content);
        
        // Clone the root element so we can modify it
        var root = jsonDocument.RootElement.Clone();
        
        // Get the data array
        if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            var enrichedData = new List<Dictionary<string, object>>();
            
            foreach (var item in dataArray.EnumerateArray())
            {
                var dict = new Dictionary<string, object>();
                
                // Copy all existing properties
                foreach (var prop in item.EnumerateObject())
                {
                    dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText())!;
                }
                
                // Enrich sender user with avatar
                if (item.TryGetProperty("senderUser", out var senderUser))
                {
                    if (senderUser.TryGetProperty("userId", out var senderUserId))
                    {
                        var senderDict = JsonSerializer.Deserialize<Dictionary<string, object>>(senderUser.GetRawText())!;
                        var userId = senderUserId.GetString()!;
                        var avatarUrl = await userCache.GetUserAvatarAsync(userId);

                        // Always add avatarUrl property (null if fetch failed)
                        senderDict["avatarUrl"] = (object?)avatarUrl ?? "";
                        dict["senderUser"] = senderDict;
                    }
                }
                
                // Enrich recipients with avatars
                if (item.TryGetProperty("recipients", out var recipients) && recipients.ValueKind == JsonValueKind.Array)
                {
                    var enrichedRecipients = new List<Dictionary<string, object>>();
                    foreach (var recipient in recipients.EnumerateArray())
                    {
                        if (recipient.TryGetProperty("userId", out var recipientUserId))
                        {
                            var recipientDict = JsonSerializer.Deserialize<Dictionary<string, object>>(recipient.GetRawText())!;
                            var userId = recipientUserId.GetString()!;
                            var avatarUrl = await userCache.GetUserAvatarAsync(userId);

                            // Always add avatarUrl property (empty string if fetch failed)
                            recipientDict["avatarUrl"] = (object?)avatarUrl ?? "";
                            enrichedRecipients.Add(recipientDict);
                        }
                    }
                    dict["recipients"] = enrichedRecipients;
                }
                
                enrichedData.Add(dict);
            }
            
            // Build response with enriched data
            var responseData = new Dictionary<string, object>
            {
                ["data"] = enrichedData
            };
            
            // Copy metadata if it exists
            if (root.TryGetProperty("metadata", out var metadata))
            {
                responseData["metadata"] = JsonSerializer.Deserialize<object>(metadata.GetRawText())!;
            }
            
            return Results.Ok(responseData);
        }
        
        return Results.Ok(root);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch Good Vibes: {ex.Message}",
            statusCode: 500
        );
    }
});

// Get a single Good Vibe with replies
app.MapGet("/api/good-vibes/{goodVibeId}", async (HttpClient httpClient, UserCacheService userCache, string goodVibeId) =>
{
    try
    {
        var url = $"{OFFICEVIBE_API_URL}/{goodVibeId}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("workleap-subscription-key", OFFICEVIBE_API_KEY);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        var response = await httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return Results.Problem(
                detail: $"Officevibe API error: {response.StatusCode}. {errorContent}",
                statusCode: (int)response.StatusCode
            );
        }

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement.Clone();
        
        // Enrich with avatars
        var dict = new Dictionary<string, object>();
        
        // Copy all existing properties
        foreach (var prop in root.EnumerateObject())
        {
            dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText())!;
        }
        
        // Enrich sender user with avatar
        if (root.TryGetProperty("senderUser", out var senderUser))
        {
            if (senderUser.TryGetProperty("userId", out var senderUserId))
            {
                var senderDict = JsonSerializer.Deserialize<Dictionary<string, object>>(senderUser.GetRawText())!;
                var userId = senderUserId.GetString()!;
                var avatarUrl = await userCache.GetUserAvatarAsync(userId);

                // Always add avatarUrl property (empty string if fetch failed)
                senderDict["avatarUrl"] = (object?)avatarUrl ?? "";
                dict["senderUser"] = senderDict;
            }
        }

        // Enrich recipients with avatars
        if (root.TryGetProperty("recipients", out var recipients) && recipients.ValueKind == JsonValueKind.Array)
        {
            var enrichedRecipients = new List<Dictionary<string, object>>();
            foreach (var recipient in recipients.EnumerateArray())
            {
                if (recipient.TryGetProperty("userId", out var recipientUserId))
                {
                    var recipientDict = JsonSerializer.Deserialize<Dictionary<string, object>>(recipient.GetRawText())!;
                    var userId = recipientUserId.GetString()!;
                    var avatarUrl = await userCache.GetUserAvatarAsync(userId);

                    // Always add avatarUrl property (empty string if fetch failed)
                    recipientDict["avatarUrl"] = (object?)avatarUrl ?? "";
                    enrichedRecipients.Add(recipientDict);
                }
            }
            dict["recipients"] = enrichedRecipients;
        }
        
        // Enrich replies with avatars
        if (root.TryGetProperty("replies", out var replies) && replies.ValueKind == JsonValueKind.Array)
        {
            var enrichedReplies = new List<Dictionary<string, object>>();
            foreach (var reply in replies.EnumerateArray())
            {
                var replyDict = JsonSerializer.Deserialize<Dictionary<string, object>>(reply.GetRawText())!;
                
                // Enrich authorUser (not senderUser for replies)
                if (reply.TryGetProperty("authorUser", out var replyAuthor))
                {
                    if (replyAuthor.TryGetProperty("userId", out var replyAuthorUserId))
                    {
                        var avatarUrl = await userCache.GetUserAvatarAsync(replyAuthorUserId.GetString()!);
                        if (avatarUrl != null)
                        {
                            var replyAuthorDict = JsonSerializer.Deserialize<Dictionary<string, object>>(replyAuthor.GetRawText())!;
                            replyAuthorDict["avatarUrl"] = avatarUrl;
                            replyDict["authorUser"] = replyAuthorDict;
                        }
                    }
                }
                enrichedReplies.Add(replyDict);
            }
            dict["replies"] = enrichedReplies;
        }
        
        return Results.Ok(dict);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch Good Vibe: {ex.Message}",
            statusCode: 500
        );
    }
});

// Get Good Vibes Collections (custom prompts)
app.MapGet("/api/good-vibes/collections", async (HttpClient httpClient) =>
{
    try
    {
        var url = $"{OFFICEVIBE_API_URL}/collections";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("workleap-subscription-key", OFFICEVIBE_API_KEY);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        var response = await httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return Results.Problem(
                detail: $"Officevibe API error: {response.StatusCode}. {errorContent}",
                statusCode: (int)response.StatusCode
            );
        }

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        
        return Results.Ok(jsonDocument.RootElement);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch Good Vibes Collections: {ex.Message}",
            statusCode: 500
        );
    }
});

// Get user information including avatar
app.MapGet("/api/users/{userId}", async (HttpClient httpClient, string userId) =>
{
    try
    {
        var url = $"https://api.workleap.com/public/users/{userId}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("workleap-subscription-key", OFFICEVIBE_API_KEY);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        var response = await httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return Results.Problem(
                detail: $"Workleap API error: {response.StatusCode}. {errorContent}",
                statusCode: (int)response.StatusCode
            );
        }

        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(content);
        
        return Results.Ok(jsonDocument.RootElement);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch user information: {ex.Message}",
            statusCode: 500
        );
    }
});

// Statistics endpoints
app.MapGet("/api/stats/top-senders", async (HttpClient httpClient, UserCacheService userCache, int? limit) =>
{
    try
    {
        var topLimit = limit ?? 10; // Default to top 10

        // Fetch all public good vibes (no pagination limit for accurate stats)
        var url = $"{OFFICEVIBE_API_URL}?isPublic=true&limit=1000";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("workleap-subscription-key", OFFICEVIBE_API_KEY);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return Results.Problem(
                detail: $"Officevibe API error: {response.StatusCode}. {errorContent}",
                statusCode: (int)response.StatusCode
            );
        }

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement.Clone();

        // Count good vibes sent by each user
        var senderCounts = new Dictionary<string, (int count, JsonElement user)>();

        if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.TryGetProperty("senderUser", out var senderUser))
                {
                    if (senderUser.TryGetProperty("userId", out var userId))
                    {
                        var userIdStr = userId.GetString()!;
                        if (senderCounts.TryGetValue(userIdStr, out var existing))
                        {
                            senderCounts[userIdStr] = (existing.count + 1, existing.user);
                        }
                        else
                        {
                            senderCounts[userIdStr] = (1, senderUser);
                        }
                    }
                }
            }
        }

        // Sort by count and take top N
        var topSenders = senderCounts
            .OrderByDescending(kvp => kvp.Value.count)
            .Take(topLimit)
            .Select(async kvp =>
            {
                var userDict = JsonSerializer.Deserialize<Dictionary<string, object>>(kvp.Value.user.GetRawText())!;
                var avatarUrl = await userCache.GetUserAvatarAsync(kvp.Key);
                if (avatarUrl != null)
                {
                    userDict["avatarUrl"] = avatarUrl;
                }

                return new Dictionary<string, object>
                {
                    ["user"] = userDict,
                    ["count"] = kvp.Value.count
                };
            })
            .ToList();

        // Wait for all avatar enrichments
        var enrichedTopSenders = await Task.WhenAll(topSenders);

        return Results.Ok(new { topSenders = enrichedTopSenders });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch top senders: {ex.Message}",
            statusCode: 500
        );
    }
});

app.MapGet("/api/stats/top-recipients", async (HttpClient httpClient, UserCacheService userCache, int? limit) =>
{
    try
    {
        var topLimit = limit ?? 10; // Default to top 10

        // Fetch all public good vibes (no pagination limit for accurate stats)
        var url = $"{OFFICEVIBE_API_URL}?isPublic=true&limit=1000";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("workleap-subscription-key", OFFICEVIBE_API_KEY);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            return Results.Problem(
                detail: $"Officevibe API error: {response.StatusCode}. {errorContent}",
                statusCode: (int)response.StatusCode
            );
        }

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDocument = JsonDocument.Parse(content);
        var root = jsonDocument.RootElement.Clone();

        // Count good vibes received by each user
        var recipientCounts = new Dictionary<string, (int count, JsonElement user)>();

        if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.TryGetProperty("recipients", out var recipients) && recipients.ValueKind == JsonValueKind.Array)
                {
                    foreach (var recipient in recipients.EnumerateArray())
                    {
                        if (recipient.TryGetProperty("userId", out var userId))
                        {
                            var userIdStr = userId.GetString()!;
                            if (recipientCounts.TryGetValue(userIdStr, out var existing))
                            {
                                recipientCounts[userIdStr] = (existing.count + 1, existing.user);
                            }
                            else
                            {
                                recipientCounts[userIdStr] = (1, recipient);
                            }
                        }
                    }
                }
            }
        }

        // Sort by count and take top N
        var topRecipients = recipientCounts
            .OrderByDescending(kvp => kvp.Value.count)
            .Take(topLimit)
            .Select(async kvp =>
            {
                var userDict = JsonSerializer.Deserialize<Dictionary<string, object>>(kvp.Value.user.GetRawText())!;
                var avatarUrl = await userCache.GetUserAvatarAsync(kvp.Key);
                if (avatarUrl != null)
                {
                    userDict["avatarUrl"] = avatarUrl;
                }

                return new Dictionary<string, object>
                {
                    ["user"] = userDict,
                    ["count"] = kvp.Value.count
                };
            })
            .ToList();

        // Wait for all avatar enrichments
        var enrichedTopRecipients = await Task.WhenAll(topRecipients);

        return Results.Ok(new { topRecipients = enrichedTopRecipients });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch top recipients: {ex.Message}",
            statusCode: 500
        );
    }
});

// Get top collections for a given month
app.MapGet("/api/stats/monthly/top-collections", async (
    int year,
    int month,
    int limit,
    GoodVibesCacheService cacheService) =>
{
    try
    {
        var topLimit = Math.Min(limit, 100); // Max 100 to prevent abuse
        var allVibes = await cacheService.GetAllVibesAsync();

        // Count collections by name for the specified month
        var collectionCounts = new Dictionary<string, int>();

        foreach (var item in allVibes)
        {
            if (item.TryGetProperty("creationDate", out var creationDateProp))
            {
                if (DateTime.TryParse(creationDateProp.GetString(), out var creationDate))
                {
                    if (creationDate.Year == year && creationDate.Month == month)
                    {
                        // Check if this good vibe has a collectionName (array of LocalizedText objects)
                        if (item.TryGetProperty("collectionName", out var collectionNameArray) &&
                            collectionNameArray.ValueKind == JsonValueKind.Array &&
                            collectionNameArray.GetArrayLength() > 0)
                        {
                            // Get the first localized text entry
                            var firstLocalizedText = collectionNameArray[0];
                            if (firstLocalizedText.TryGetProperty("text", out var textProp))
                            {
                                var collectionName = textProp.GetString();
                                if (!string.IsNullOrEmpty(collectionName))
                                {
                                    if (collectionCounts.ContainsKey(collectionName))
                                    {
                                        collectionCounts[collectionName]++;
                                    }
                                    else
                                    {
                                        collectionCounts[collectionName] = 1;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Sort by count and take top N
        var topCollections = collectionCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(topLimit)
            .Select(kvp => new Dictionary<string, object>
            {
                ["name"] = kvp.Key,
                ["count"] = kvp.Value
            })
            .ToList();

        return Results.Ok(new
        {
            topCollections = topCollections,
            year = year,
            month = month
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch top collections: {ex.Message}",
            statusCode: 500
        );
    }
});

// Get all available months with good vibes data
app.MapGet("/api/stats/available-months", async (GoodVibesCacheService cacheService) =>
{
    try
    {
        var allVibes = await cacheService.GetAllVibesAsync();

        // Collect all unique month/year combinations
        var months = new HashSet<(int year, int month)>();

        foreach (var item in allVibes)
        {
            if (item.TryGetProperty("creationDate", out var creationDateProp))
            {
                if (DateTime.TryParse(creationDateProp.GetString(), out var creationDate))
                {
                    months.Add((creationDate.Year, creationDate.Month));
                }
            }
        }

        // Sort by year and month descending (most recent first)
        var sortedMonths = months
            .OrderByDescending(m => m.year)
            .ThenByDescending(m => m.month)
            .Select(m => new
            {
                year = m.year,
                month = m.month,
                label = new DateTime(m.year, m.month, 1).ToString("MMMM yyyy")
            })
            .ToList();

        return Results.Ok(new { months = sortedMonths });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch available months: {ex.Message}",
            statusCode: 500
        );
    }
});

// Fast cached endpoint for good vibes (for carousel)
app.MapGet("/api/good-vibes/cached", async (GoodVibesCacheService cacheService, UserCacheService userCache) =>
{
    try
    {
        // Check if cache is ready
        if (!cacheService.IsReady)
        {
            return Results.Ok(new {
                data = new List<object>(),
                metadata = new { cacheReady = false }
            });
        }

        var allVibes = await cacheService.GetAllVibesAsync();

        // Enrich with avatars (using cache)
        var enrichedData = new List<Dictionary<string, object>>();

        foreach (var item in allVibes)
        {
            var dict = new Dictionary<string, object>();

            // Copy all existing properties
            foreach (var prop in item.EnumerateObject())
            {
                dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText())!;
            }

            // Enrich sender user with avatar
            if (item.TryGetProperty("senderUser", out var senderUser))
            {
                if (senderUser.TryGetProperty("userId", out var senderUserId))
                {
                    var senderDict = JsonSerializer.Deserialize<Dictionary<string, object>>(senderUser.GetRawText())!;
                    var userId = senderUserId.GetString()!;
                    var avatarUrl = await userCache.GetUserAvatarAsync(userId);

                    // Always add avatarUrl property (empty string if fetch failed)
                    senderDict["avatarUrl"] = (object?)avatarUrl ?? "";
                    dict["senderUser"] = senderDict;
                }
            }

            // Enrich recipients with avatars
            if (item.TryGetProperty("recipients", out var recipients) && recipients.ValueKind == JsonValueKind.Array)
            {
                var enrichedRecipients = new List<Dictionary<string, object>>();
                foreach (var recipient in recipients.EnumerateArray())
                {
                    if (recipient.TryGetProperty("userId", out var recipientUserId))
                    {
                        var recipientDict = JsonSerializer.Deserialize<Dictionary<string, object>>(recipient.GetRawText())!;
                        var userId = recipientUserId.GetString()!;
                        var avatarUrl = await userCache.GetUserAvatarAsync(userId);

                        // Always add avatarUrl property (empty string if fetch failed)
                        recipientDict["avatarUrl"] = (object?)avatarUrl ?? "";
                        enrichedRecipients.Add(recipientDict);
                    }
                }
                dict["recipients"] = enrichedRecipients;
            }

            enrichedData.Add(dict);
        }

        return Results.Ok(new {
            data = enrichedData,
            metadata = new {
                cacheReady = true,
                totalCount = enrichedData.Count
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch cached good vibes: {ex.Message}",
            statusCode: 500
        );
    }
});

// Monthly leaderboard endpoints
app.MapGet("/api/stats/monthly/top-senders", async (GoodVibesCacheService cacheService, UserCacheService userCache, int? year, int? month, int? limit) =>
{
    try
    {
        var topLimit = limit ?? 5;
        var targetYear = year ?? DateTime.UtcNow.Year;
        var targetMonth = month ?? DateTime.UtcNow.Month;

        // Get all vibes from cache
        var allVibes = await cacheService.GetAllVibesAsync();

        // Count good vibes sent by each user in the specified month
        var senderCounts = new Dictionary<string, (int count, JsonElement user)>();

        foreach (var item in allVibes)
        {
            if (item.TryGetProperty("creationDate", out var creationDateProp) &&
                item.TryGetProperty("senderUser", out var senderUser))
            {
                if (DateTime.TryParse(creationDateProp.GetString(), out var creationDate))
                {
                    // Check if the vibe is from the target month/year
                    if (creationDate.Year == targetYear && creationDate.Month == targetMonth)
                    {
                        if (senderUser.TryGetProperty("userId", out var userId))
                        {
                            var userIdStr = userId.GetString()!;
                            if (senderCounts.TryGetValue(userIdStr, out var existing))
                            {
                                senderCounts[userIdStr] = (existing.count + 1, existing.user);
                            }
                            else
                            {
                                senderCounts[userIdStr] = (1, senderUser);
                            }
                        }
                    }
                }
            }
        }

        // Sort by count and take top N
        var topSenders = senderCounts
            .OrderByDescending(kvp => kvp.Value.count)
            .Take(topLimit)
            .Select(async kvp =>
            {
                var userDict = JsonSerializer.Deserialize<Dictionary<string, object>>(kvp.Value.user.GetRawText())!;
                var avatarUrl = await userCache.GetUserAvatarAsync(kvp.Key);
                if (avatarUrl != null)
                {
                    userDict["avatarUrl"] = avatarUrl;
                }

                return new
                {
                    user = userDict,
                    count = kvp.Value.count
                };
            })
            .ToList();

        var enrichedTopSenders = await Task.WhenAll(topSenders);

        return Results.Ok(new {
            topSenders = enrichedTopSenders,
            year = targetYear,
            month = targetMonth
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch monthly top senders: {ex.Message}",
            statusCode: 500
        );
    }
});

app.MapGet("/api/stats/monthly/top-recipients", async (GoodVibesCacheService cacheService, UserCacheService userCache, int? year, int? month, int? limit) =>
{
    try
    {
        var topLimit = limit ?? 5;
        var targetYear = year ?? DateTime.UtcNow.Year;
        var targetMonth = month ?? DateTime.UtcNow.Month;

        // Get all vibes from cache
        var allVibes = await cacheService.GetAllVibesAsync();

        // Count good vibes received by each user in the specified month
        var recipientCounts = new Dictionary<string, (int count, JsonElement user)>();

        foreach (var item in allVibes)
        {
            if (item.TryGetProperty("creationDate", out var creationDateProp) &&
                item.TryGetProperty("recipients", out var recipients) && recipients.ValueKind == JsonValueKind.Array)
            {
                if (DateTime.TryParse(creationDateProp.GetString(), out var creationDate))
                {
                    // Check if the vibe is from the target month/year
                    if (creationDate.Year == targetYear && creationDate.Month == targetMonth)
                    {
                        foreach (var recipient in recipients.EnumerateArray())
                        {
                            if (recipient.TryGetProperty("userId", out var userId))
                            {
                                var userIdStr = userId.GetString()!;
                                if (recipientCounts.TryGetValue(userIdStr, out var existing))
                                {
                                    recipientCounts[userIdStr] = (existing.count + 1, existing.user);
                                }
                                else
                                {
                                    recipientCounts[userIdStr] = (1, recipient);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Sort by count and take top N
        var topRecipients = recipientCounts
            .OrderByDescending(kvp => kvp.Value.count)
            .Take(topLimit)
            .Select(async kvp =>
            {
                var userDict = JsonSerializer.Deserialize<Dictionary<string, object>>(kvp.Value.user.GetRawText())!;
                var avatarUrl = await userCache.GetUserAvatarAsync(kvp.Key);
                if (avatarUrl != null)
                {
                    userDict["avatarUrl"] = avatarUrl;
                }

                return new
                {
                    user = userDict,
                    count = kvp.Value.count
                };
            })
            .ToList();

        var enrichedTopRecipients = await Task.WhenAll(topRecipients);

        return Results.Ok(new {
            topRecipients = enrichedTopRecipients,
            year = targetYear,
            month = targetMonth
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Failed to fetch monthly top recipients: {ex.Message}",
            statusCode: 500
        );
    }
});

app.Run("http://localhost:5000");

Console.WriteLine("Server is running on http://localhost:5000");
Console.WriteLine("Good Vibes list endpoint: http://localhost:5000/api/good-vibes");
Console.WriteLine("Good Vibes cached endpoint (fast): http://localhost:5000/api/good-vibes/cached");
Console.WriteLine("Single Good Vibe endpoint: http://localhost:5000/api/good-vibes/{id}");
Console.WriteLine("Collections endpoint: http://localhost:5000/api/good-vibes/collections");
Console.WriteLine("User info endpoint: http://localhost:5000/api/users/{userId}");
Console.WriteLine("Top senders: http://localhost:5000/api/stats/top-senders");
Console.WriteLine("Top recipients: http://localhost:5000/api/stats/top-recipients");
Console.WriteLine("Available months: http://localhost:5000/api/stats/available-months");
Console.WriteLine("Monthly top senders: http://localhost:5000/api/stats/monthly/top-senders");
Console.WriteLine("Monthly top recipients: http://localhost:5000/api/stats/monthly/top-recipients");
Console.WriteLine("Health check: http://localhost:5000/health");
Console.WriteLine("Swagger UI: http://localhost:5000/swagger");

// User cache service for avatar caching
public class UserCacheService
{
    private readonly IMemoryCache _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserCacheService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore = new(10, 10); // Limit concurrent requests

    public UserCacheService(IMemoryCache cache, HttpClient httpClient, ILogger<UserCacheService> logger, IConfiguration configuration)
    {
        _cache = cache;
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string?> GetUserAvatarAsync(string userId)
    {
        string cacheKey = $"user_avatar_{userId}";
        
        if (_cache.TryGetValue(cacheKey, out string? cachedAvatar))
        {
            return cachedAvatar;
        }

        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring semaphore
            if (_cache.TryGetValue(cacheKey, out cachedAvatar))
            {
                return cachedAvatar;
            }

            var avatar = await FetchUserAvatarWithRetryAsync(userId);
            
            // Cache for 1 hour
            _cache.Set(cacheKey, avatar, TimeSpan.FromHours(1));
            
            return avatar;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string?> FetchUserAvatarWithRetryAsync(string userId)
    {
        int maxRetries = 3;
        int baseDelayMs = 1000;
        var apiKey = _configuration["WorkleapCOToken"] ?? _configuration["OfficevibeApiKey"] ?? "1dfdd5f52b3c4829adc15e794485eea6";

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.workleap.com/public/users/{userId}");
                request.Headers.Add("workleap-subscription-key", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    _logger.LogWarning("Rate limited for user {UserId}, waiting {DelaySeconds}s", userId, delay.TotalSeconds);
                    await Task.Delay(delay);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var userDoc = JsonDocument.Parse(content);

                    // Try to get image URL from the extension schema
                    if (userDoc.RootElement.TryGetProperty("urn:workleap:params:scim:schemas:extension:user:2.0:User", out var extension))
                    {
                        if (extension.TryGetProperty("imageUrls", out var imageUrls))
                        {
                        // Try to get appropriately sized image for avatars
                        if (imageUrls.TryGetProperty("32x32", out var size32))
                            return size32.GetString();
                        if (imageUrls.TryGetProperty("48x48", out var size48))
                            return size48.GetString();
                        if (imageUrls.TryGetProperty("24x24", out var size24))
                            return size24.GetString();
                        if (imageUrls.TryGetProperty("64x64", out var size64))
                            return size64.GetString();
                        // Fallback to any available size
                        foreach (var prop in imageUrls.EnumerateObject())
                            return prop.Value.GetString();                        }
                    }

                    // No image URL found in extension schema
                    _logger.LogInformation("No avatar found for user {UserId} - missing imageUrls in extension schema", userId);
                }
                else
                {
                    _logger.LogWarning("Failed to fetch avatar for user {UserId}: HTTP {StatusCode}", userId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching avatar for user {UserId}, attempt {Attempt}", userId, attempt + 1);
            }
        }

        return null; // Return null if all attempts failed
    }
}

// Background service to cache all good vibes
public class GoodVibesCacheService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoodVibesCacheService> _logger;
    private readonly IConfiguration _configuration;
    private List<JsonElement> _cachedVibes = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isReady = false;
    private const string OFFICEVIBE_API_URL = "https://api.workleap.com/officevibe/goodvibes";

    public GoodVibesCacheService(IHttpClientFactory httpClientFactory, ILogger<GoodVibesCacheService> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public bool IsReady => _isReady;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GoodVibesCacheService is starting - will fetch all good vibes in the background...");

        // Start initial load in background without blocking startup
        _ = Task.Run(RefreshCacheAsync, stoppingToken);

        // Refresh every 5 minutes
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            await RefreshCacheAsync();
        }
    }

    private async Task RefreshCacheAsync()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var allVibes = new List<JsonElement>();
            string? continuationToken = null;
            int pageCount = 0;
            var apiKey = _configuration["WorkleapCOToken"] ?? _configuration["OfficevibeApiKey"] ?? "1dfdd5f52b3c4829adc15e794485eea6";

            do
            {
                var url = $"{OFFICEVIBE_API_URL}?isPublic=true&limit=100";
                if (!string.IsNullOrEmpty(continuationToken))
                {
                    url += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("workleap-subscription-key", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to fetch good vibes: {response.StatusCode}");
                    break;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var jsonDocument = JsonDocument.Parse(content);
                var root = jsonDocument.RootElement.Clone();

                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        allVibes.Add(item.Clone());
                    }
                }

                pageCount++;

                // Check for continuation token
                continuationToken = null;
                if (root.TryGetProperty("metadata", out var metadata))
                {
                    if (metadata.TryGetProperty("continuationToken", out var token))
                    {
                        continuationToken = token.GetString();
                    }
                }

            } while (!string.IsNullOrEmpty(continuationToken));

            await _semaphore.WaitAsync();
            try
            {
                _cachedVibes = allVibes;
                _isReady = true;
                _logger.LogInformation("Cached {Count} good vibes from {PageCount} pages - cache is ready", allVibes.Count, pageCount);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing good vibes cache");
        }
    }

    public async Task<List<JsonElement>> GetAllVibesAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return new List<JsonElement>(_cachedVibes);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
