# Rive .NET MAUI

RivePlayer control for .NET MAUI. Based on https://github.com/rive-app/rive-sharp

![Rive](images/rive-maui.gif)

## Building

You just need to fetch the rive-cpp submodule:

```
git submodule update --init
```

To build, you first need to generate rive.vcproj, the project that builds the
native rive.dll:

```
cd Native
premake5.exe vs2022
```
After that, you should be able to open RiveSharpSample.sln in Visual Studio, build, and run!