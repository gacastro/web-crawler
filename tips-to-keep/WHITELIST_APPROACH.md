# Whitelist vs Blacklist Approach

## What Changed

We switched from a **blacklist** approach (reject known bad extensions) to a **whitelist** approach (allow only known good extensions).

## Blacklist (Old)
```csharp
// ❌ "Block everything we know is bad"
if (!string.IsNullOrEmpty(extension) && !HtmlExtensions.Contains(extension))
{
    return false;  // Block PDFs, images, unknown types, etc.
}
```

**Problems:**
- ❌ Assumes we know all bad file types
- ❌ New file type we haven't seen? It gets crawled!
- ❌ False sense of security
- ❌ Hard to reason about edge cases

## Whitelist (New)
```csharp
// ✅ "Only allow what we explicitly know is good"
if (!string.IsNullOrEmpty(extension))
{
    if (!HtmlExtensions.Contains(extension))
    {
        return false;  // Reject unknown extensions
    }
}
// If NO extension, assume it's HTML
```

**Advantages:**
- ✅ Explicit about what we accept
- ✅ Unknown file types are rejected by default
- ✅ Safer security posture
- ✅ Easy to audit and maintain
- ✅ Clear intent in code

## Examples

### URL: `https://example.com/page.pdf`
- Extension: `.pdf`
- In whitelist? NO
- Result: ❌ Rejected

### URL: `https://example.com/page.html`
- Extension: `.html`
- In whitelist? YES
- Result: ✅ Allowed

### URL: `https://example.com/page.xyz` (unknown format)
- Extension: `.xyz`
- In whitelist? NO
- Result: ❌ Rejected (safer choice)

### URL: `https://example.com/page` (no extension)
- Extension: (none)
- Assumption: HTML
- Result: ✅ Allowed (common for dynamic URLs)

## The Whitelist

```csharp
private static readonly HashSet<string> HtmlExtensions = new(StringComparer.OrdinalIgnoreCase)
{
    ".html", ".htm",      // Standard HTML
    ".php", ".asp", ".aspx", ".jsp", ".jspx",  // Server-side rendered
    ".cfm", ".cgi",       // Other dynamic content
    ".shtml"              // Server-side includes
};
```

**Easy to expand:** Just add new extensions as needed, but default to **reject**.

## Security Principle

> **Default deny is safer than default allow**

- ✅ Whitelist: "Only let through what we explicitly trust"
- ❌ Blacklist: "Let everything through except what we know is bad"

This is a fundamental security principle used in firewalls, access control, and content filtering.

## Real-World Scenarios

| URL | Blacklist | Whitelist | Better? |
|-----|-----------|-----------|---------|
| `.html` | ✅ Allow | ✅ Allow | Same |
| `.pdf` | ❌ Reject | ❌ Reject | Same |
| `.exe` (executable) | ❌ Reject | ❌ Reject | Same |
| `.iso` (disk image) | ❌ Reject | ❌ Reject | Same |
| `.mp4` (video) | ❌ Reject | ❌ Reject | Same |
| `.unknown` (new type) | ✅ Allow ⚠️ | ❌ Reject ✅ | **Whitelist wins!** |
| `.svelte` (new JS framework) | ❌ Reject ⚠️ | ❌ Reject | Whitelist explicit |

## When to Use Each

### Blacklist is okay for:
- Filtering spam/profanity (you know the bad words)
- Content moderation (explicit content)
- Ad blocking (known bad sources)

### Whitelist is better for:
- **File type validation** ← We're doing this ✅
- Access control
- API endpoints
- Payment processing
- Security-sensitive operations

## Maintenance

**Adding new crawlable formats:**

Easy! Just update the whitelist:
```csharp
private static readonly HashSet<string> HtmlExtensions = new(StringComparer.OrdinalIgnoreCase)
{
    ".html", ".htm", ".php", ".asp", ".aspx",
    ".jsp", ".jspx", ".cfm", ".cgi", ".shtml",
    // Add new formats here as needed
    ".md",      // Markdown (if you want to crawl it)
    ".rst"      // ReStructuredText
};
```

## Testing

Our tests validate both:
- ✅ Known good extensions are accepted
- ✅ Known bad extensions are rejected
- ✅ Unknown extensions are rejected (safety)
- ✅ No extension defaults to HTML (reasonable assumption)

All 32 tests passing! 🎉

