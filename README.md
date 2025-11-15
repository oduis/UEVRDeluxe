# Unreal Engine VR Easy Injector
Play many esp. first person Unreal Engine games in VR!  
Replacing the old UEVR Injector with a much more user friendly, easy to use and automated app.  
It auto-scans you Steam/EPIC/GOG installed games and replaces tools like launcher, UEVRInjector, OpenXR switchers.

Most profile websites are outdated, profiles floating in Discord discussions, hard to discover, hard to install for laymen (including PAKs etc.).
UEVR Easy Injector contains a web database for easy discovery and installation, plus adds authors information on how to use the profiles.

It also adds voice commands and global hotkeys to improve the VR experience.

It uses [PrayDogs UEVR engine](https://github.com/praydog/UEVR) but does not need or use an an UEVR classic installation. 
UEVR Easy allows you to install any specific version of the UEVR backend using its integrated updater.

## Installation
Simply download the setup program from the release section.  
Some virus scanner mark the UEVR engine as a false positive, because it (suspiciously) injects itself into other game apps.
Please add an exception for UEVR in your antivirus software if necessary.  
Do **not** run the classical UEVR injector in parallel (side by side install is no problem, just not start them).

## Submitting profiles
Please only submit tested profiles, not work in progress.  
[Click here on how to submit a profile](https://uevrdeluxe.org/SubmitProfile.html)

# Contributing
The backend database lives on Azure and is privately paid for by myself. 
- Do not make any code changes that would result in more calls or loads to the backend
- Do not use the backend database from other apps
- Do NOT start Visual Studio as Admin, as this [will crash the debugger](https://github.com/microsoft/WindowsAppSDK/issues/567) when accessing system functions

While this is free to use for non-commercial users, the project is copyrighted and may not be used commercially.