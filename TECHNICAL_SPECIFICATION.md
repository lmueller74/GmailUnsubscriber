# Gmail Unsubscriber - Technical Specification

## Document History

| Version | Date | Description |
|---------|------|-------------|
| 1.0 | 11/28/2024 | Initial specification |

---

## 1. Overview

### 1.1 Purpose

Gmail Unsubscriber is a command-line utility that automates the process of unsubscribing from email mailing lists. Users label unwanted emails in Gmail, and the tool processes those emails by extracting and executing unsubscribe requests.

### 1.2 Problem Statement

Users receive numerous marketing emails and newsletters. Manually unsubscribing from each is time-consuming. This tool automates the process by:

1. Finding emails the user has marked for unsubscription
2. Extracting unsubscribe URLs from email headers or HTML content
3. Executing HTTP requests or sending mailto responses
4. Organizing processed emails with labels

### 1.3 Target Platform

- Windows 10/11
- .NET 8.0 Runtime
- Gmail accounts (personal or Google Workspace)

---

## 2. Architecture

### 2.1 Solution Structure

```
GmailUnsubscriber/
├── GmailUnsubscriber.sln
├── GmailUnsubscriber.Core/           # Domain models and interfaces
├── GmailUnsubscriber.Infrastructure/ # Gmail API and HTTP implementations
├── GmailUnsubscriber.App/            # Console application entry point
└── GmailUnsubscriber.Tests/          # Unit tests (xUnit)
```

### 2.2 Project Dependencies

```
┌─────────────────────────────────────────────────────────┐
│                  GmailUnsubscriber.App                  │
│                   (Console Application)                 │
└─────────────────────┬───────────────────────────────────┘
                      │ references
                      ▼
┌─────────────────────────────────────────────────────────┐
│             GmailUnsubscriber.Infrastructure            │
│              (Gmail API, HTTP, HTML Parsing)            │
└─────────────────────┬───────────────────────────────────┘
                      │ references
                      ▼
┌─────────────────────────────────────────────────────────┐
│                 GmailUnsubscriber.Core                  │
│                (Models and Interfaces)                  │
└─────────────────────────────────────────────────────────┘
```

### 2.3 External Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Google.Apis.Gmail.v1 | 1.* | Gmail API client |
| Google.Apis.Auth | (transitive) | OAuth 2.0 authentication |
| HtmlAgilityPack | 1.* | HTML parsing for link extraction |
| Microsoft.Extensions.Logging.Abstractions | 8.* | Logging interfaces |
| Microsoft.Extensions.Configuration.Json | 8.* | Configuration loading |
| Microsoft.Extensions.Logging.Console | 8.* | Console logging output |

---

## 3. Core Domain Models

### 3.1 AppSettings

Configuration model bound from `appsettings.json`.

```csharp
public class AppSettings
{
    public string SearchQuery { get; set; } = "label:unsubscribe";
    public string SourceLabel { get; set; } = "unsubscribe";
    public string ProcessedLabel { get; set; } = "Unsubscribed";
    public string FailedLabel { get; set; } = "unsubscribe-failed";
    public int MaxMessagesPerRun { get; set; } = 500;
}
```

| Property | Description |
|----------|-------------|
| `SearchQuery` | Gmail search query to find emails to process |
| `SourceLabel` | Label removed from emails after processing |
| `ProcessedLabel` | Label applied to successfully unsubscribed emails |
| `FailedLabel` | Label applied to emails that could not be processed |
| `MaxMessagesPerRun` | Maximum emails to process per execution (Gmail API limit: 500) |

### 3.2 EmailMessage

Represents an email retrieved from Gmail.

```csharp
public class EmailMessage
{
    public required string Id { get; set; }
    public string? Subject { get; set; }
    public string? From { get; set; }
    public string? ListUnsubscribeHeader { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
}
```

### 3.3 UnsubscribeInfo

Contains extracted unsubscribe information for execution.

```csharp
public class UnsubscribeInfo
{
    public required string Url { get; set; }
    public UnsubscribeMethod Method { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public required string MessageId { get; set; }
    public string? Subject { get; set; }
    public string? From { get; set; }
}
```

### 3.4 UnsubscribeMethod

Enumeration of supported unsubscribe mechanisms.

```csharp
public enum UnsubscribeMethod
{
    Get,    // HTTP GET request
    Post,   // HTTP POST request
    Mailto  // Email-based unsubscribe
}
```

---

## 4. Interfaces

### 4.1 IGmailClient

Abstraction over Gmail API operations.

