# Contributing to Fast IoC

Thanks for helping out. Bug reports, documentation fixes and pull requests are welcome.

## Before you start

- Search the [existing issues](https://github.com/sgonzalezrozo/unity-fast-ioc/issues) first.
- Open an issue before starting a large change, so we can agree on the approach.
- Keep each pull request focused on one change.

## Set up

1. Create a Unity 2022.3 or newer project.
2. Clone this repository.
3. In **Window > Package Manager**, select **+ > Add package from disk** and choose this repository's `package.json`.
4. Install [Tools Core](https://github.com/sgonzalezrozo/unity-tools-core) first, then run **Tools > FastIoC > Setup**.

## Make a change

- Match the style of the surrounding code.
- Add XML documentation to new public APIs.
- Don't leave internal notes in comments, such as planning phases or old file locations.
- Update `README.md` when behavior changes.
- Add an entry to `CHANGELOG.md`, and mark breaking changes with **Breaking**.
- Unity needs a `.meta` file for every new file and folder in the package, so commit those too. Folders ending in `~` and folders starting with `.` don't need them.

## Test your change

The package has no automated test suite yet. Test your change in a Unity project, in Play Mode and in a player build.

In your pull request, list the Unity version, scripting backend and platform you tested.

## Release checklist

1. Update `version` in `package.json`.
2. Update the version in the README badge, pin example and Project status section.
3. Move the changelog entries under the new version.
4. Create the matching tag, such as `v0.3.0`.
