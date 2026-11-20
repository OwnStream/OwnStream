# OwnStream

OwnStream is a "stil lwork in progress but good enough to use daily" media server.

## Features

### Ahead of Time Transcoding

OwnStream transcodes all library media to the chosen formats ahead of time, so that if a lot of users want to stream
media, the server doesn't slow down due to multiple transcodes running in parallel. This comes with the cost of using
a forced trade of using more storage, while saving on compute.

### Automatic Intro/Outro Detection

While importing items into the library, OwnStream scans all video files and detects similar sections to provide a "Skip
Intro" button as a convenience.

### Content Localization

OwnStream stores localized content information for all languages it can find, which means all users can browse the
library in their preferred language.

## Development

OwnStream is built with .NET 10. For development of the main server, a copy of the .NET SDK is required.

By default, `dotnet restore` will most likely fail due to the `OwnStream.PluginCore` dependency, which lives in GitHub
Package Registry. To build and develop OwnStream, you should edit the `nuget.config` file at the project root to contain
your GitHub username and a access token with the `packages:read` scope.

To build the Docker image with `nuget.config`, run `docker buildx build --secret id=nuget_config,src=nuget.config -t ownstream .`

Development is split to multiple repositories. **This is the backend server repository**, and by itself, it doesn't
provide a usable UI. For a fuller development environment, visit the following repositories:

- **[ownstream-web](https://github/OwnStream/ownstream-web)**: The web app for OwnStream.
- **[OwnStream-Android](https://github/OwnStream/OwnStream-Android)**: The Android & Android TV apps for OwnStream.