```csharp
public interface IGmailClient
{
    Task<IEnumerable<EmailMessage>> SearchAsync(string query, int maxResults);
    Task<EmailMessage?> GetMessageAsync(string messageId);
    Task ApplyLabelAsync(string messageId, string labelName);
    Task RemoveLabelAsync(string messageId, string labelName);
    Task RemoveFromInboxAsync(string messageId);
    Task SendEmailAsync(string to, string subject, string body);
}
```

### 4.2 IMessageService

Extracts unsubscribe information from email messages.

```csharp
public interface IMessageService
{
    UnsubscribeInfo? ExtractUnsubscribeInfo(EmailMessage message);
}
```

### 4.3 IUnsubscribeService

Executes unsubscribe requests.

```csharp
public interface IUnsubscribeService
{
    Task<bool> ExecuteAsync(UnsubscribeInfo info);
}
```

### 4.4 IHtmlExtractionService

Parses HTML content and headers for unsubscribe links.

```csharp
public interface IHtmlExtractionService
{
    IEnumerable<string> ExtractUnsubscribeLinks(string htmlContent);
    string? ParseListUnsubscribeHeader(string headerValue);
}
```

---

## 5. Infrastructure Services

### 5.1 GmailAuthService

Handles OAuth 2.0 authentication with Google.

**Token Storage:** `%LOCALAPPDATA%\GmailUnsubscriber.Auth\`

**Required Scopes:**
- `https://www.googleapis.com/auth/gmail.modify`
- `https://www.googleapis.com/auth/gmail.send`
- `https://www.googleapis.com/auth/gmail.labels`

**Authentication Flow:**
1. Load client secrets from `credentials.json`
2. Check for existing token in FileDataStore
3. If no token, open browser for user consent
4. Store token for future sessions
5. Return authenticated `GmailService` instance

### 5.2 GmailClient

Implements `IGmailClient` using Google.Apis.Gmail.v1.

**Key Operations:**

| Method | Gmail API Call |
|--------|----------------|
| `SearchAsync` | `users.messages.list` |
| `GetMessageAsync` | `users.messages.get` (format: FULL) |
| `ApplyLabelAsync` | `users.messages.modify` (addLabelIds) |
| `RemoveLabelAsync` | `users.messages.modify` (removeLabelIds) |
| `RemoveFromInboxAsync` | `users.messages.modify` (remove INBOX label) |
| `SendEmailAsync` | `users.messages.send` |

**Label Management:**
- Labels are cached after first lookup
- Non-existent labels are created automatically
- Label IDs are resolved by name (case-insensitive)

### 5.3 HtmlExtractionService

Parses email content for unsubscribe links.

**Extraction Priority:**
1. `List-Unsubscribe` header (RFC 2369)
2. HTML anchor tags containing unsubscribe keywords

**Header Parsing:**
- Supports `<https://...>` format
- Supports `<mailto:...>` format
- Prefers HTTP URLs over mailto

**HTML Link Detection Keywords:**
- URL contains: `unsubscribe`, `optout`, `opt-out`, `remove`
- Link text contains: `unsubscribe`, `opt out`, `opt-out`

### 5.4 MessageService

Implements `IMessageService` to coordinate extraction.

**Extraction Logic:**
1. Check `List-Unsubscribe` header first (preferred, RFC standard)
2. Fall back to HTML body parsing
3. Determine method based on URL scheme (mailto vs http/https)

### 5.5 HttpUnsubscribeService

Implements `IUnsubscribeService` to execute unsubscribe requests.

**Supported Methods:**

| Method | Implementation |
|--------|----------------|
| HTTP GET | `HttpClient.GetAsync()` |
| HTTP POST | `HttpClient.PostAsync()` with empty body |
| Mailto | Parse mailto URL, send email via Gmail API |

**HTTP Client Configuration:**
- User-Agent: Chrome browser string (prevents bot blocking)
- Follows redirects automatically
- Success: 2xx status codes or redirect responses

**Mailto Parsing:**
- Extracts recipient from `mailto:address`
- Parses `?subject=` and `?body=` query parameters
- Defaults: Subject="Unsubscribe", Body="Please unsubscribe me from this mailing list."

---

## 6. Application Workflow

### 6.1 Startup Sequence

```
1. Load configuration from appsettings.json
2. Initialize logging (Console, Debug level)
3. Validate credentials.json exists
4. Authenticate with Gmail API
5. Create service instances
6. Execute main processing loop
```

### 6.2 Processing Loop

```
FOR each message matching SearchQuery:
    1. Log message details (ID, From, Subject)
    2. Extract unsubscribe info

    IF no unsubscribe info found:
        - Apply FailedLabel
        - Remove SourceLabel
        - Continue to next message

    3. Execute unsubscribe request

    IF successful:
        - Apply ProcessedLabel
        - Remove from Inbox (archive)
        - Remove SourceLabel
    ELSE:
        - Apply FailedLabel
        - Remove SourceLabel
```

