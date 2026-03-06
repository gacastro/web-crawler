# HEAD Request Optimization: Lean and Mean

## Why This Approach is Perfect

You were spot on! Since `ShouldCrawlUrl()` already filters out PDFs, images, etc., we only need to validate **size** in `GetPageContentAsync()`. No redundant content-type checks needed.

## The Architecture

**Layered filtering:**
1. **URL Queuing** (`ShouldCrawlUrl`): Filter out non-HTML before even requesting
2. **Network Validation** (`GetPageContentAsync`): Only validate size via HEAD
3. **Memory Load** (`GetPageWithGetAsync`): Simple ReadAsStringAsync with final sanity check

## The Approach (HEAD + GET)
```csharp
// 1. Lightweight HEAD request - check only Content-Length
var headResponse = await _httpClient.SendAsync(
    new HttpRequestMessage(HttpMethod.Head, url),
    HttpCompletionOption.ResponseHeadersRead);

// Validate only size (content-type already validated by ShouldCrawlUrl)
if (headResponse.Content.Headers.ContentLength.Value > maxContentLength)
    return null; // Reject before downloading

// 2. Safe to download - we know it's HTML and not too large
var html = await response.Content.ReadAsStringAsync();
```

**Advantages:**
- ✅ **Lean logic** - only size validation, no redundant checks
- ✅ **Clear separation** - ShouldCrawlUrl filters, GetPageContent validates size
- ✅ **Super efficient** - HEAD just checks one header
- ✅ **Simple memory loading** - ReadAsStringAsync (no streaming)
- ✅ **Fallback handling** - if HEAD fails, try GET directly

## Flow Diagram

```
Request URL
    ↓
Was it queued by ShouldCrawlUrl()? 
    ↓ YES (so it's HTML-like)
HEAD request (check size only)
    ↓
├─ Fails? → Try GET instead (server may not support HEAD)
├─ Too large? → Return null
└─ Good size? ↓
    ↓
    GET request (full page)
    ↓
    Load into memory (ReadAsStringAsync)
    ↓
    Final size sanity check
    ↓
    Return HTML
```

## Defense Layers

| Layer | What it filters | Method |
|-------|-----------------|--------|
| **URL Queuing** | PDFs, images, non-HTML | `ShouldCrawlUrl()` |
| **HEAD Request** | Oversized files (Content-Length) | HEAD check before download |
| **HttpClient Buffer** | Oversized responses | `MaxResponseContentBufferSize` |
| **Memory** | Final sanity check | Length check after load |

Four layers of protection ensure memory safety!

**How they work together:**
1. HEAD checks headers (fast, no body downloaded)
2. If good, GET request happens
3. HttpClient enforces buffer limit (throws if exceeds 10MB)
4. Final sanity check after ReadAsStringAsync

## Real-World Benefits

### Scenario 1: Small HTML page (50KB)
- HEAD: ~1KB transfer → size validated ✅ → GET: 50KB transfer ✅
- Result: Fast, no wasted resources

### Scenario 2: Someone passes a PDF as starting URL
- `ShouldCrawlUrl()`: PDF extension detected → Rejected at queue stage ✅
- Never even makes a HEAD request
- Result: Filtered before network call

### Scenario 3: Mislabeled file (Content-Length says 5MB, actually 10MB)
- HEAD: 5MB reported → looks good
- GET: Loads into memory
- Final check: 10MB > limit → Rejected ✅
- Result: Double-check saves us

## Code Structure

```
Queue URL via ShouldCrawlUrl()
    ↓
GetPageContentAsync(url)  
    ├─ HEAD request → check size only
    └─ GetPageWithGetAsync() 
        ├─ GET request
        ├─ ReadAsStringAsync()
        └─ Final size check
```

All content-type validation happens in `ShouldCrawlUrl()` - GetPageContent is purely about size!

