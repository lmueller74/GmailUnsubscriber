# Gmail Unsubscriber

A command-line tool that automatically unsubscribes you from unwanted email lists by processing emails you've labeled for removal.

## How It Works

1. You label emails in Gmail with "unsubscribe"
2. Run this tool
3. It finds unsubscribe links (via `List-Unsubscribe` header or HTML body)
4. Executes the unsubscribe request (HTTP GET/POST or mailto)
5. Moves processed emails to "Unsubscribed" label
6. Failed emails go to "unsubscribe-failed" for manual review

## Prerequisites

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Google account

## Setup

### Step 1: Clone the Repository

```bash
git clone https://github.com/lmueller74/GmailUnsubscriber.git
cd GmailUnsubscriber
```

### Step 2: Create Google Cloud OAuth Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)

2. **Create a new project**
   - Click the project dropdown (top-left) → "New Project"
   - Name it something like "Gmail Unsubscriber"
   - Click "Create"

3. **Enable the Gmail API**
   - Go to "APIs & Services" → "Library"
   - Search for "Gmail API"
   - Click it → Click "Enable"

4. **Configure OAuth Consent Screen**
   - Go to "APIs & Services" → "OAuth consent screen"
   - Select "External" → Click "Create"
   - Fill in required fields:
     - App name: `Gmail Unsubscriber`
     - User support email: your email
     - Developer contact: your email
   - Click "Save and Continue"
   - On Scopes page, click "Add or Remove Scopes"
     - Add these scopes:
       - `https://www.googleapis.com/auth/gmail.modify`
       - `https://www.googleapis.com/auth/gmail.send`
       - `https://www.googleapis.com/auth/gmail.labels`
     - Click "Update" → "Save and Continue"
   - On Test Users page, click "Add Users"
     - Add your Gmail address
     - Click "Save and Continue"
   - Click "Back to Dashboard"

5. **Create OAuth Credentials**
   - Go to "APIs & Services" → "Credentials"
   - Click "+ Create Credentials" → "OAuth client ID"
   - Application type: **Desktop app**
   - Name: `Gmail Unsubscriber Desktop`
   - Click "Create"

6. **Download the credentials**
   - Click the download icon next to your new credential
   - Rename the file to `credentials.json`
   - Move it to `GmailUnsubscriber.App/credentials.json`

### Step 3: Build the Application

```bash
dotnet restore
dotnet build
```

### Step 4: Create the "unsubscribe" Label in Gmail

1. Open [Gmail](https://mail.google.com)
2. In the left sidebar, scroll down and click "Create new label"
3. Name it exactly: `unsubscribe`
4. Click "Create"

## Usage

### First Run

```bash
cd GmailUnsubscriber.App
dotnet run
```

On first run, a browser window will open asking you to authorize the app. Sign in with your Google account and allow access.

Your authorization token is saved locally at:
```
%LOCALAPPDATA%\GmailUnsubscriber.Auth\
```

### Label Emails for Unsubscription

1. In Gmail, select emails you want to unsubscribe from
2. Apply the "unsubscribe" label
3. Run the tool

### Configuration

Edit `appsettings.json` to customize:

```json
{
  "SearchQuery": "label:unsubscribe",
  "SourceLabel": "unsubscribe",
  "ProcessedLabel": "Unsubscribed",
  "FailedLabel": "unsubscribe-failed",
  "MaxMessagesPerRun": 500
}
```

| Setting | Description |
|---------|-------------|
| `SearchQuery` | Gmail search query to find emails to process |
| `SourceLabel` | Label to remove after processing |
| `ProcessedLabel` | Label applied to successfully unsubscribed emails |
| `FailedLabel` | Label applied to emails that couldn't be processed |
| `MaxMessagesPerRun` | Maximum emails to process per run |

## Building a Standalone Executable

To create a single `.exe` file you can run anywhere:

```bash
dotnet publish GmailUnsubscriber.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

The executable will be at `./publish/GmailUnsubscriber.App.exe`

Note: You still need `credentials.json` and `appsettings.json` in the same folder as the `.exe`.

## Troubleshooting

### "Access Denied" or "App not verified"
- Make sure you added your email as a test user in Google Cloud Console
- During sign-in, click "Advanced" → "Go to Gmail Unsubscriber (unsafe)"

### "credentials.json not found"
- Ensure the file is in the same directory as the executable
- Check the filename is exactly `credentials.json`

### Emails stuck in "unsubscribe-failed"
These emails either:
- Don't have an unsubscribe link
- Have a broken/expired unsubscribe URL
- Require CAPTCHA or login to unsubscribe

You'll need to unsubscribe from these manually.

### Token expired
Delete the token folder and re-run:
```bash
Remove-Item -Recurse "$env:LOCALAPPDATA\GmailUnsubscriber.Auth"
dotnet run
```

## Security Notes

- Your OAuth token is stored locally on your machine only
- The app never sends your data anywhere except to Gmail's API
- Each user creates their own Google Cloud project (no shared credentials)
- Review the source code - it's fully open source

## License

MIT License - do whatever you want with it.

## Contributing

Pull requests welcome! Ideas for improvement:
- [ ] Add dry-run mode (show what would be unsubscribed without doing it)
- [ ] Add logging to file
- [ ] Support for Linux/macOS
- [ ] GUI version