### 6.3 State Transitions

```
                    ┌─────────────────┐
                    │  User labels    │
                    │  "unsubscribe"  │
                    └────────┬────────┘
                             │
                             ▼
                    ┌─────────────────┐
                    │   Tool runs     │
                    │   SearchQuery   │
                    └────────┬────────┘
                             │
              ┌──────────────┴──────────────┐
              │                             │
              ▼                             ▼
    ┌─────────────────┐           ┌─────────────────┐
    │    SUCCESS      │           │     FAILED      │
    │                 │           │                 │
    │ +ProcessedLabel │           │ +FailedLabel    │
    │ -SourceLabel    │           │ -SourceLabel    │
    │ -INBOX          │           │                 │
    └─────────────────┘           └─────────────────┘
```

---

## 7. Configuration

### 7.1 appsettings.json

```json
{
  "SearchQuery": "label:unsubscribe",
  "SourceLabel": "unsubscribe",
  "ProcessedLabel": "Unsubscribed",
  "FailedLabel": "unsubscribe-failed",
  "MaxMessagesPerRun": 500
}
```

### 7.2 credentials.json

OAuth 2.0 client credentials downloaded from Google Cloud Console.

```json
{
  "installed": {
    "client_id": "xxxxx.apps.googleusercontent.com",
    "project_id": "project-name",
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token",
    "client_secret": "xxxxx",
    "redirect_uris": ["http://localhost"]
  }
}
```

---

## 8. Security Considerations

### 8.1 Credential Storage

| Item | Location | Protection |
|------|----------|------------|
| OAuth Client Secret | `credentials.json` | User responsibility, excluded from git |
| OAuth Token | `%LOCALAPPDATA%\GmailUnsubscriber.Auth\` | User profile directory, persisted |

### 8.2 Scope Minimization

Only requested scopes necessary for operation:
- `gmail.modify` - Read emails, apply labels, archive
- `gmail.send` - Send mailto unsubscribe emails
- `gmail.labels` - Create/manage labels

### 8.3 No Data Transmission

- No telemetry or analytics
- No external servers contacted except Gmail API and unsubscribe URLs
- All processing is local

### 8.4 Unsubscribe Safety

- Only processes user-labeled emails (no automatic selection)
- Failed attempts are labeled for manual review
- No destructive operations (emails are archived, not deleted)

---

## 9. Error Handling

### 9.1 Authentication Errors

| Error | Handling |
|-------|----------|
| Missing credentials.json | Log error, exit gracefully |
| Token expired | Automatic refresh via Google.Apis.Auth |
| User denies consent | Exit gracefully |

### 9.2 Processing Errors

| Error | Handling |
|-------|----------|
| No unsubscribe link found | Apply FailedLabel, continue |
| HTTP request fails | Apply FailedLabel, continue |
| Gmail API error | Log error, continue to next message |

### 9.3 Label Errors

| Error | Handling |
|-------|----------|
| Label not found (remove) | Log warning, continue |
| Label creation fails | Propagate exception |

---

## 10. Testing

### 10.1 Test Coverage

| Component | Test Class | Tests |
|-----------|------------|-------|
| HtmlExtractionService | HtmlExtractionServiceTests | 12 |
| MessageService | MessageServiceTests | 6 |

### 10.2 Test Categories

**HtmlExtractionService Tests:**
- Extract links with unsubscribe in URL
- Extract links with unsubscribe in text
- Extract optout links
- Handle missing links
- Handle duplicate links
- Handle empty/null HTML
- Parse HTTP List-Unsubscribe header
- Parse mailto List-Unsubscribe header
- Parse combined headers (prefer HTTP)
- Handle invalid headers

**MessageService Tests:**
- Extract from List-Unsubscribe header
- Extract mailto with correct method
- Extract from HTML body
- Prefer header over HTML body
- Handle missing unsubscribe info
- Preserve message metadata

---

## 11. Build and Deployment

### 11.1 Development Build

```bash
dotnet restore
dotnet build
```

### 11.2 Release Build

```bash
dotnet publish GmailUnsubscriber.App -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -o ./publish
```

### 11.3 Deployment Artifacts

| File | Required | Description |
|------|----------|-------------|
| GmailUnsubscriber.App.exe | Yes | Self-contained executable |
| appsettings.json | Yes | Configuration |
| credentials.json | Yes | OAuth client credentials |

---

## 12. Future Considerations

- [ ] Dry-run mode (preview without executing)
- [ ] File-based logging
- [ ] Linux/macOS support
- [ ] GUI version
- [ ] Scheduled execution via Windows Task Scheduler
- [ ] Batch confirmation before processing
