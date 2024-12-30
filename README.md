# Unreal VR Deluxe
Play many esp. first person Unreal Engine games in VR!  
Replacing the old UEVR Injector with a much more user friendly, easy to use and automated app.  
It auto-scans you Steam installed games and replaces tools like launcher, UEVRInjector, OpenXR switchers.

Most profile websites are outdated, profiles floating in Discord discussions, hard to discover.
UEVR Deluxe contains a web database for easy discovery and installation, plus adds authors information on how to use the profiles.

It uses [PrayDogs UEVR engine](https://github.com/praydog/UEVR) in the background but does not need an UEVR installation.

## Installation
Simply download the setup program from the release section.  
Some virus scanner mark the UEVR engine as a false positive, because it (suspiciously) injects itself into other game apps.
Please add an exception for UEVR in your antivirus software if necessary.

## Submitting profiles
Please only submit tested profiles, not work in progress.  
In the "Edit profile" screen is a "Publish" button. It cleans you profile from temp files like logs and dumps and packs it up in a ZIP, 
with some standardized variables.
On first submit it will generate two files in the profiles folder (if not existing already).  
Add descriptive data in these files (see below). 
Then publish again, and post the resulting ZIP file on this [Discord channel](https://discord.com/channels/747967102895390741/947806014344925274)

### ProfileMeta.json
Open with a text editor and fill in especially your authors name, a SHORT remark.

### ProfileDescription.md
Here you can add more information for the user, like required mods, restrictions etc.
The attention span these days is short. Simple bullet points, keep it short.
The format is MarkDown. There are a of online editors, like [Dillinger](https://dillinger.io/)

# Contributing
The backend database lives on Azure and is privately paid for by myself. 
- Do not make any code changes that would result in more calls or loads to the backend
- Do not use the backend database from other apps

Please mention the license, as it is designed to forbit closed source usage, hindering commercial exploitation.