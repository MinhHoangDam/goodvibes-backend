using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

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

// Your Officevibe API key
const string OFFICEVIBE_API_KEY = "1dfdd5f52b3c4829adc15e794485eea6";
const string OFFICEVIBE_API_URL = "https://api.workleap.com/officevibe/goodvibes";

// Health check endpoint
app.MapGet("/health", () => 
{
    return Results.Ok(new { status = "ok", message = "Server is running" });
});

// Good Vibes list endpoint
app.MapGet("/api/good-vibes", async (HttpClient httpClient, UserCacheService userCache, bool? isPublic) =>
{
    try
    {
        // Build URL with optional isPublic filter
        var url = OFFICEVIBE_API_URL;
        if (isPublic.HasValue)
        {
            url += $"?isPublic={isPublic.Value.ToString().ToLower()}";
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
                        var avatarUrl = await userCache.GetUserAvatarAsync(senderUserId.GetString()!);
                        if (avatarUrl != null)
                        {
                            var senderDict = JsonSerializer.Deserialize<Dictionary<string, object>>(senderUser.GetRawText())!;
                            senderDict["avatarUrl"] = avatarUrl;
                            dict["senderUser"] = senderDict;
                        }
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
                            var avatarUrl = await userCache.GetUserAvatarAsync(recipientUserId.GetString()!);
                            if (avatarUrl != null)
                            {
                                recipientDict["avatarUrl"] = avatarUrl;
                            }
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
                var avatarUrl = await userCache.GetUserAvatarAsync(senderUserId.GetString()!);
                if (avatarUrl != null)
                {
                    var senderDict = JsonSerializer.Deserialize<Dictionary<string, object>>(senderUser.GetRawText())!;
                    senderDict["avatarUrl"] = avatarUrl;
                    dict["senderUser"] = senderDict;
                }
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
                    var avatarUrl = await userCache.GetUserAvatarAsync(recipientUserId.GetString()!);
                    if (avatarUrl != null)
                    {
                        recipientDict["avatarUrl"] = avatarUrl;
                    }
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
                
                if (reply.TryGetProperty("senderUser", out var replySender))
                {
                    if (replySender.TryGetProperty("userId", out var replySenderUserId))
                    {
                        var avatarUrl = await userCache.GetUserAvatarAsync(replySenderUserId.GetString()!);
                        if (avatarUrl != null)
                        {
                            var replySenderDict = JsonSerializer.Deserialize<Dictionary<string, object>>(replySender.GetRawText())!;
                            replySenderDict["avatarUrl"] = avatarUrl;
                            replyDict["senderUser"] = replySenderDict;
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

app.Run("http://localhost:5000");

Console.WriteLine("Server is running on http://localhost:5000");
Console.WriteLine("Good Vibes list endpoint: http://localhost:5000/api/good-vibes");
Console.WriteLine("Single Good Vibe endpoint: http://localhost:5000/api/good-vibes/{id}");
Console.WriteLine("Collections endpoint: http://localhost:5000/api/good-vibes/collections");
Console.WriteLine("User info endpoint: http://localhost:5000/api/users/{userId}");
Console.WriteLine("Health check: http://localhost:5000/health");
Console.WriteLine("Swagger UI: http://localhost:5000/swagger");

// User cache service for avatar caching
public class UserCacheService
{
    private readonly IMemoryCache _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserCacheService> _logger;
    private readonly SemaphoreSlim _semaphore = new(10, 10); // Limit concurrent requests
    private const string OFFICEVIBE_API_KEY = "1dfdd5f52b3c4829adc15e794485eea6";

    public UserCacheService(IMemoryCache cache, HttpClient httpClient, ILogger<UserCacheService> logger)
    {
        _cache = cache;
        _httpClient = httpClient;
        _logger = logger;
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

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.workleap.com/public/users/{userId}");
                request.Headers.Add("workleap-subscription-key", OFFICEVIBE_API_KEY);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                    _logger.LogWarning($"Rate limited for user {userId}, waiting {delay.TotalSeconds}s");
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching avatar for user {userId}, attempt {attempt + 1}");
            }
        }

        return null; // Return null if all attempts failed
    }
}
