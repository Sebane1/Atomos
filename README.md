# Atomos

Atomos is a tool for managing and installing mods for Final Fantasy XIV through integration with Penumbra.

## Getting Started

### Installation

Download the latest release from the GitHub repository and run `Launcher.exe`. A console window will appear first, followed by the main UI.

### Support

If you encounter any issues, please report them in the **Penumbra Mod Forwarder** category on [our Discord server](https://discord.gg/rtGXwMn7pX).

## Features

- **Mod Tracking**: Keeps track of installed mods
- **XMA Integration**: Browse and install mods directly from XIV Mod Archive (first 2 pages)
- **Error Reporting**: Optional Sentry integration for automatic error reporting
- **Penumbra Integration**: Seamless integration with the Penumbra mod framework

## Known Issues

- **Beta Releases**: Do not enable beta releases as this may cause update loops
- **Large Mod Files**: Large zip files may take time to unpack (optimisation in progress)
- **TexTools Conversion**: Occasional TexTools conversion failures may occur (program continues to function)

## XMA Integration Setup

To browse and install mods from XIV Mod Archive, you'll need to provide your XMA authentication cookie.

### Security Note
Your cookie is only used for authenticated requests to XMA. You can review the source code:
- [HttpClientFactory](https://github.com/CouncilOfTsukuyomi/PMF.CommonLib/blob/main/CommonLib/Factory/XmaHttpClientFactory.cs)
- [Mod Display](https://github.com/CouncilOfTsukuyomi/PMF.CommonLib/blob/main/CommonLib/Services/XmaModDisplay.cs)

### Setup Steps

1. Install a cookie editor extension for your browser (recommended: [Cookie Editor](https://cookie-editor.com/))
2. Log into your XIV Mod Archive account
3. Open the cookie editor extension and locate the cookie named `connect.sid`
4. Copy the cookie value (**Keep this private - it's your login credential!**)
5. In ModForwarder, go to Settings → Advanced
6. Paste the value into the "XIV Mod Archive Cookie" field

## Error Reporting (Sentry)

When Sentry logging is enabled, the following information is automatically sent when errors occur:
- Error message and stack trace
- IP address
- Computer account username
- Windows version
- .NET runtime version

This helps us identify and fix issues quickly. Error reporting is optional and can be disabled in settings.

## Upcoming Features
- Plugin Framework (XMA integration will be moved to this)

## Contributing

Please report bugs and feature requests through our Discord server in the appropriate channels. Pull requests are welcome on the GitHub repository.